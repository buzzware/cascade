using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// Functionality to manage cached collections and records,
	/// offering operations such as clearing, setting, and updating collections and individual records 
	/// in the cascading caching system.
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Clear a collection from all cache layers for the specified collection type.
		/// </summary>
		/// <param name="collectionName">The name of the collection to be cleared from the cache.</param>
		/// <typeparam name="Model">Specifies the model type of the collection.</typeparam>
		public async Task ClearCollection<Model>(string collectionName) {
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					// Iterate over each cache layer in reverse order and clear the collection
					await layer.StoreCollection(typeof(Model), collectionName, null, NowMs);
				}
			});
		}

		/// <summary>
		/// Replace a collection with a specified set of ids in all cache layers.
		/// </summary>
		/// <param name="collectionName">The name of the collection to be updated.</param>
		/// <param name="ids">An enumerable list of ids representing the new collection content.</param>
		/// <typeparam name="Model">Specifies the model type of the collection.</typeparam>
		/// <returns>The new set of ids as an IEnumerable of objects.</returns>
		public async Task<IEnumerable<object>> SetCollection<Model>(string collectionName, IEnumerable<object> ids) where Model : class {
			var result = ids.ToImmutableArray();
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.StoreCollection(typeof(Model), collectionName, result, NowMs);
				}
			});
			return result;
		}
		
		/// <summary>
		/// Add a single id to the beginning of a collection in all cache layers.
		/// </summary>
		/// <param name="collectionName">The name of the collection to be updated.</param>
		/// <param name="id">The id to prepend to the collection.</param>
		/// <typeparam name="Model">Specifies the model type of the collection.</typeparam>
		/// <returns>The updated collection as an IEnumerable of objects.</returns>
		public async Task<IEnumerable<object>> CollectionPrepend<Model>(string collectionName, object id) where Model : class {
			var collection = await GetCollection<Model>(collectionName);
			if (collection == null)
				return Array.Empty<object>();

			// Create a new collection with the prepended id
			var newCollection = collection.ToImmutableArray().Insert(0, id);
			await SetCollection<Model>(collectionName, newCollection);
			return newCollection;
		}
		
		/// <summary>
		/// Add a single id to the end of a collection in all cache layers.
		/// </summary>
		/// <param name="collectionName">The name of the collection to be updated.</param>
		/// <param name="id">The id to append to the collection.</param>
		/// <typeparam name="Model">Specifies the model type of the collection.</typeparam>
		/// <returns>The updated collection as an IEnumerable of objects.</returns>
		public async Task<IEnumerable<object>> CollectionAppend<Model>(string collectionName, object id) where Model : class {
			var collection = await GetCollection<Model>(collectionName);
			if (collection == null)
				return Array.Empty<object>();

			// Create a new collection with the appended id
			var newCollection = collection.ToImmutableArray().Add(id);
			await SetCollection<Model>(collectionName, newCollection);
			return newCollection;
		}

		/// <summary>
		/// Replaces the cached values for HasMany/HasOne type associations (not typically used)
		/// Updates all cache layers with the provided association collection.
		/// </summary>
		/// <param name="modelType">The type of model for which the association cache is being replaced.</param>
		/// <param name="propertyName">The foreign key property name that defines the association.</param>
		/// <param name="propertyValue">The foreign key property value that defines the association.</param>
		/// <param name="collection">An enumerable of ids representing the new association values.</param>
		public async Task SetCacheWhereCollection(Type modelType, string propertyName, string propertyValue, IEnumerable<object> collection) {
			IEnumerable<object>? ids;
			var enumerable = collection as object[] ?? collection.ToArray();
			if (!enumerable.Any()) {
				ids = ImmutableArray<object>.Empty;
			}
			else if (CascadeTypeUtils.IsModel(enumerable.First())) {
				ids = enumerable.Select(m => CascadeTypeUtils.GetCascadeId(m)).ToImmutableArray();
			}
			else if (CascadeTypeUtils.IsId(enumerable.First())) {
				ids = enumerable.Cast<object>().ToImmutableArray();
			}
			else
				throw new ArgumentException("collection not recognised as ids or models");

			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					// Generate a key for the association and update caches
					var key = CascadeUtils.WhereCollectionKey(modelType.Name, propertyName, propertyValue);
					await layer.StoreCollection(modelType, key, ids, NowMs);
				}
			});
		}

		/// <summary>
		/// Replace a single cached model with a new version in all caches.
		/// </summary>
		/// <param name="id">The unique identifier of the model to be replaced.</param>
		/// <param name="model">The new model object to store in the cache.</param>
		/// <param name="timeMs">Optional: specific time in milliseconds since 1970 for the caching record's timestamp.</param>
		public async Task SetCacheRecord(object id, object model, long? timeMs = null) {
			var arrivedAt = timeMs ?? NowMs;
			var modelType = model.GetType();
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.Store(modelType, id, model, arrivedAt);
				}
			});
		}
		
		/// <summary>
		/// Store data in all previous cache layers that come before the current layer where the operation was found.
		/// </summary>
		/// <param name="opResponse">The response containing operation details, results, and layer information.</param>
		private async Task StoreInPreviousCaches(OpResponse opResponse) {
			if (opResponse.LayerIndex == 0)
				return;

			await errorControl.FilterGuard(async () => {
				ICascadeCache? layerFound = null;
				var layers = CacheLayers.ToArray();

				// Determine the layer found based on the index
				if (opResponse.LayerIndex >= 0 && opResponse.LayerIndex < layers.Length)
					layerFound = layers[opResponse.LayerIndex];
				var beforeLayer = layerFound == null;

				// Iterate over cache layers in reverse order and store responses in older layers
				foreach (var layer in CacheLayers.Reverse()) {
					if (!beforeLayer && layer == layerFound)
						beforeLayer = true;
					if (!beforeLayer)
						continue;

					// Store collections or full operation response based on the request type
					if (opResponse.RequestOp.Verb == RequestVerb.GetCollection)
						await layer.StoreCollection(opResponse.RequestOp.Type, opResponse.RequestOp.Key!, opResponse.Results, opResponse.TimeMs);
					else
						await layer.Store(opResponse);
				}
			});
		}

		/// <summary>
		/// Clears all content from a specified cache layer, with the option to preserve held entries.
		/// </summary>
		/// <param name="index">The index of the cache layer to be cleared.</param>
		/// <param name="exceptHeld">If true, entries marked as held will not be cleared.</param>
		public async Task ClearLayer(int index, bool exceptHeld = true) {
			await CacheLayers.ToArray()[index].ClearAll(exceptHeld);
		}
		
		/// <summary>
		/// Clears all content from all cache layers, with options to preserve held entries and clear content based on age.
		/// </summary>
		/// <param name="exceptHeld">If true, entries marked as held will not be cleared.</param>
		/// <param name="olderThan">Optional: clear only items older than this DateTime.</param>
		public async Task ClearLayers(bool exceptHeld = true, DateTime? olderThan = null) {
			foreach (var layer in CacheLayers) {
				await layer.ClearAll(exceptHeld, olderThan);
			}
		}
	}
}

using System;
using System.Collections;
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
		/// Utility method to either replace or remove an item from a HasMany association while optionally ensuring the item is present.
		/// </summary>
		/// <param name="collectionName">the name of the collection to modify</param>
		/// <param name="item">the item - can be a string (eg. a collection name for a collection of collections) or number - doesn't have to be a valid id in the collection</param>
		/// <param name="remove">Specifies whether to remove the item from the association.</param>
		/// <param name="ensureItem">Indicates if the item should be ensured in the association, avoiding duplicates.</param>
		protected async Task<IReadOnlyList<object>> CollectionReplaceRemoveItem<Model>(string collectionName, object item, bool remove = false, bool ensureItem = false) where Model : class {
			var collection = (await GetCollection<Model>(collectionName) ?? ImmutableArray<Model>.Empty).ToImmutableArray() ;
			
			var modified = false;
			for (var i = 0; i < collection.Length; i++) {
				var existing = collection[i];
				if (EqualityComparer<object>.Default.Equals(existing,item)) {
					collection = collection.RemoveAt(i);
					if (!remove)
						collection = collection.Insert(i, item);
					modified = true;
					break;
				}
			}
			if (modified) {
				await SetCollection<Model>(collectionName, collection);
			} else if (ensureItem) {
				collection = collection.Add(item);
				await SetCollection<Model>(collectionName, collection);
			}
			return collection;
		}
		
		/// <summary>
		/// Replaces an item in the collection only if it already exists
		/// </summary>
		/// <param name="collectionName"></param>
		/// <param name="item"></param>
		public Task<IReadOnlyList<object>> CollectionReplaceItem<Model>(string collectionName, object item) where Model : class {
			return CollectionReplaceRemoveItem<Model>(collectionName, item, remove: false);
		}

		/// <summary>
		/// Removes an item from the collection if it exists
		/// </summary>
		/// <param name="collectionName"></param>
		/// <param name="item"></param>
		public Task<IReadOnlyList<object>> CollectionRemoveItem<Model>(string collectionName, object item) where Model : class {
			return CollectionReplaceRemoveItem<Model>(collectionName, item, remove: true);
		}

		/// <summary>
		/// Ensures that an item exists in the collection - by replacing or adding
		/// </summary>
		/// <param name="collectionName"></param>
		/// <param name="item"></param>
		public Task<IReadOnlyList<object>> CollectionEnsureItem<Model>(string collectionName, object item) where Model : class {
			return CollectionReplaceRemoveItem<Model>(collectionName, item, remove: false, ensureItem: true);
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
		/// Get the arrivedAt timestamp of a cached record, from the nearest cache layer holding it.
		/// </summary>
		/// <param name="id">The unique identifier of the model.</param>
		/// <typeparam name="Model">Specifies the model type of the record.</typeparam>
		/// <returns>The arrivedAt time in milliseconds since 1970, or -1 if not present in any cache layer.</returns>
		public async Task<long?> GetArrivedAt<Model>(object id) where Model : class {
			var req = RequestOp.GetOp<Model>(id, NowMs, freshnessSeconds: RequestOp.FRESHNESS_ANY);
			foreach (var layer in CacheLayers) {
				var response = await layer.Fetch(req);
				if (response.Exists && response.ArrivedAtMs != null)
					return response.ArrivedAtMs.Value;
			}
			return null;
		}

		/// <summary>
		/// Get the arrivedAt timestamp of a cached collection, from the nearest cache layer holding it.
		/// </summary>
		/// <param name="name">The name of the collection.</param>
		/// <typeparam name="Model">Specifies the model type of the collection.</typeparam>
		/// <returns>The arrivedAt time in milliseconds since 1970, or -1 if not present in any cache layer.</returns>
		public async Task<long?> GetCollectionArrivedAt<Model>(string name) where Model : class {
			var req = RequestOp.GetCollectionOp<Model>(name, NowMs);
			foreach (var layer in CacheLayers) {
				var response = await layer.Fetch(req);
				if (response.Exists && response.ArrivedAtMs != null)
					return response.ArrivedAtMs.Value;
			}
			return null;
		}

		/// <summary>
		/// Store data in all previous cache layers that come before the current layer where the operation was found.
		/// </summary>
		/// <param name="opResponse">The response containing operation details, results, and layer information.</param>
		private async Task<OpResponse> StoreInPreviousCaches(OpResponse opResponse) {
			if (opResponse.LayerIndex == 0)
				return opResponse;

			//await errorControl.FilterGuard(async () => {
				ICascadeCache? layerFound = null;
				var layers = CacheLayers.ToArray();

				// Determine the layer found based on the index
				if (opResponse.LayerIndex >= 0 && opResponse.LayerIndex < layers.Length)
					layerFound = layers[opResponse.LayerIndex];
				var beforeLayer = layerFound == null;		// came from server, so store in all layers

				// Iterate over cache layers in reverse order and store responses in older layers
				foreach (var layer in CacheLayers.Reverse()) {
					if (!beforeLayer && layer == layerFound) {	// found layer it came from
						beforeLayer = true;												// set flag for next iteration to begin storing
						continue;																	// skip this layer
					}
					if (!beforeLayer)
						continue;

					// Store collections or full operation response based on the request type
					if (opResponse.RequestOp.Verb == RequestVerb.GetCollection)
						await layer.StoreCollection(opResponse.RequestOp.Type!, opResponse.RequestOp.Key!, opResponse.Results, opResponse.TimeMs);
					else
						opResponse = await layer.Store(opResponse);
				}
			//});
			return opResponse;
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

		public async Task ClearCache(
			IEnumerable<Type>  modelTypes,
			bool clearBlobs = false,
			bool exceptHeld = true,
			DateTime? olderThan = null
		) {
			foreach (var layer in CacheLayers) {
				foreach (var modelType in modelTypes) {
					await layer.ClearByType(modelType, exceptHeld, olderThan);
				}
				if (clearBlobs)
					await layer.ClearBlobs(exceptHeld, olderThan);
			}
		}
	}
}

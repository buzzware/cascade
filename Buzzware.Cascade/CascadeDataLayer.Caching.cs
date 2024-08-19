using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Clear a collection from all caches
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		public async Task ClearCollection<Model>(string collectionName) {
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.StoreCollection(typeof(Model), collectionName, null, NowMs);
				}
			});
		}

		/// <summary>
		/// Replace a collection with a set of ids in all caches
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <param name="ids">an enumerable of ids</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		public async Task<IEnumerable<object>> SetCollection<Model>(string collectionName, IEnumerable<object> ids) where Model : class {
			var result = ids.ToImmutableArray();
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.StoreCollection(typeof(Model), collectionName, result, NowMs);
				}
			});
			return result;
		}
		
		public async Task<IEnumerable<object>> CollectionPrepend<Model>(string collectionName, object id) where Model : class {
			var collection = await GetCollection<Model>(collectionName);
			if (collection == null)
				return Array.Empty<object>();
			var newCollection = collection.ToImmutableArray().Insert(0, id);
			await SetCollection<Model>(collectionName, newCollection);
			return newCollection;
		}
		
		public async Task<IEnumerable<object>> CollectionAppend<Model>(string collectionName, object id) where Model : class {
			var collection = await GetCollection<Model>(collectionName);
			if (collection == null)
				return Array.Empty<object>();
			var newCollection = collection.ToImmutableArray().Add(id);
			await SetCollection<Model>(collectionName, newCollection);
			return newCollection;
		}


		/// <summary>
		/// Replaces the cached values for HasMany/HasOne like associations. Not normally used.
		/// </summary>
		/// <param name="modelType">type of model</param>
		/// <param name="propertyName">Name of foreign key</param>
		/// <param name="propertyValue">Value of foreign key</param>
		/// <param name="collection">enumerable of ids for the collection</param>
		/// <returns>void</returns>
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
					var key = CascadeUtils.WhereCollectionKey(modelType.Name, propertyName, propertyValue);
					await layer.StoreCollection(modelType, key, ids, NowMs);
				}
			});
		}

		/// <summary>
		/// Replaces the cached model in all caches 
		/// </summary>
		/// <param name="id">id of model to replace</param>
		/// <param name="model">model to replace with. The model type is derived from this</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		public async Task SetCacheRecord(object id, object model, long? timeMs = null) {
			var arrivedAt = timeMs ?? NowMs;
			var modelType = model.GetType();
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.Store(modelType, id, model, arrivedAt);
				}
			});
		}
		
		private async Task StoreInPreviousCaches(OpResponse opResponse) {
			if (opResponse.LayerIndex == 0)
				return;
			await errorControl.FilterGuard(async () => {
				ICascadeCache? layerFound = null;
				var layers = CacheLayers.ToArray();
				if (opResponse.LayerIndex>=0 && opResponse.LayerIndex<layers.Length)
					layerFound = layers[opResponse.LayerIndex];
				var beforeLayer = layerFound == null;
				foreach (var layer in CacheLayers.Reverse()) {
					if (!beforeLayer && layer == layerFound)
						beforeLayer = true;
					if (!beforeLayer)
						continue;
					if (opResponse.RequestOp.Verb == RequestVerb.GetCollection)
						await layer.StoreCollection(opResponse.RequestOp.Type, opResponse.RequestOp.Key!, opResponse.Results, opResponse.TimeMs);
					else
						await layer.Store(opResponse);
				}
			});
		}

		public async Task ClearLayer(int index, bool exceptHeld=true) {
			await CacheLayers.ToArray()[index].ClearAll(exceptHeld);
		}
		
		public async Task ClearLayers(bool exceptHeld = true, DateTime? olderThan = null) {
			foreach (var layer in CacheLayers) {
				await layer.ClearAll(exceptHeld,olderThan);
			}
		}
	}
}

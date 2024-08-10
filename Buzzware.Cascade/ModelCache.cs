using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {

  /// <summary>
  /// Top level cache implementing ICascadeCache. This class manages an array of IModelClassCache, one per model class.
  /// The IModelClassCache implementations passed to the constructor can use any storage mechanism, and Cascade provides a few file and memory options.
  /// A IBlobCache can also be included to manage the storing, fetching, and clearing of binary blob data. 
  /// </summary>
	public class ModelCache : ICascadeCache {
		private readonly IBlobCache? blobCache;

    /// <summary>
    /// A thread-safe dictionary storing IModelClassCache instances keyed by their corresponding model types.
    /// </summary>
		private ConcurrentDictionary<Type, IModelClassCache> classCache;
		
		private CascadeDataLayer _cascade;

    /// <summary>
    /// CascadeDataLayer instance used by the ModelCache. When set, updates the Cascade instance for all class caches and the blob cache if available.
    /// </summary>
		public CascadeDataLayer Cascade {
			get => _cascade;
			set {
				_cascade = value;
				foreach (var ts in classCache) {
					ts.Value.Cascade = _cascade;
				}
				if (blobCache != null) 
					blobCache.Cascade = _cascade;
			}
		}

    /// <summary>
    /// Clears all cached data from class caches and the blob cache if specified. Can optionally protect held data and clear only data older than a certain date.
    /// </summary>
    /// <param name="exceptHeld">Flag indicating whether to exclude held items from being cleared.</param>
    /// <param name="olderThan">If specified, only clear items older than this date/time.</param>
		public async Task ClearAll(bool exceptHeld = true, DateTime? olderThan = null) {
			foreach (var pair in classCache) {
				await pair.Value.ClearAll(exceptHeld: exceptHeld, olderThan: olderThan);
			}
			if (blobCache != null) 
				await blobCache.ClearAll(exceptHeld: exceptHeld, olderThan: olderThan);
		}
		
    /// <summary>
    /// ModelCache Constructor
    /// </summary>
    /// <param name="aClassCache">Dictionary that provides the model class caches to be managed.</param>
    /// <param name="blobCache">Optional instance of a blob cache to be managed alongside the class caches.</param>
		public ModelCache(IDictionary<Type, IModelClassCache> aClassCache, IBlobCache? blobCache = null) {
			this.blobCache = blobCache;
			this.classCache = new ConcurrentDictionary<Type, IModelClassCache>(aClassCache);
		}
		
    /// <summary>
    /// Fetches data based on the specified request operation from the enclosed caches
    /// </summary>
    /// <param name="requestOp">The operation request that specifies what data to fetch and how to do it.</param>
    /// <returns>OpResponse containing the result of the fetch operation and its metadata.</returns>
		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			OpResponse opResponse;
			if (requestOp.Verb is RequestVerb.BlobGet or RequestVerb.BlobPut) {
				if (blobCache != null) {
					opResponse = await blobCache.Fetch(requestOp);
					opResponse.SourceName = blobCache.GetType().Name;
				} else {
					opResponse = OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name);
				}
			} else {
				if (requestOp.Type is null)
					throw new Exception("Type cannot be null");
				if (!classCache.ContainsKey(requestOp.Type)) {
					Log.Debug($"ModelCache: No type store for that type - returning not found. You may wish to register a IModelClassCache for the type {requestOp.Type.Name}");
					return OpResponse.None(requestOp,Cascade.NowMs,GetType().Name);
				}
				var store = classCache[requestOp.Type];
				opResponse = await store.Fetch(requestOp);
				opResponse.SourceName = store.GetType().Name;
			}
			return opResponse;
		}

    /// <summary>
    /// Stores a model in the cache under the specified type, id, and arrival time.
    /// </summary>
    /// <param name="type">The type of the model being stored.</param>
    /// <param name="id">The id of the model.</param>
    /// <param name="model">The model instance to store.</param>
    /// <param name="arrivedAt">Timestamp representing when the data arrived from the origin</param>
		public Task Store(Type type, object id, object model, long arrivedAt) {
			if (type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(type))
				throw new Exception($"ModelCache: No type store for that type. Consider registering a IModelClassCache for the type {type.Name}");
			var store = classCache[type]!;
			return store.Store(id,model,arrivedAt);
		}

    /// <summary>
    /// Stores a collection of model IDs in the cache under the given type and key. 
    /// </summary>
    /// <param name="type">The type of the models being stored.</param>
    /// <param name="key">The key to associate with the collection of model IDs.</param>
    /// <param name="ids">The collection of model IDs to store.</param>
    /// <param name="arrivedAt">Timestamp indicating when the collection was captured, in ms since 1970</param>
		public Task StoreCollection(Type type, string key, IEnumerable ids, long arrivedAt) {
			if (type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(type))
				Log.Debug($"ModelCache: No type store for that type. Consider registering a IModelClassCache for the type {type.Name}");
			var store = classCache[type]!;
			return store.StoreCollection(key,ids,arrivedAt);
		}

    /// <summary>
    /// Stores an operation response in the appropriate cache
    /// Manages storage differently depending on whether it's a Blob operation, single model, or a query collection.
    /// </summary>
    /// <param name="opResponse">The response of an operation that provides details about what to store.</param>
		public async Task Store(OpResponse opResponse) {
			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;

      // Decide on storage action based on the verb of the request operation
			switch (opResponse.RequestOp.Verb) {
				case RequestVerb.Get:
				case RequestVerb.Update:
				case RequestVerb.Create:
				case RequestVerb.Destroy:
				case RequestVerb.Execute:
					if (opResponse.RequestOp.Type is null)
						throw new Exception("Type cannot be null");
					if (!classCache.ContainsKey(opResponse.RequestOp.Type)) {
						Log.Debug($"ModelCache: No type store for that type. Consider registering a IModelClassCache for the type {opResponse.RequestOp.Type.Name}");
						return;
					}
					var cache1 = classCache[opResponse.RequestOp.Type];
					
          // Determine model ID, and decide whether to store or remove based on the response verb and presence flag
					object? id = opResponse.RequestOp.Id ?? CascadeTypeUtils.TryGetCascadeId(opResponse.Result);
					if (id == null) {
						Log.Warning("ModelCache.Store: Unable to get valid Id - not caching this");
						return;
					}
					if (opResponse.RequestOp.Verb==RequestVerb.Destroy || !opResponse.Exists) {
						await cache1.Remove(id);
					} else {
						if (opResponse.Result is null)
							throw new Exception("When Present is true, Result cannot be null");
						await cache1.Store(id, opResponse.Result, arrivedAt);
					}
					break;
				case RequestVerb.Query:
					if (opResponse.RequestOp.Type is null)
						throw new Exception("Type cannot be null");
					if (!classCache.ContainsKey(opResponse.RequestOp.Type)) {
						Log.Debug($"ModelCache: No type store for that type. Consider registering a IModelClassCache for the type {opResponse.RequestOp.Type.Name}");
						return;
					}
					var cache2 = classCache[opResponse.RequestOp.Type];
					if (opResponse.IsModelResults) {
            // Store each individual model in the list of results
            foreach (var model in opResponse.Results)
							await cache2.Store(CascadeTypeUtils.GetCascadeId(model), model, arrivedAt);
					}
          // Store the collection of model IDs associated with the query
					await cache2.StoreCollection(opResponse.RequestOp.Key!, opResponse.ResultIds, arrivedAt);
					break;
				case RequestVerb.BlobGet:
				case RequestVerb.BlobPut:
				case RequestVerb.BlobDestroy:
					if (blobCache == null)
						return;
					await blobCache.Store(opResponse);
					break;
				default:
					break;
			}
		}
	}
}

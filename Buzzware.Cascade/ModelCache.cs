using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {
	public class ModelCache : ICascadeCache {
		private readonly IBlobCache? blobCache;
		private ConcurrentDictionary<Type, IModelClassCache> classCache;
		
		private CascadeDataLayer _cascade;
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

		public async Task ClearAll(bool exceptHeld = true, DateTime? olderThan = null) {
			foreach (var pair in classCache) {
				await pair.Value.ClearAll(exceptHeld: exceptHeld, olderThan: olderThan);
			}
			if (blobCache != null) 
				await blobCache.ClearAll(exceptHeld: exceptHeld, olderThan: olderThan);
		}
		
		public ModelCache(IDictionary<Type, IModelClassCache> aClassCache, IBlobCache? blobCache = null) {
			this.blobCache = blobCache;
			this.classCache = new ConcurrentDictionary<Type, IModelClassCache>(aClassCache);
		}
		
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

		public Task Store(Type type, object id, object model, long arrivedAt) {
			if (type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(type))
				throw new Exception($"ModelCache: No type store for that type. Consider registering a IModelClassCache for the type {type.Name}");
			var store = classCache[type]!;
			return store.Store(id,model,arrivedAt);
		}

		public Task StoreCollection(Type type, string key, IEnumerable ids, long arrivedAt) {
			if (type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(type))
				Log.Debug($"ModelCache: No type store for that type. Consider registering a IModelClassCache for the type {type.Name}");
			var store = classCache[type]!;
			return store.StoreCollection(key,ids,arrivedAt);
		}


		public async Task Store(OpResponse opResponse) {
			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;

			// if (!opResponse.Connected)
			// 	throw new Exception("Don't attempt to store responses from a disconnected store");
			
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
						// var results = opResponse.Results(); // as IEnumerable<ICascadeModel>)!;
						// var models = CascadeUtils.ConvertTo(typeof(IEnumerable<ICascadeModel>),results)! as IEnumerable<ICascadeModel>;
						foreach (var model in opResponse.Results)
							await cache2.Store(CascadeTypeUtils.GetCascadeId(model), model, arrivedAt);
					}
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

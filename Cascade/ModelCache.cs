using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Cascade {
	public class ModelCache : ICascadeCache {
		private Dictionary<Type, IModelClassCache> classCache;
		
		private CascadeDataLayer _cascade;
		public CascadeDataLayer Cascade {
			get => _cascade;
			set {
				_cascade = value;
				foreach (var ts in classCache) {
					ts.Value.Cascade = _cascade;
				}
			}
		}

		public ModelCache(Dictionary<Type, IModelClassCache> aClassCache) {
			this.classCache = aClassCache;
		}
		
		public Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(requestOp.Type))
				throw new Exception("No type store for that type");
			
			var store = classCache[requestOp.Type];
			return store.Fetch(requestOp);
		}

		public Task Store(Type type, object id, object model, long arrivedAt) {
			if (type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(type))
				throw new Exception("No type store for that type");
			var store = classCache[type]!;
			return store.Store(id,model,arrivedAt);
		}

		public Task StoreCollection(Type type, string key, IEnumerable ids, long arrivedAt) {
			if (type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(type))
				throw new Exception("No type store for that type");
			var store = classCache[type]!;
			return store.StoreCollection(key,ids,arrivedAt);
		}


		public async Task Store(OpResponse opResponse) {
			if (opResponse.RequestOp.Type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(opResponse.RequestOp.Type))
				throw new Exception("No type store for that type");
			if (!opResponse.Connected)
				throw new Exception("Don't attempt to store responses from a disconnected store");

			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;
			
			var cache = classCache[opResponse.RequestOp.Type];

			switch (opResponse.RequestOp.Verb) {
				case RequestVerb.Get:
				case RequestVerb.Update:
				case RequestVerb.Create:
				case RequestVerb.Destroy:
					//IdType id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), opResponse.RequestOp.Id);
					var id = opResponse.RequestOp.Id;
					if (id == null)
						throw new Exception("Unable to get right value for Id");
					if (!opResponse.Exists) {
						await cache.Remove(id);
					} else {
						if (opResponse.Result is null)
							throw new Exception("When Present is true, Result cannot be null");
						//Model model = (opResponse.Result as Model)!;
						await cache.Store(id, opResponse.Result, arrivedAt);
					}
					break;
				case RequestVerb.Query:
					if (opResponse.IsModelResults) {
						// var results = opResponse.Results(); // as IEnumerable<ICascadeModel>)!;
						// var models = CascadeUtils.ConvertTo(typeof(IEnumerable<ICascadeModel>),results)! as IEnumerable<ICascadeModel>;
						foreach (var model in opResponse.Results)
							await cache.Store(CascadeTypeUtils.GetCascadeId(model), model, arrivedAt);
					}
					await cache.StoreCollection(opResponse.RequestOp.Key!, opResponse.ResultIds, arrivedAt);
					break;
				default:
					break;
			}
		}
		
		public async Task Clear() {
			foreach (var kv in classCache) {
				await kv.Value.Clear();
			}
		}
	}
}

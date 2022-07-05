using System;
using System.Collections.Generic;
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
			var store = classCache[type];
			return store.Store(id,model,arrivedAt);
		}


		public async Task Store(OpResponse opResponse) {
			if (opResponse.RequestOp.Type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(opResponse.RequestOp.Type))
				throw new Exception("No type store for that type");
			if (!opResponse.Connected)
				throw new Exception("Don't attempt to store responses from a disconnected store");

			//IdType id = (IdType) CascadeUtils.ConvertTo(typeof(IdType), opResponse.RequestOp.Id);
			var id = opResponse.RequestOp.Id;
			if (id == null)
				throw new Exception("Unable to get right value for Id");
			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;
			
			var cache = classCache[opResponse.RequestOp.Type];
			
			if (!opResponse.Exists) {
				await cache.Remove(id);
			} else {
				if (opResponse.Result is null)
					throw new Exception("When Present is true, Result cannot be null");
				//Model model = (opResponse.Result as Model)!;
				await cache.Store(id, opResponse.Result, arrivedAt);
			}
		}
		
		public async Task Clear() {
			foreach (var kv in classCache) {
				await kv.Value.Clear();
			}
		}
	}
}

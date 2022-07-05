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


		public Task Store(OpResponse opResponse) {
			if (opResponse.RequestOp.Type is null)
				throw new Exception("Type cannot be null");
			if (!classCache.ContainsKey(opResponse.RequestOp.Type))
				throw new Exception("No type store for that type");
			var store = classCache[opResponse.RequestOp.Type];
			return store.Store(opResponse);
		}

		public async Task Clear() {
			foreach (var kv in classCache) {
				kv.Value.Clear();
			}
		}
	}
}

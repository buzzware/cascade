using System;
using System.Collections;
using System.Threading.Tasks;

namespace Cascade {
	public interface ICascadeCache {
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Store(OpResponse opResponse);	// must store either get model or query ids
		Task Store(Type type, object id, object model, long arrivedAt);
		Task StoreCollection(Type type, string key, IEnumerable? ids, long arrivedAt);
		CascadeDataLayer Cascade { get; set; }
		Task ClearAll(bool exceptHeld=true);
	}
}

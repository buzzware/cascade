using System;
using System.Collections;
using System.Threading.Tasks;

namespace Buzzware.Cascade {
	public interface IModelClassCache {
		CascadeDataLayer Cascade { get; set; }
		Task Store(object id, object model, long arrivedAt);
		Task StoreCollection(string key, IEnumerable ids, long aArrivedAt);
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Remove(object id);
		Task ClearAll(bool exceptHeld, DateTime? olderThan = null);
	}
}

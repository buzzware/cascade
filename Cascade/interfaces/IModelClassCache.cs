using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cascade {
	public interface IModelClassCache {
		CascadeDataLayer Cascade { get; set; }
		Task Store(object id, object model, long arrivedAt);
		Task StoreCollection(string key, object[] ids, long aArrivedAt);
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Remove(object id);
		Task Clear();
	}
}

using System.Threading.Tasks;

namespace Cascade {
	public interface IModelClassCache {
		CascadeDataLayer Cascade { get; set; }
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Store(OpResponse opResponse);

		Task Store(object id, object model, long arrivedAt);
		Task Remove(object id);
		Task Clear();
	}
}

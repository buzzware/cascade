using System;
using System.Threading.Tasks;

namespace Cascade {
	public interface ICascadeCache {
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Store(OpResponse opResponse);
		Task Store(Type type, object id, object model, long arrivedAt);
		CascadeDataLayer Cascade { get; set; }
	}
}

using System;
using System.Threading.Tasks;

namespace Buzzware.Cascade {
	public interface IBlobCache {
		CascadeDataLayer Cascade { get; set; }
		Task ClearAll(bool exceptHeld, DateTime? olderThan);
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Store(OpResponse opResponse);
	}
}

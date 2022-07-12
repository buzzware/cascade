using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Cascade {
	public interface ICascadeCache {
		Task<OpResponse> Fetch(RequestOp requestOp);
		Task Store(OpResponse opResponse);	// must store either get model or query ids
		Task Store(Type type, object id, object model, long arrivedAt);
		Task StoreCollection(Type type, string key, ImmutableArray<object> ids, long arrivedAt);
		CascadeDataLayer Cascade { get; set; }
	}
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cascade;

namespace Cascade.testing {
	public class MockOrigin2 : MockOrigin {

		public int RequestCount { get; protected set; }

		private readonly Dictionary<Type,IModelClassOrigin> classOrigins;
		// private Func<MockOrigin,RequestOp,Task<OpResponse>>? HandleRequest;

		public MockOrigin2(Dictionary<Type, IModelClassOrigin> classOrigins, long nowMs = 1000)  {
			NowMs = nowMs;
			this.classOrigins = classOrigins;
		}
		
		public override async Task<OpResponse> ProcessRequest(RequestOp request) {

			RequestCount += 1;
			
			object? result = null;
			
			var co = classOrigins[request.Type];
			
			switch (request.Verb) {
				case RequestVerb.Query:
					result = await co.Query(request.Criteria,request.Key!);
					break;
				case RequestVerb.Get: 
					result = await co.Get(request.Id);
					break;
				default:
					throw new NotImplementedException();
			}

			return new OpResponse(
				request,
				NowMs,
				true,
				true,
				NowMs,
				result
			);
		}


		// public CascadeDataLayer Cascade { get; set; } 
		//
		// public long NowMs { get; set; }
		//
		// public long IncNowMs(long incMs=1000) {
		// 	return NowMs += incMs;
		// }
		//
		// public Task<OpResponse> ProcessRequest(RequestOp request) {
		// 	if (HandleRequest != null)
		// 		return HandleRequest(this,request);
		// 	throw new NotImplementedException("Attach HandleRequest or override this");
		// }
	}
}

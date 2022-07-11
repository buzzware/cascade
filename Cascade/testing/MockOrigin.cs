using System;
using System.Threading.Tasks;

namespace Cascade.testing {
	public class MockOrigin : ICascadeOrigin {
		private Func<MockOrigin,RequestOp,Task<OpResponse>>? HandleRequest;

		public MockOrigin(long nowMs = 1000, Func<MockOrigin,RequestOp,Task<OpResponse>>? handleRequest = null) {
			NowMs = nowMs;
			HandleRequest = handleRequest;
		}

		public CascadeDataLayer Cascade { get; set; } 
		
		public long NowMs { get; set; }

		public long IncNowMs(long incMs=1000) {
			return NowMs += incMs;
		}

		public virtual Task<OpResponse> ProcessRequest(RequestOp request) {
			if (HandleRequest != null)
				return HandleRequest(this,request);
			throw new NotImplementedException("Attach HandleRequest or override this");
		}
	}
}

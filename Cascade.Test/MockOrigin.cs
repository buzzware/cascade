using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cascade.Test {
	public class MockOrigin : ICascadeOrigin {
		private Func<MockOrigin,RequestOp,Task<OpResponse>>? HandleRequest;

		public MockOrigin(long nowMs = 1000, Func<MockOrigin,RequestOp,Task<OpResponse>>? handleRequest = null) {
			NowMs = nowMs;
			HandleRequest = handleRequest;
		}

		public CascadeDataLayer Cascade { get; set; } 
		
		public long NowMs { get; set; }
		public async Task EnsureAuthenticated(Type? type) {
		}

		public virtual Type LookupModelType(string typeName) {
			if (typeName == typeof(Thing).FullName)
				return typeof(Thing);
			else if (typeName == typeof(Parent).FullName)
				return typeof(Parent);
			else if (typeName == typeof(Child).FullName)
				return typeof(Child);
			else
				throw new TypeLoadException($"Type {typeName} not found in origin");
		}

		public string NewGuid() {
			return Guid.NewGuid().ToString();
		}

		public IEnumerable<Type> ListModelTypes() {
			return new[] { typeof(Thing), typeof(Parent), typeof(Child) };
		}

		public long IncNowMs(long incMs=1000) {
			return NowMs += incMs;
		}

		public virtual Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline) {
			if (HandleRequest != null)
				return HandleRequest(this,request);
			throw new NotImplementedException("Attach HandleRequest or override this");
		}
	}
}

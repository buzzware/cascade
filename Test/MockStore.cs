using System;
using System.Threading.Tasks;
using Cascade;

namespace Test {
	public class MockStore : ICascadeStore {
		public Func<RequestOp, Task<OpResponse>> handleOp;

		public MockStore(bool origin, bool local) {
			Origin = origin;
			Local = local;
		}

		public bool Local { get; }
		public bool Origin { get; }
				
		public Task<OpResponse> Create(RequestOp aRequestOp) {
			return handleOp != null ? handleOp(aRequestOp) : null;
		}

		public Task<OpResponse> Read(RequestOp aRequestOp) {
			return handleOp != null ? handleOp(aRequestOp) : null;
		}

		public Task<OpResponse> ReadAll(RequestOp aRequestOp) {
			return handleOp != null ? handleOp(aRequestOp) : null;
		}

		public Task<OpResponse> Update(RequestOp aRequestOp) {
			return handleOp != null ? handleOp(aRequestOp) : null;
		}

		public Task<OpResponse> Destroy(RequestOp aRequestOp) {
			return handleOp != null ? handleOp(aRequestOp) : null;
		}

		public Task<OpResponse> Execute(RequestOp aRequestOp) {
			throw new NotImplementedException();
		}

		public void Replace(ICascadeModel aModel) {
			throw new NotImplementedException();
		}

		public void KeySet(string aKey, object aValue) {
			throw new NotImplementedException();
		}

		public object KeyGet(string aKey, object aDefault = null) {
			throw new NotImplementedException();
		}
	}
}
// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Buzzware.Cascade;
//
// namespace Test {
// 	public class MockStore : ICascadeStore {
// 		
// 		public CascadeDataLayer Buzzware.Cascade { get; }
// 		
// 		public Func<MockStore,RequestOp,Task<OpResponse>> handleOp;
//
// 		public MockStore(CascadeDataLayer cascade, bool origin, bool local) {
// 			Buzzware.Cascade = cascade;
// 			Origin = origin;
// 			Local = local;
// 		}
//
// 		public bool Local { get; }
// 		public bool Origin { get; }
// 		public Dictionary<string,object> Models = new Dictionary<string, object>();
// 		public Dictionary<string,string> JsonStore = new Dictionary<string, string>();
// 				
// 		public Task<OpResponse> Create(RequestOp aRequestOp) {
// 			return handleOp != null ? handleOp(this,aRequestOp) : null;
// 		}
//
// 		public Task<OpResponse> Read(RequestOp aRequestOp) {
// 			return handleOp != null ? handleOp(this,aRequestOp) : null;
// 		}
//
// 		public Task<OpResponse> ReadAll(RequestOp aRequestOp) {
// 			return handleOp != null ? handleOp(this,aRequestOp) : null;
// 		}
//
// 		public Task<OpResponse> Update(RequestOp aRequestOp) {
// 			return handleOp != null ? handleOp(this,aRequestOp) : null;
// 		}
//
// 		public Task<OpResponse> Destroy(RequestOp aRequestOp) {
// 			return handleOp != null ? handleOp(this,aRequestOp) : null;
// 		}
//
// 		public Task<OpResponse> Execute(RequestOp aRequestOp) {
// 			throw new NotImplementedException();
// 		}
//
// 		public void Replace(ICascadeModel aModel) {
// 			Models[Buzzware.Cascade.GetKeyFrom(aModel)] = aModel;
// 		}
//
// 		public void KeySet(string aKey, string aValue) {
// 			JsonStore[aKey] = aValue;
// 		}
//
// 		public object KeyGet(string aKey, string aDefault = null) {
// 			return JsonStore.ContainsKey(aKey) ? JsonStore[aKey] : aDefault;
// 		}
// 	}
// }

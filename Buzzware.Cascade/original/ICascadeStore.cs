// using System.Collections.Generic;
// using System.Threading.Tasks;
//
// namespace Buzzware.Cascade {
//
// 	public interface ICascadeStore {
// 		
// 		CascadeDataLayer Buzzware.Cascade { get; }
// 		
// 		bool Local { get; }
// 		bool Origin { get; }
// 		
// 		Task<OpResponse> Create(RequestOp aRequestOp);		
// 		Task<OpResponse> Read(RequestOp aRequestOp);
// 		Task<OpResponse> ReadAll(RequestOp aRequestOp);
// 		Task<OpResponse> Update(RequestOp aRequestOp);
// 		Task<OpResponse> Destroy(RequestOp aRequestOp);
// 		Task<OpResponse> Execute(RequestOp aRequestOp);
// 		
// 		void Replace(ICascadeModel aModel);
// 		void KeySet(string aKey, object aValue);
// 		object KeyGet(string aKey, object aDefault = null);				
// 	}
// }

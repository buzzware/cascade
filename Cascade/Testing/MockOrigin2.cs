using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StandardExceptions;

namespace Cascade.Testing {
	public class MockOrigin2 : MockOrigin, ICascadeOrigin {

		public int RequestCount { get; protected set; }

		private readonly Dictionary<Type,IModelClassOrigin> classOrigins;

		public bool ActLikeOffline { get; set; }

		public MockOrigin2(Dictionary<Type, IModelClassOrigin> classOrigins, long nowMs = 1000)  {
			NowMs = nowMs;
			this.classOrigins = classOrigins;
			foreach (var pair in classOrigins) {
				pair.Value.Origin = this;
			}
		}
		
		public override async Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline) {

			RequestCount += 1;
			
			object? result = null;
			
			var co = classOrigins[request.Type];
			if (ActLikeOffline)
				throw new NoNetworkException();
			
			switch (request.Verb) {
				case RequestVerb.Query:
					result = await co.Query(request.Criteria);
					break;
				case RequestVerb.Get: 
					result = await co.Get(request.Id);
					break;
				case RequestVerb.Create: 
					result = await co.Create(request.Value!);
					break;
				case RequestVerb.Update:
					if (request != null)
						result = await co.Update(
							request.Id,
							((IDictionary<string, object>)request.Value)!,
							request.Extra
						);
					break;
				case RequestVerb.Replace: 
					result = await co.Replace(request.Value!);
					break;
				case RequestVerb.Destroy: 
					await co.Destroy(request.Value!);
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

		public override Type LookupModelType(string typeName) {
			foreach (var co in classOrigins) {
				if (co.Key.FullName == typeName)
					return co.Key;
			}
			throw new TypeLoadException($"Type {typeName} not found in origin");
		}

		public async Task<M?> Get<M>(object id) where M : SuperModel {
			var co = classOrigins[typeof(M)] as MockModelClassOrigin<M>;
			var model = (await co?.Get(id)) as M;
			return model;
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

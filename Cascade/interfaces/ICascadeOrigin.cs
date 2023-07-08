using System;
using System.Threading.Tasks;

namespace Cascade {
	public interface ICascadeOrigin {
		Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline);
		
		CascadeDataLayer Cascade { get; set; }
		long NowMs { get; }
		Task EnsureAuthenticated();
		Type LookupModelType(string typeName);
		string NewGuid();
	}
}

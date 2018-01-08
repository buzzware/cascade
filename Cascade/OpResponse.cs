using System.Collections.Generic;

namespace Cascade {
	public class OpResponse {
		
		public bool Present = false;
		public bool Connected = false;
				
		public int Index;		
		public IDictionary<string, object> Results;		// we store results as object internally
		public string ResultKey;

		public object Error;
		
		public object ResultObject {
			get {
				if (Results != null && ResultKey != null && Results.ContainsKey(ResultKey))
					return Results[ResultKey];
				else
					return null;
			}
		}
		
		// generic type M is only used for vanity properties/methods
//		public M Result {
//			get {
//				if (Results != null && ResultKey != null && Results.ContainsKey(ResultKey))
//					return Results[ResultKey] as M;
//				else
//					return default(M);
//			}
//		}		
	}
}
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Cascade {
	public class OpResponse {
		
		[ImmutableObject(true)]
		public OpResponse(
			RequestOp requestOp,
			long timeMs,
			bool connected,
			bool exists,
			long? arrivedAtMs,
			object? result
		) {
			RequestOp = requestOp;
			TimeMs = timeMs;
			Connected = connected;
			Exists = exists;
			ArrivedAtMs = arrivedAtMs;
			Result = result;
		}

		public readonly RequestOp RequestOp;
		public readonly long TimeMs;
		public readonly bool Connected;	// layer available ?
		public readonly bool Exists;		// present in cache? Result can be null if known to not exist on origin
		public readonly object? Result;								// for create, read, update
		public long? ArrivedAtMs;

		public bool PresentAndFresh() => 
			Connected==true && Exists == true && (TimeMs-ArrivedAtMs) <= RequestOp.FreshnessSeconds*1000;
		// 		
		// public int Index;		
		// public IDictionary<string, object> Results;		// we store results as object internally
		// public string ResultKey;
		//
		// public object Error;
		// public bool FromOrigin;	// was this data from the Origin store ? 


		// public object ResultObject {
		// 	get {
		// 		if (Results != null && ResultKey != null && Results.ContainsKey(ResultKey))
		// 			return Results[ResultKey];
		// 		else
		// 			return null;
		// 	}
		// }
		//
		// // helper function
		// public void SetResult(string aResultKey, object aValue) {
		// 	if (Results==null)
		// 		Results = new Dictionary<string, object>();
		// 	ResultKey = aResultKey;
		// 	Results[ResultKey] = aValue;
		// }

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

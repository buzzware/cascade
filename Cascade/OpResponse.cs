using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

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

		public OpResponse withChanges(
			RequestOp? requestOp = null,
			long? timeMs = null,
			bool? connected = null,
			bool? exists = null,
			long? arrivedAtMs = null,
			object? result = null
		) {
			return new OpResponse(
				requestOp: requestOp ?? this.RequestOp,
				timeMs: timeMs ?? this.TimeMs,
				connected: connected ?? this.Connected,
				exists: exists ?? this.Exists,
				arrivedAtMs: arrivedAtMs ?? this.ArrivedAtMs,
				result: result ?? this.Result
			);
		}
		
		public readonly RequestOp RequestOp;
		public readonly long TimeMs;
		public readonly bool Connected;	// layer available ?
		public readonly bool Exists;		// present in cache? Result can be null if known to not exist on origin
		public readonly object? Result;								// for create, read, update
		public long? ArrivedAtMs;

		public bool PresentAndFresh() => 
			 Connected==true && Exists == true && RequestOp.FreshnessSeconds>0 && (TimeMs-ArrivedAtMs) <= RequestOp.FreshnessSeconds*1000;

		public bool ResultIsEmpty() {
			if (Result == null)
				return true;
			IEnumerable<object>? inumerableObj = Result as IEnumerable<object>;
			if (inumerableObj != null)
				return !inumerableObj.GetEnumerator().MoveNext();
			IEnumerable? inumerable = Result as IEnumerable;
			if (inumerable != null)
				return !inumerable.GetEnumerator().MoveNext();
			object[]? objects = Result as object[];
			if (objects != null)
				return objects.Length == 0;
			ICollection? icollection = Result as ICollection;
			if (icollection != null)
				return !icollection.GetEnumerator().MoveNext();
			return false;				// there is something there that we can't identify
		}

		public IEnumerable Results {
			get {
				if (Result == null)
					return ImmutableArray<object>.Empty;
				IEnumerable<object> enumerable = Result is IEnumerable<object> ? ((IEnumerable<object>)Result).ToImmutableArray() : ImmutableArray.Create(Result);
				return enumerable;
			}
		}

		public object? FirstResult => (Result as IEnumerable)?.Cast<object>().FirstOrDefault(); 

		public bool IsModelResults => CascadeTypeUtils.IsModel(FirstResult); 

		public bool IsIdResults => CascadeTypeUtils.IsId(FirstResult);

		public IEnumerable ResultIds {
			get {
				var results = Results.Cast<object>();
				if (!results.Any())
					return results;
				var first = results.FirstOrDefault();
				if (CascadeTypeUtils.IsId(first))
					return results;
				else
					return results.Select(CascadeTypeUtils.GetCascadeId).ToImmutableArray();
			}
		}

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

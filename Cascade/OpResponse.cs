using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

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
		public int LayerIndex;

		public string? SourceName;
		
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
				if (CascadeTypeUtils.IsEnumerableType(Result.GetType())) {
					//IEnumerable<object> objects = (IEnumerable<object>)Result;
					return (IEnumerable)CascadeTypeUtils.ImmutableArrayOfType(typeof(object), (IEnumerable) Result);
				} else {
					return ImmutableArray.Create(Result);
				}

				//return ImmutableArray.CreateRange<object>((IEnumerable) Result);

				// if (Result is IEnumerable<object>)
				// 	return ((IEnumerable<object>)Result).ToImmutableArray();
				// else
				// 	return ImmutableArray.CreateRange<object>((IEnumerable) Result);
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
					return results.Select(i => i!=null ? CascadeTypeUtils.GetCascadeId(i) : null).ToImmutableArray();
			}
		}

		// !!! Be aware that JsonSerializer.Serialize(Result) will throw an UnsupportedException in production (not development) if Criteria is an ImmutableDictionary
		public string ToSummaryString() {
			string? result = null;

			try {
				result = Result==null ? null : JsonSerializer.Serialize(Result);
			}
			catch (Exception e) {
				// swallow errors
			}
			return $"{result} Connected:{Connected} Exists:{Exists}";
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
		public static OpResponse None(RequestOp requestOp,long timeMs,string? sourceName = null) {
			var opResponse = new OpResponse(
				requestOp,
				timeMs,
				connected: true,
				exists: false,
				result: null,
				arrivedAtMs: null
			);
			opResponse.SourceName = sourceName;
			return opResponse;
		}
		
		public static OpResponse ConnectionFailure(RequestOp requestOp,long timeMs,string sourceName = null) {
			var opResponse = new OpResponse(
				requestOp,
				timeMs,
				connected: false,
				exists: false,
				result: null,
				arrivedAtMs: null
			);
			opResponse.SourceName = sourceName;
			return opResponse;
		}
	}
}

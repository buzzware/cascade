using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;

namespace Buzzware.Cascade {
	
	public enum RequestVerb {
		None,
		Create,
		Get,
		Update,
		Replace,
		Destroy,
		Query,
		Execute,
		GetCollection,
		BlobGet,
		BlobPut,
		BlobDestroy
	};
	
	public class RequestOp {
		public const int FRESHNESS_DEFAULT = 5*60;
		public const int FRESHNESS_ANY = int.MaxValue;
		public const int FRESHNESS_FRESHEST = 0;
		public const int FRESHNESS_INSIST = -1;

		public static RequestOp GetOp<Model>(object id,
			long timeMs = -1,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool? hold = null
		) {
			return new RequestOp(
				timeMs==-1 ? CascadeUtils.NowMs : timeMs,
				typeof(Model),
				RequestVerb.Get,
				id,
				populate: populate,
				freshnessSeconds: freshnessSeconds ?? FRESHNESS_DEFAULT,
				populateFreshnessSeconds: populateFreshnessSeconds ?? FRESHNESS_DEFAULT,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? FRESHNESS_ANY,
				hold: hold
			);
		}

		public static RequestOp GetOp(
			Type modelType,
			object id,
			long timeMs = -1,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool? hold = null
		) {
			return new RequestOp(
				timeMs==-1 ? CascadeUtils.NowMs : timeMs,
				modelType,
				RequestVerb.Get,
				id,
				populate: populate,
				freshnessSeconds: freshnessSeconds ?? FRESHNESS_DEFAULT,
				populateFreshnessSeconds: populateFreshnessSeconds ?? FRESHNESS_DEFAULT,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? FRESHNESS_ANY,
				hold: hold
			);
		}
		
		public static RequestOp BlobGetOp(
			string path,
			long timeMs = -1,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null, 
			bool? hold = null
		) {
			return new RequestOp(
				timeMs==-1 ? CascadeUtils.NowMs : timeMs,
				typeof(IReadOnlyList<byte>),
				RequestVerb.BlobGet,
				path,
				freshnessSeconds: freshnessSeconds ?? FRESHNESS_DEFAULT,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? FRESHNESS_ANY,
				hold: hold
			);
		}
		
		public static RequestOp BlobPutOp(
			string path,
			long timeMs,
			IReadOnlyList<byte> data
		) {
			return new RequestOp(
				timeMs,
				typeof(IReadOnlyList<byte>),
				RequestVerb.BlobPut,
				path,
				value: data
			);
		}

		public static RequestOp BlobDestroyOp(
			string path, 
			long timeMs
		) {
			return new RequestOp(
				timeMs,
				typeof(IReadOnlyList<byte>),
				RequestVerb.BlobDestroy,
				path
			);
		}
		
		public static RequestOp GetCollectionOp<Model>(
			string collectionName, 
			long timeMs = -1
		) {
			return new RequestOp(
				timeMs==-1 ? CascadeUtils.NowMs : timeMs,
				typeof(Model),
				RequestVerb.GetCollection,
				null,
				value: null,
				populate: null,
				freshnessSeconds: null,
				populateFreshnessSeconds: null,
				fallbackFreshnessSeconds: null,
				criteria: null,
				key: collectionName
			);
		}
		
		public static RequestOp QueryOp<Model>(string collectionName,
			object criteria,
			long timeMs,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Query,
				null,
				populate: populate,
				freshnessSeconds: freshnessSeconds ?? FRESHNESS_DEFAULT,
				populateFreshnessSeconds: populateFreshnessSeconds ?? FRESHNESS_DEFAULT,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? FRESHNESS_ANY,
				hold: hold,
				criteria: criteria, 
				key: collectionName
			);
		}

		public static RequestOp CreateOp(
			object model,
			long timeMs,
			bool hold = false
		) {
			return new RequestOp(
				timeMs,
				model.GetType(),
				RequestVerb.Create,
				CascadeTypeUtils.GetCascadeId(model),
				value: model,
				hold: hold
			);
		}
		
		public static RequestOp DestroyOp<Model>(Model model, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Destroy,
				CascadeTypeUtils.GetCascadeId(model),
				value: model
			);
		}
		
		public static RequestOp ReplaceOp<Model>(Model model, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Replace,
				CascadeTypeUtils.GetCascadeId(model),
				value: model
			);
		}
		
		public static RequestOp UpdateOp<Model>(Model model, IDictionary<string, object> changes, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Update,
				CascadeTypeUtils.GetCascadeId(model),
				value: changes,
				extra: model
			);
		}
		
		public static RequestOp ExecuteOp<OriginClass,ReturnType>(string action, IDictionary<string, object> parameters, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(OriginClass),
				RequestVerb.Execute,
				null,
				value: action,
				criteria: parameters
			);
		}
		
		public RequestOp(
			long timeMs,
			Type type,
			RequestVerb verb,
			object? id,
			object? value = null,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			object? criteria = null,
			string? key = null,
			object? extra = null
		) {
			TimeMs = timeMs;
			Type = type;
			Verb = verb;
			Id = id;
			Value = value;
			Populate = populate;
			FreshnessSeconds = freshnessSeconds ?? FRESHNESS_DEFAULT;
			PopulateFreshnessSeconds = populateFreshnessSeconds ?? FRESHNESS_DEFAULT;
			FallbackFreshnessSeconds = fallbackFreshnessSeconds ?? FRESHNESS_ANY;
			Hold = hold ?? false;
			Criteria = criteria;
			Key = key;
			Extra = extra;
		}
		
		public RequestOp CloneWith(
			long? timeMs = null,
			Type? type = null,
			RequestVerb? verb = null,
			object? id = null,
			object? value = null,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			object? criteria = null,
			string? key = null,
			object? extra = null
		) {
			return new RequestOp(
				timeMs ?? this.TimeMs,
				type ?? this.Type,
				verb ?? this.Verb,
				id ?? this.Id,
				value: value ?? this.Value,
				populate: populate ?? this.Populate,
				freshnessSeconds: freshnessSeconds ?? this.FreshnessSeconds,
				populateFreshnessSeconds: populateFreshnessSeconds ?? this.PopulateFreshnessSeconds,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? this.FallbackFreshnessSeconds,
				hold: hold ?? this.Hold,
				criteria: criteria ?? this.Criteria,
				key: key ?? this.Key,
				extra: extra ?? this.Extra
			);
		}

		public static bool IsWriteVerb(RequestVerb aVerb) {
			return aVerb == RequestVerb.Create ||
			       aVerb == RequestVerb.Update ||
			       aVerb == RequestVerb.Execute;
		}

		public static RequestVerb VerbFromString(string aString) {
			RequestVerb verb;
			return RequestVerb.TryParse(aString, true, out verb) ? verb : RequestVerb.None;
		}

		public readonly long TimeMs;
		public readonly Type Type;
		public readonly RequestVerb Verb;		// what we are doing
		public readonly object? Id;			// eg. 34
		public readonly object? Value;
		public readonly string? Key;		// eg. Products or Products__34
		public readonly object? Criteria;
		public readonly object? Extra;
		
		public int? IdAsInt {
			get {
				if (Id == null)
					return null;
				if ((Id is int) || (Id is long))
					return (int)Id;
				return null;
			}
		}

		public long? IdAsLong {
			get {
				if (Id == null)
					return null;
				if ((Id is int) || (Id is long))
					return (long)Id;
				return null;
			}
		}

		public String? IdAsString {
			get {
				if (Id == null)
					return null;
				if ((Id is int) || (Id is long))
					return ((long)Id).ToString();
				return null;
			}
		}


		// only one of Key or Id would normally be used

		public readonly IEnumerable<string>? Populate;
		
		public readonly int? FreshnessSeconds = FRESHNESS_DEFAULT;
		public readonly int? PopulateFreshnessSeconds = FRESHNESS_DEFAULT;
		public readonly int? FallbackFreshnessSeconds = FRESHNESS_ANY;
		public readonly bool Hold;
		
		public readonly IDictionary<string, string> Params;	// app specific paramters for the request


		// !!! Be aware that JsonSerializer.Serialize(Criteria) will throw an UnsupportedException in production (not development) if Criteria is an ImmutableDictionary
		public string ToSummaryString() {
			var criteria = JsonSerializer.Serialize(Criteria);
			return $"{Verb} Id:{Id} Type:{Type} Key:{Key} Criteria:{criteria} Freshness: {FreshnessSeconds}";
		}

	}
}

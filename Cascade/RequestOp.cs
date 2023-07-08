using System;
using System.Collections.Generic;
using System.Management.Instrumentation;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade {
	
	public enum RequestVerb {
		None,
		Create,
		Get,
		Update,
		Replace,
		Destroy,
		Query,
		Execute,
		GetCollection
	};
	
	public class RequestOp {
		public const int FRESHNESS_DEFAULT = 5*60;

		public static RequestOp GetOp<Model>(object id,
			long timeMs = -1,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null, 
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
				hold: hold
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
			bool? hold = null
		) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Query,
				null,
				populate: populate,
				freshnessSeconds: freshnessSeconds ?? FRESHNESS_DEFAULT,
				populateFreshnessSeconds: populateFreshnessSeconds ?? FRESHNESS_DEFAULT,
				hold: hold,
				criteria: criteria, 
				key: collectionName
			);
		}

		public static RequestOp CreateOp(
			object model,
			long timeMs,
			IEnumerable<string>? populate = null, 
			bool hold = false
		) {
			return new RequestOp(
				timeMs,
				model.GetType(),
				RequestVerb.Create,
				CascadeTypeUtils.GetCascadeId(model),
				value: model,
				populate: populate,
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
		public readonly bool Hold;
		
		public readonly IDictionary<string, string> Params;	// app specific paramters for the request

		public string ToSummaryString() {
			var criteria = JsonSerializer.Serialize(Criteria);
			return $"{Verb} Id:{Id} Type:{Type} Key:{Key} Criteria:{criteria} Freshness: {FreshnessSeconds}";
		}
	}
}

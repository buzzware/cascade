using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cascade {
	
	public enum RequestVerb {
		None,
		Create,
		Get,
		Update,
		Replace,
		Destroy,
		Query,
		Execute
	};
	
	public class RequestOp {
		public const int FRESHNESS_DEFAULT = 5*60;

		public static RequestOp GetOp<Model>(
			object id,
			long timeMs = -1,
			IEnumerable<string>? populate = null,
			int freshnessSeconds = 0
		) {
			return new RequestOp(
				timeMs==-1 ? CascadeUtils.NowMs : timeMs,
				typeof(Model),
				RequestVerb.Get,
				id,
				populate: populate,
				freshnessSeconds: freshnessSeconds
			);
		}
		
		public static RequestOp QueryOp<Model>(
			string collectionName, 
			object criteria, 
			long timeMs, 
			IEnumerable<string>? populate = null,
			int freshnessSeconds = 0
		) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Query,
				null,
				populate: populate,
				freshnessSeconds: freshnessSeconds, 
				criteria: criteria, 
				key: CascadeUtils.CollectionKeyFromName(typeof(Model).Name,collectionName)
			);
		}

		public static RequestOp CreateOp(object model, long timeMs) {
			return new RequestOp(
				timeMs,
				model.GetType(),
				RequestVerb.Create,
				CascadeTypeUtils.GetCascadeId(model),
				value: model
			);
		}
		
		public RequestOp(long timeMs,
			Type type,
			RequestVerb verb,
			object? id,
			object? value = null,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			object? criteria = null,
			string? key = null
		) {
			TimeMs = timeMs;
			Type = type;
			Verb = verb;
			Id = id;
			Value = value;
			Populate = populate;
			FreshnessSeconds = freshnessSeconds;
			Criteria = criteria;
			Key = key;
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
		public readonly object Id;			// eg. 34
		public readonly object Value;
		public readonly string? Key;		// eg. Products or Products__34
		public object Criteria { get; set; }
		
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

		public readonly IDictionary<string, string> Params;	// app specific paramters for the request

	}

}

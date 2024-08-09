using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;

namespace Buzzware.Cascade {
	
	/// <summary>
	/// Enum representing various possible verbs for a request operation, such as Create, Get, Update, etc.
	/// </summary>
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
	
	/// <summary>
	/// Represents a request operation in the Cascade library. It encapsulates all necessary details such as the type, verb, id, value, etc.
	/// </summary>
	public class RequestOp {
		public const int FRESHNESS_DEFAULT = 5*60;
		public const int FRESHNESS_ANY = int.MaxValue;
		public const int FRESHNESS_FRESHEST = 30;		// allowing for the period of the request. When arrivedAfter is supported, set this back to 0 
		public const int FRESHNESS_INSIST = -1;
		public const int FALLBACK_NEVER = -1;

		/// <summary>
		/// Constructs a "Get" operation for a specific model type, with optional parameters for population and freshness.
		/// </summary>
		/// <typeparam name="Model">The type of the model to be retrieved.</typeparam>
		/// <param name="id">The identifier for the model to be retrieved.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created. If not specified, the current time is used.</param>
		/// <param name="populate">Collection of string relations to be populated along with the model.</param>
		/// <param name="freshnessSeconds">Indicates how fresh the data should be. Defaults to FRESHNESS_DEFAULT if not provided.</param>
		/// <param name="populateFreshnessSeconds">Specific freshness requirement for populated relations. Defaults to FRESHNESS_DEFAULT if not provided.</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">Specifies if the request should be held from processing. Defaults to null.</param>
		/// <returns>A new instance of RequestOp representing a "Get" request.</returns>
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

		/// <summary>
		/// Constructs a "Get" operation for a specified type with additional configuration options.
		/// </summary>
		/// <param name="modelType">The type of the model to be retrieved.</param>
		/// <param name="id">The identifier for the model to be retrieved.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created. If not specified, the current time is used.</param>
		/// <param name="populate">Associations to be populated along with the model.</param>
		/// <param name="freshnessSeconds">Indicates how fresh the data should be. Defaults to FRESHNESS_DEFAULT if not provided.</param>
		/// <param name="populateFreshnessSeconds">Specific freshness requirement for populated relations. Defaults to FRESHNESS_DEFAULT if not provided.</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">Specifies if the request should be held from processing. Defaults to null.</param>
		/// <returns>A new instance of RequestOp representing a "Get" request.</returns>
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
		
		/// <summary>
		/// Constructs a "BlobGet" operation for retrieving a binary large object at a specified path.
		/// </summary>
		/// <param name="path">The path of the blob to be retrieved.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created. If not specified, the current time is used.</param>
		/// <param name="freshnessSeconds">Indicates how fresh the blob data should be. Defaults to FRESHNESS_DEFAULT if not provided.</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">Specifies if the request should be held from processing. Defaults to null.</param>
		/// <returns>A new instance of RequestOp representing a "BlobGet" request.</returns>
		public static RequestOp BlobGetOp(
			string path,
			long timeMs = -1,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null, 
			bool? hold = null
		) {
			return new RequestOp(
				timeMs==-1 ? CascadeUtils.NowMs : timeMs,
				typeof(byte[]),
				RequestVerb.BlobGet,
				path,
				freshnessSeconds: freshnessSeconds ?? FRESHNESS_DEFAULT,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? FRESHNESS_ANY,
				hold: hold
			);
		}
		
		/// <summary>
		/// Constructs a "BlobPut" operation for storing a binary large object at a specified path.
		/// </summary>
		/// <param name="path">The path where the blob will be stored.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <param name="data">The byte array representing the blob data to be stored.</param>
		/// <param name="hold">Specifies if the request should be held from processing. Defaults to null.</param>
		/// <returns>A new instance of RequestOp representing a "BlobPut" request.</returns>
		public static RequestOp BlobPutOp(
			string path,
			long timeMs,
			byte[] data, 
			bool? hold = null
		) {
			return new RequestOp(
				timeMs,
				typeof(byte[]),
				RequestVerb.BlobPut,
				path,
				value: data,
				hold: hold
			);
		}

		/// <summary>
		/// Constructs a "BlobDestroy" operation for deleting a binary large object at a specified path.
		/// </summary>
		/// <param name="path">The path of the blob to be deleted.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <returns>A new instance of RequestOp representing a "BlobDestroy" request.</returns>
		public static RequestOp BlobDestroyOp(
			string path, 
			long timeMs
		) {
			return new RequestOp(
				timeMs,
				typeof(byte[]),
				RequestVerb.BlobDestroy,
				path
			);
		}
		
		/// <summary>
		/// Constructs a "GetCollection" operation for retrieving a collection of model ids.
		/// </summary>
		/// <typeparam name="Model">The type of the model</typeparam>
		/// <param name="collectionName">The name of the collection to be retrieved.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created. If not specified, the current time is used.</param>
		/// <returns>A new instance of RequestOp representing a "GetCollection" request.</returns>
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
		
		/// <summary>
		/// Constructs a "Query" operation for querying a collection of models with specific criteria.
		/// </summary>
		/// <typeparam name="Model">The type of the models in the collection.</typeparam>
		/// <param name="collectionName">The name of the collection to be queried.</param>
		/// <param name="criteria">The criteria used to filter models within the collection.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <param name="populate">Collection of string relations to be populated along with the models.</param>
		/// <param name="freshnessSeconds">Indicates how fresh the data should be. Defaults to FRESHNESS_DEFAULT if not provided.</param>
		/// <param name="populateFreshnessSeconds">Specific freshness requirement for populated relations. Defaults to FRESHNESS_DEFAULT if not provided.</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">Specifies if the request should be held from processing. Defaults to null.</param>
		/// <returns>A new instance of RequestOp representing a "Query" request.</returns>
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

		/// <summary>
		/// Constructs a "Create" operation for creating a new model instance.
		/// </summary>
		/// <param name="model">The model instance to be created.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <param name="hold">Specifies if the request should be held from processing.</param>
		/// <returns>A new instance of RequestOp representing a "Create" request.</returns>
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
		
		/// <summary>
		/// Constructs a "Destroy" operation for deleting a specified model instance.
		/// </summary>
		/// <typeparam name="Model">The type of the model to be deleted.</typeparam>
		/// <param name="model">The model instance to be deleted.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <returns>A new instance of RequestOp representing a "Destroy" request.</returns>
		public static RequestOp DestroyOp<Model>(Model model, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Destroy,
				CascadeTypeUtils.GetCascadeId(model),
				value: model
			);
		}
		
		/// <summary>
		/// Constructs a "Replace" operation for replacing an existing model instance.
		/// </summary>
		/// <typeparam name="Model">The type of the model to be replaced.</typeparam>
		/// <param name="model">The model instance to be replaced.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <returns>A new instance of RequestOp representing a "Replace" request.</returns>
		public static RequestOp ReplaceOp<Model>(Model model, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Replace,
				CascadeTypeUtils.GetCascadeId(model),
				value: model
			);
		}
		
		/// <summary>
		/// Constructs an "Update" operation for updating an existing model instance with specified changes.
		/// </summary>
		/// <typeparam name="Model">The type of the model to be updated.</typeparam>
		/// <param name="model">The model instance to be updated.</param>
		/// <param name="changes">A dictionary containing the changes to be applied to the model.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <returns>A new instance of RequestOp representing an "Update" request.</returns>
		public static RequestOp UpdateOp<Model>(Model model, IDictionary<string, object?> changes, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(Model),
				RequestVerb.Update,
				CascadeTypeUtils.GetCascadeId(model),
				value: changes,
				extra: model
			);
		}
		
		/// <summary>
		/// Constructs an "Execute" operation to perform a custom action with specified parameters.
		/// </summary>
		/// <typeparam name="OriginClass">The class from which the action originates.</typeparam>
		/// <typeparam name="ReturnType">The expected return type from executing the action.</typeparam>
		/// <param name="action">The name of the action to be executed.</param>
		/// <param name="parameters">A dictionary containing parameters required for the action.</param>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <returns>A new instance of RequestOp representing an "Execute" request.</returns>
		public static RequestOp ExecuteOp<OriginClass,ReturnType>(string action, IDictionary<string, object?> parameters, long timeMs) {
			return new RequestOp(
				timeMs,
				typeof(OriginClass),
				RequestVerb.Execute,
				null,
				value: action,
				criteria: parameters
			);
		}
		
		/// <summary>
		/// Initializes a new instance of the RequestOp class with the provided details.
		/// </summary>
		/// <param name="timeMs">The timestamp in milliseconds when the request was created.</param>
		/// <param name="type">The type of the model associated with the request.</param>
		/// <param name="verb">The operation type represented by a verb.</param>
		/// <param name="id">The identifier of the model for requests involving a specific instance.</param>
		/// <param name="value">The value associated with the request, such as the model or changes.</param>
		/// <param name="populate">A collection indicating associations to be populated, if applicable.</param>
		/// <param name="freshnessSeconds">Desired freshness of the returned data in seconds.</param>
		/// <param name="populateFreshnessSeconds">Desired freshness for populated relations in seconds.</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness in seconds to use if main freshness cannot be satisfied.</param>
		/// <param name="hold">Specifies whether the request should be held for deferred processing.</param>
		/// <param name="criteria">Criteria for filtering results in certain operations like queries.</param>
		/// <param name="key">Identifier for collection-level operations instead of individual instances.</param>
		/// <param name="extra">Any additional data required by the request.</param>
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
		
		/// <summary>
		/// Creates a new instance of RequestOp by cloning the current instance and optionally modifying its properties.
		/// </summary>
		/// <param name="timeMs">Optional new timestamp in milliseconds.</param>
		/// <param name="type">Optional new model type.</param>
		/// <param name="verb">Optional new operation type.</param>
		/// <param name="id">Optional new model identifier.</param>
		/// <param name="value">Optional new value associated with the request.</param>
		/// <param name="populate">Optional new relationships to populate.</param>
		/// <param name="freshnessSeconds">Optional new freshness requirement in seconds.</param>
		/// <param name="populateFreshnessSeconds">Optional new populate freshness in seconds.</param>
		/// <param name="fallbackFreshnessSeconds">Optional new fallback freshness in seconds.</param>
		/// <param name="hold">Optional new hold status.</param>
		/// <param name="criteria">Optional new criteria.</param>
		/// <param name="key">Optional new key for collection operations.</param>
		/// <param name="extra">Optional new extra data.</param>
		/// <returns>A cloned instance of RequestOp with optional modifications.</returns>
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

		/// <summary>
		/// Determines if a specified RequestVerb is associated with write operations such as Create, Update, or Execute.
		/// </summary>
		/// <param name="aVerb">The RequestVerb to be checked.</param>
		/// <returns>bool indicating whether the verb is associated with a write operation.</returns>
		public static bool IsWriteVerb(RequestVerb aVerb) {
			return aVerb == RequestVerb.Create ||
			       aVerb == RequestVerb.Update ||
			       aVerb == RequestVerb.Execute;
		}

		/// <summary>
		/// Parses a string to a corresponding RequestVerb. If parsing fails, it returns RequestVerb.None.
		/// </summary>
		/// <param name="aString">The string to be parsed.</param>
		/// <returns>A RequestVerb corresponding to the string, or None if the string could not be parsed.</returns>
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
		
		/// <summary>
		/// Attempts to return the Id field as an integer, if it can be interpreted as such.
		/// </summary>
		public int? IdAsInt {
			get {
				if (Id == null)
					return null;
				if ((Id is int) || (Id is long))
					return (int)Id;
				return null;
			}
		}

		/// <summary>
		/// Attempts to return the Id field as a long, if it can be interpreted as such.
		/// </summary>
		public long? IdAsLong {
			get {
				if (Id == null)
					return null;
				if ((Id is int) || (Id is long))
					return (long)Id;
				return null;
			}
		}

		/// <summary>
		/// Attempts to return the Id field as a string, if it can be interpreted as long or int.
		/// </summary>
		public String? IdAsString {
			get {
				if (Id == null)
					return null;
				if ((Id is int) || (Id is long))
					return ((long)Id).ToString();
				return null;
			}
		}


		// Only one of Key or Id would normally be used

		public readonly IEnumerable<string>? Populate;
		
		public readonly int? FreshnessSeconds = FRESHNESS_DEFAULT;
		public readonly int? PopulateFreshnessSeconds = FRESHNESS_DEFAULT;
		public readonly int? FallbackFreshnessSeconds = FRESHNESS_ANY;
		public readonly bool Hold;
		
		public readonly IDictionary<string, string> Params;	// app specific paramters for the request

		/// <summary>
		/// Converts the operation details into a concise summary string for logging or display purposes.
		/// </summary>
		/// <returns>A string summary of the request operation, including verb, type, id, key, criteria, and freshness.</returns>
		/// <remarks>
		/// Be aware that JsonSerializer.Serialize(Criteria) will throw an UnsupportedException in production (not development)
		/// if Criteria is an ImmutableDictionary.
		/// </remarks>
		public string ToSummaryString() {
			var criteria = JsonSerializer.Serialize(Criteria);
			return $"{Verb} Id:{Id} Type:{Type} Key:{Key} Criteria:{criteria} Freshness: {FreshnessSeconds}";
		}

	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.Cascade.Utilities;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// The main class for all data requests and other operations.
	/// This would generally only be created once on startup of an application.
	/// </summary>
	public partial class CascadeDataLayer {


		/// <summary>
		/// Get data from cache/origin of model type M and return result or null
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="M">model type - subclass of SuperModel</typeparam>
		/// <returns>model of type M or null</returns>
		public async Task<M?> Get<M>(
			int id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) where M : class {
			return (await this.GetResponse(typeof(M),id, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold, timeMs)).Result as M;
		}

		/// <summary>
		/// Get data from cache/origin of model type M and return result or null
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">whether to mark the main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="M">model type - subclass of SuperModel</typeparam>
		/// <returns>model of type M or null</returns>
		public async Task<M?> Get<M>(
			string id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) where M : class {
			return (await this.GetResponse(typeof(M),id, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold, timeMs)).Result as M;
		}


		/// <summary>
		/// Get data from cache/origin of model type M and return result or null
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="M">model type - subclass of SuperModel</typeparam>
		/// <returns>model of type M or null</returns>
		public async Task<M?> Get<M>(
			long id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) where M : class {
			return (await this.GetResponse(typeof(M),id, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold, timeMs)).Result as M;
		}


		/// <summary>
		/// Get a model instance of given model type and id with a full detail OpResponse object
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <returns>OpResponse</returns>
		public Task<OpResponse> GetResponse(
			Type modelType,
			object id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) {
			var req = RequestOp.GetOp(
				modelType,
				id,
				timeMs ?? NowMs,
				populate,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				populateFreshnessSeconds ?? Config.DefaultPopulateFreshnessSeconds,
				fallbackFreshnessSeconds ?? Config.DefaultFallbackFreshnessSeconds,
				hold
			);
			return ProcessRequest(req);
		}

		
		public async Task<IEnumerable<OpResponse>> GetModelsForIds(
			Type type,
			IEnumerable iids,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) {
			var timeMsFixed = timeMs ?? NowMs;
			const int MaxParallelRequests = 8;
			var ids = iids.Cast<object>().ToImmutableArray();
			Log.Debug("BEGIN GetModelsForIds");
			var profiler = new TimingProfiler("GetModelsForIds "+type.Name);
			profiler.Start();
			OpResponse[] allResponses = new OpResponse[ids.Count()];
			for (var i = 0; i < ids.Count(); i += MaxParallelRequests) {
				var someIds = ids.Skip(i).Take(MaxParallelRequests).ToImmutableArray();

				var tasks = someIds.Select(id => {
					return Task.Run(() => ProcessRequest( // map each id to a get request and process it
						new RequestOp(
							timeMsFixed,
							type,
							RequestVerb.Get,
							id,
							freshnessSeconds: freshnessSeconds,
							fallbackFreshnessSeconds: fallbackFreshnessSeconds,
							hold: hold
						)
					));
				}).ToImmutableArray();
				var someGetResponses = await Task.WhenAll(tasks); // wait on all requests in parallel
				for (int j = 0; j < someGetResponses.Length; j++) // fill allResponses array from responses
					allResponses[i + j] = someGetResponses[j];
			}
			profiler.Stop();
			Log.Information(profiler.Report());
			Log.Debug("END GetModelsForIds");
			return allResponses.ToImmutableArray();
		}
		
		
		/// <summary>
		/// Gets a collection literally ie an enumerable of ids
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <typeparam name="M">the type of the collection</typeparam>
		/// <returns>enumerable of ids</returns>
		public async Task<IEnumerable<object>?> GetCollection<M>(
			string collectionName,
			long? timeMs = null
		) where M : class {
			return (await this.GetCollectionResponse<M>(collectionName,timeMs)).Result as IEnumerable<object>;
		}

		/// <summary>
		/// Gets a collection literally ie an enumerable of ids with the full detail OpResponse
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <param name="timeMs"></param>
		/// <typeparam name="M">the type of the collection</typeparam>
		/// <returns>OpResponse with Results = enumerable of ids</returns>
		public Task<OpResponse> GetCollectionResponse<M>(string collectionName, long? timeMs = null) {
			var req = RequestOp.GetCollectionOp<M>(
				collectionName,
				timeMs ?? NowMs
			);
			return ProcessRequest(req);
		}
		
		
		/// <summary>
		/// A kind of query like that used for populating HasMany/HasOne associations. Not normally used.
		/// </summary>
		/// <param name="propertyName">Name of foreign key</param>
		/// <param name="propertyValue">Value of foreign key</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		/// <returns>OpResponse</returns>
		public async Task<OpResponse> GetWhereCollectionResponse<Model>(
			string propertyName, 
			string propertyValue, 
			int? freshnessSeconds = null, 
			int? populateFreshnessSeconds = null,
			long? timeMs = null
		) {
			var key = CascadeUtils.WhereCollectionKey(typeof(Model).Name, propertyName, propertyValue);
			var requestOp = new RequestOp(
				timeMs ?? NowMs,
				typeof(Model),
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				populateFreshnessSeconds: populateFreshnessSeconds ?? Config.DefaultPopulateFreshnessSeconds,
				criteria: new Dictionary<string, object?>() { [propertyName] = propertyValue },
				key: key
			);
			var opResponse = await ProcessRequest(requestOp);
			return opResponse;
		}

		/// <summary>
		/// A kind of query like that used for populating HasMany/HasOne associations. Not normally used.
		/// </summary>
		/// <param name="propertyName">Name of foreign key</param>
		/// <param name="propertyValue">Value of foreign key</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		/// <returns>Enumerable of models of type M</returns>
		public async Task<IEnumerable<M>> GetWhereCollection<M>(
			string propertyName, 
			string propertyValue, 
			int? freshnessSeconds = null, 
			int? populateFreshnessSeconds = null,
			long? timeMs = null
		) where M : class {
			var response = await this.GetWhereCollectionResponse<M>(propertyName, propertyValue, freshnessSeconds, populateFreshnessSeconds, timeMs);
			var results = response.Results.Cast<M>().ToImmutableArray();
			return results;
		}

		/// <summary>
		/// Do a search on the origin with the given model and criteria and cache the resulting collection under the collectionKey.
		/// Models are cached and returned, as are populated association models.
		/// </summary>
		/// <param name="collectionKey"></param>
		/// <param name="criteria"></param>
		/// <param name="populate"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="populateFreshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="M"></typeparam>
		/// <returns>IEnumerable<M></returns>
		public async Task<IEnumerable<M>> Query<M>(
			string? collectionKey,
			object? criteria = null,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) {
			var response = await QueryResponse<M>(collectionKey, criteria, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			var results = response.Results.Cast<M>().ToImmutableArray();
			//return Array.ConvertAll<object,M>(response.Results) ?? Array.Empty<M>();
			return results;
		}

		/// <summary>
		/// Do a search on the origin with the given model and criteria for a single record, and cache the resulting collection under the collectionKey.
		/// Models are cached and returned, as are populated association models.
		/// </summary>
		/// <param name="collectionKey"></param>
		/// <param name="criteria"></param>
		/// <param name="populate"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="populateFreshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="M"></typeparam>
		/// <returns></returns>
		public async Task<M?> QueryOne<M>(
			string? collectionKey,
			object criteria = null,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) {
			return (await this.Query<M>(collectionKey, criteria, populate, freshnessSeconds: freshnessSeconds, populateFreshnessSeconds: populateFreshnessSeconds, fallbackFreshnessSeconds: fallbackFreshnessSeconds, hold: hold, timeMs: timeMs)).FirstOrDefault();
		}

		/// <summary>
		/// Do a search on the origin with the given model and criteria and cache the resulting collection under the collectionKey and return full detail OpResponse.
		/// Models are cached and returned, as are populated association models.
		/// </summary>
		/// <param name="collectionKey"></param>
		/// <param name="criteria"></param>
		/// <param name="populate"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="populateFreshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <typeparam name="M"></typeparam>
		/// <returns>OpResponse</returns>
		public Task<OpResponse> QueryResponse<M>(string collectionName,
			object criteria,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) {
			var req = RequestOp.QueryOp<M>(
				collectionName,
				criteria,
				timeMs ?? NowMs,
				populate: populate,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				populateFreshnessSeconds: populateFreshnessSeconds ?? Config.DefaultPopulateFreshnessSeconds,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? Config.DefaultFallbackFreshnessSeconds,
				hold ?? false
			);
			return ProcessRequest(req);
		}

		/// <summary>
		/// Create and return a model of the given type. An instance is used to pass in the values, and a newly created
		/// instance is returned from the origin.
		/// Note: the populate option will be removed from all write methods. Instead Create should be called followed by
		/// any call(s) to Populate() as required. 
		/// </summary>
		/// <param name="model"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>model of type M</returns>
		public async Task<M> Create<M>(M model, bool hold = false) {
			var response = await CreateResponse<M>(model,hold: hold);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}
		
		/// <summary>
		/// Create and return a model of the given type. An instance is used to pass in the values, and a newly created
		/// instance is returned from the origin.
		/// Note: the populate option will be removed from all write methods. Instead Create should be called followed by
		/// any call(s) to Populate() as required. 
		/// </summary>
		/// <param name="model"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>OpResponse with full detail of operation, including Result of type M</returns>
		public Task<OpResponse> CreateResponse<M>(M model, bool hold = false) {
			var req = RequestOp.CreateOp(
				model!,
				NowMs,
				hold: hold
			);
			return ProcessRequest(req);
		}

		public async Task<M> Replace<M>(M model) {
			var response = await ReplaceResponse<M>(model);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}

		public Task<OpResponse> ReplaceResponse<M>(M model) {
			var req = RequestOp.ReplaceOp(
				model!,
				NowMs
			);
			return ProcessRequest(req);
		}

		// may return null if the record no longer exists
		public async Task<M?> Update<M>(M model, IDictionary<string, object?> changes) where M : class {
			var response = await UpdateResponse<M>(model, changes);
			if (response.Result != null && response.Result is not M)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return response.Result as M;
		}
		
		public Task<OpResponse> UpdateResponse<M>(M model, IDictionary<string, object?> changes) {
			var req = RequestOp.UpdateOp(
				model!,
				changes,
				NowMs
			);
			return ProcessRequest(req);
		}
		
		public async Task Destroy<M>(M model) {
			var response = await DestroyResponse<M>(model);
		}

		public Task<OpResponse> DestroyResponse<M>(M model) {
			var req = RequestOp.DestroyOp<M>(
				model,
				NowMs
			);
			return ProcessRequest(req);
		}

		// ModelType : what model type are you executing the action on? Useful when implementing the action on the origin
		// ReturnType : the type you will be returning - eg. same as ModelType or IEnumerable<ModelType> or anything else
		public async Task<ReturnType> Execute<ModelType, ReturnType>(string action, IDictionary<string, object?> parameters) {
			var response = await ExecuteResponse<ModelType, ReturnType>(
				action,
				parameters
			);
			if (response.Result is not ReturnType result)
				throw new AssumptionException($"Should be of type {typeof(ReturnType).Name}");
			return (ReturnType)response.Result;
		}

		public Task<OpResponse> ExecuteResponse<ModelType, ReturnType>(string action, IDictionary<string, object?> parameters) {
			var req = RequestOp.ExecuteOp<ModelType, ReturnType>(
				action,
				parameters,
				NowMs
			);
			return ProcessRequest(req);
		}
		

		/// <summary>
		/// Populates (sets the given association property(s) on the given model each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). 
		/// </summary>
		/// <param name="model">model to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		public async Task Populate(
			SuperModel model, 
			string property, 
			int? freshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool skipIfSet = false, 
			bool? hold = null, 
			long? timeMs = null
		) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);

			if (skipIfSet && model != null && propertyInfo.GetValue(model) != null) {
				Log.Debug($"Skipping Populate {nameof(modelType)}.{property}");
				return;
			}

			if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute), true).FirstOrDefault() is HasManyAttribute hasMany) {
				await processHasMany(model, modelType, propertyInfo!, hasMany, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(HasOneAttribute), true).FirstOrDefault() is HasOneAttribute hasOne) {
				await processHasOne(model, modelType, propertyInfo!, hasOne, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(BelongsToAttribute), true).FirstOrDefault() is BelongsToAttribute belongsTo) {
				await processBelongsTo(model, modelType, propertyInfo!, belongsTo, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(FromBlobAttribute), true).FirstOrDefault() is FromBlobAttribute fromBlob) {
				await processFromBlob(model, modelType, propertyInfo!, fromBlob, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(FromPropertyAttribute), true).FirstOrDefault() is FromPropertyAttribute fromProperty) {
				await processFromProperty(model, modelType, propertyInfo!, fromProperty);
			}
		}

		/// <summary>
		/// Populates (sets the given association property(s) on the given model each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). 
		/// </summary>
		/// <param name="model">model to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		public async Task Populate(SuperModel model, IEnumerable<string> associations, int? freshnessSeconds = null, int? fallbackFreshnessSeconds = null, bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var association in associations) {
				await Populate(model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
			}
		}

		/// <summary>
		/// Populates (sets the given association property(s) on the given models each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). This is useful for setting association(s) on a list of models.
		/// In future, this could be optimised for when many are associated with the same. 
		/// </summary>
		/// <param name="models">models to act on</param>
		/// <param name="associations"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="property">nameof(Model.someProperty)</param>
		public async Task Populate(IEnumerable<SuperModel> models, IEnumerable<string> associations, int? freshnessSeconds = null, int? fallbackFreshnessSeconds = null, bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var model in models) {
				foreach (var association in associations) {
					await Populate((SuperModel)model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
				}
			}
		}

		/// <summary>
		/// Populates (sets a given association property on the given models according to the definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). This is useful for setting association(s) on a list of models.
		/// In future, this could be optimised for when many are associated with the same. 
		/// </summary>
		/// <param name="models">models to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="association"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		public async Task Populate(IEnumerable<SuperModel> models, string association, int? freshnessSeconds = null,int? fallbackFreshnessSeconds = null,  bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var model in models) {
				await Populate((SuperModel)model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
			}
		}

		
		
	}
}

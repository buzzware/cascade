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
  /// Methods used by an application for reading (Get and Query)
  /// </summary>
  public partial class CascadeDataLayer {

    /// <summary>
    /// Retrieves data from cache or origin of model type M based on a numerical ID and returns the result or null.
    /// </summary>
    /// <param name="id">ID of model to retrieve.</param>
    /// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests.</param>
    /// <param name="freshnessSeconds">Freshness duration for the main object.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for any populated associations.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
    /// <param name="hold">Indicates whether to mark the main object and populated associations to be held in cache.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970. Ideally, a group of requests will be given the same time to optimize caching.</param>
    /// <typeparam name="M">Model type, which is a subclass of SuperModel.</typeparam>
    /// <returns>Model of type M or null.</returns>
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
    /// Retrieves data from cache or origin of model type M based on a string ID and returns the result or null.
    /// </summary>
    /// <param name="id">ID of model to retrieve.</param>
    /// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests.</param>
    /// <param name="freshnessSeconds">Freshness duration for the main object.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for any populated associations.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
    /// <param name="hold">Indicates whether to mark the main object and populated associations to be held in cache.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970. Ideally, a group of requests will be given the same time to optimize caching.</param>
    /// <typeparam name="M">Model type, which is a subclass of SuperModel.</typeparam>
    /// <returns>Model of type M or null.</returns>
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
    /// Retrieves data from cache or origin of model type M based on a long ID and returns the result or null.
    /// </summary>
    /// <param name="id">ID of model to retrieve.</param>
    /// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests.</param>
    /// <param name="freshnessSeconds">Freshness duration for the main object.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for any populated associations.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
    /// <param name="hold">Indicates whether to mark the main object and populated associations to be held in cache.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970. Ideally, a group of requests will be given the same time to optimize caching.</param>
    /// <typeparam name="M">Model type, which is a subclass of SuperModel.</typeparam>
    /// <returns>Model of type M or null.</returns>
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
    /// Retrieves a model instance of the given model type and ID with a full detail OpResponse object.
    /// </summary>
    /// <param name="id">ID of model to retrieve.</param>
    /// <param name="populate">Enumerable association property names to set with data for convenience.</param>
    /// <param name="freshnessSeconds">Freshness duration for the main object.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for any populated associations.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
    /// <param name="hold">Indicates whether to mark the main object and populated associations to be held in cache.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970.</param>
    /// <returns>OpResponse containing the response details.</returns>
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

    /// <summary>
    /// Retrieves models of the given type for a multitude of IDs.
    /// </summary>
    /// <param name="type">The type of models to retrieve.</param>
    /// <param name="iids">An enumerable list of IDs for which models are to be retrieved.</param>
    /// <param name="freshnessSeconds">Freshness duration for the retrieved objects.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement.</param>
    /// <param name="hold">Indicates whether to mark the retrieved objects to be held in cache.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970.</param>
    /// <returns>An enumerable of OpResponse containing the retrieved models.</returns>
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
    /// Gets a collection literally, i.e., an enumerable of IDs.
    /// </summary>
    /// <param name="collectionName">User-defined name for the collection.</param>
    /// <typeparam name="M">The type of the collection.</typeparam>
    /// <returns>An enumerable of IDs.</returns>
    public async Task<IEnumerable<object>?> GetCollection<M>(
      string collectionName,
      long? timeMs = null
    ) where M : class {
      return (await this.GetCollectionResponse<M>(collectionName,timeMs)).Result as IEnumerable<object>;
    }

    /// <summary>
    /// Retrieves a collection as an enumerable of IDs with full detail as an OpResponse.
    /// </summary>
    /// <param name="collectionName">User-defined name for the collection.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970.</param>
    /// <typeparam name="M">The type of the collection.</typeparam>
    /// <returns>OpResponse with Results as an enumerable of IDs.</returns>
    public Task<OpResponse> GetCollectionResponse<M>(string collectionName, long? timeMs = null) {
      var req = RequestOp.GetCollectionOp<M>(
        collectionName,
        timeMs ?? NowMs
      );
      return ProcessRequest(req);
    }
    
    /// <summary>
    /// Used for populating HasMany or HasOne associations based on a foreign key.
    /// </summary>
    /// <param name="propertyName">Name of the foreign key.</param>
    /// <param name="propertyValue">Value of the foreign key.</param>
    /// <param name="freshnessSeconds">Freshness duration for the main object.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for any populated associations.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970.</param>
    /// <typeparam name="Model">The type of the collection.</typeparam>
    /// <returns>OpResponse containing the response details.</returns>
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
    /// Used for populating HasMany or HasOne associations based on a foreign key.
    /// </summary>
    /// <param name="propertyName">Name of the foreign key.</param>
    /// <param name="propertyValue">Value of the foreign key.</param>
    /// <param name="freshnessSeconds">Freshness duration for the main object.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for any populated associations.</param>
    /// <param name="timeMs">(Optional) Request time represented as milliseconds since 1970.</param>
    /// <typeparam name="Model">The type of the collection.</typeparam>
    /// <returns>Enumerable of models of type M.</returns>
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
    /// Performs a query on the origin with the given model and criteria, caching the resulting collection under collectionKey.
    /// </summary>
    /// <param name="collectionKey">Key under which the collection will be cached.</param>
    /// <param name="criteria">Filtering criteria for the query.</param>
    /// <param name="populate">Association names to populate with data.</param>
    /// <param name="freshnessSeconds">Freshness duration for main objects.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for populated associations.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if main requirement is not met.</param>
    /// <param name="hold">Whether to hold objects in cache.</param>
    /// <param name="timeMs">(Optional) Request time as milliseconds since 1970 for optimized caching.</param>
    /// <typeparam name="M">Model type to query.</typeparam>
    /// <returns>IEnumerable<M> containing the query results.</returns>
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
      return results;
    }

    /// <summary>
    /// Performs a query on the origin with the given model and criteria for a single record.
    /// Caches the result under the collectionKey.
    /// </summary>
    /// <param name="collectionKey">Key under which collection will be cached.</param>
    /// <param name="criteria">Filtering criteria for the query.</param>
    /// <param name="populate">Association names to populate with data.</param>
    /// <param name="freshnessSeconds">Freshness duration for main objects.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for populated associations.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if main requirement is not met.</param>
    /// <param name="hold">Whether to hold objects in cache.</param>
    /// <param name="timeMs">(Optional) Request time as milliseconds since 1970 for optimized caching.</param>
    /// <typeparam name="M">Model type to query.</typeparam>
    /// <returns>Single model of type M if found, otherwise null.</returns>
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
    /// Performs a query on the origin with the given model and criteria, caching the resulting collection under collectionKey and returns a full detail OpResponse.
    /// </summary>
    /// <param name="collectionKey">Key under which collection will be cached.</param>
    /// <param name="criteria">Filtering criteria for the query.</param>
    /// <param name="populate">Association names to populate with data.</param>
    /// <param name="freshnessSeconds">Freshness duration for main objects.</param>
    /// <param name="populateFreshnessSeconds">Freshness duration for populated associations.</param>
    /// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if main requirement is not met.</param>
    /// <param name="hold">Whether to hold objects in cache.</param>
    /// <param name="timeMs">(Optional) Request time as milliseconds since 1970 for optimized caching.</param>
    /// <typeparam name="M">Model type to query.</typeparam>
    /// <returns>OpResponse with query details.</returns>
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

  }
}

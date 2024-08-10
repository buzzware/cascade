using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Buzzware.Cascade {

  /// <summary>
  /// A memory based cache for a given model type designed to store and retrieve instances of models and collections of ids
  /// with associated arrival timestamps
  /// </summary>
  public class ModelClassCache<Model, IdType> : IModelClassCache 
    where Model : class {

    /// <summary>
    /// Stores model instances with arrival timestamp in ms by id 
    /// </summary>
    private readonly ConcurrentDictionary<IdType, Tuple<Model, long>> models = new ConcurrentDictionary<IdType, Tuple<Model, long>>();

    /// <summary>
    /// Stores collections of ids with arrival timestamp in ms by their name
    /// </summary>
    private readonly ConcurrentDictionary<string, Tuple<IEnumerable, long>> collections = new ConcurrentDictionary<string, Tuple<IEnumerable, long>>();

    /// <summary>
    /// Reference to the CascadeDataLayer
    /// </summary>
    public CascadeDataLayer Cascade { get; set; }

    /// <summary>
    /// ModelClassCache Constructor
    /// </summary>
    public ModelClassCache() {
      
    }
    
    /// <summary>
    /// Fetches the requested model or collection from the cache if available and fresh enough;
    /// otherwise returns a none response to indicate a miss.
    /// </summary>
    /// <param name="requestOp">The operation object containing request details for fetching data.</param>
    /// <returns>An OpResponse object containing details about the request result.</returns>
    public async Task<OpResponse> Fetch(RequestOp requestOp) {
      if (requestOp.Type != typeof(Model))
        throw new Exception("requestOp.Type != typeof(Model)");
      
      switch (requestOp.Verb) {
        case RequestVerb.Get:
          var id = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), requestOp.Id); 
          if (id == null)
            throw new Exception("Unable to get right value for Id");
          
          models.TryGetValue(id, out var modelEntry);
          
          if (
            modelEntry != null && 
            (requestOp.FreshnessSeconds>=0) && 
            (requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-modelEntry.Item2) <= requestOp.FreshnessSeconds*1000))
          ) {
            return new OpResponse(
              requestOp,
              Cascade.NowMs,
              connected: true,
              exists: true,
              result: modelEntry.Item1,
              arrivedAtMs: modelEntry.Item2
            );
          } else {
            return OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name);
          }
          break;

        case RequestVerb.Query:
        case RequestVerb.GetCollection:
          
          collections.TryGetValue(requestOp.Key!, out var collEntry);
          
          if (
            collEntry != null && 
            (requestOp.FreshnessSeconds>=0) && 
            (requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-collEntry.Item2) <= requestOp.FreshnessSeconds*1000))
          ) {
            return new OpResponse(
              requestOp,
              Cascade.NowMs,
              connected: true,
              exists: true,
              result: collections[requestOp.Key!].Item1,
              arrivedAtMs: collections[requestOp.Key!].Item2
            );
          } else {
            return OpResponse.None(requestOp,Cascade.NowMs, this.GetType().Name);
          }
          break;

        default:
          throw new NotImplementedException($"Unsupported {requestOp.Verb}");
      }
    }

    /// <summary>
    /// Fetches a model instance from the cache if available and within freshness period.
    /// </summary>
    /// <typeparam name="Model">The type of model being requested.</typeparam>
    /// <param name="id">The identifier for the model instance.</param>
    /// <param name="freshnessSeconds">The maximum acceptable age of the cached data in seconds.</param>
    /// <returns>The model instance if found and fresh enough, otherwise null.</returns>
    public async Task<Model?> Fetch<Model>(object id, int freshnessSeconds = 0) where Model : class {
      var response = await Fetch(RequestOp.GetOp<Model>(id, Cascade.NowMs, freshnessSeconds: freshnessSeconds));
      return response.Result as Model;
    }

    /// <summary>
    /// Stores a model instance in the cache with the arrived timestamp.
    /// </summary>
    /// <param name="id">The identifier representing the model.</param>
    /// <param name="model">The model object to be stored.</param>
    /// <param name="arrivedAt">The timestamp when the model data was obtained.</param>
    public async Task Store(object id, object model, long arrivedAt) {
      var idTyped = (IdType?) CascadeTypeUtils.ConvertTo(typeof(IdType), id);
      if (idTyped == null)
        throw new Exception("Bad id");
      models[idTyped] = new Tuple<Model, long>((Model)model, arrivedAt);
    }
    
    /// <summary>
    /// Stores a collection of ids in the cache with the specified name key and arrived timestamp.
    /// </summary>
    /// <param name="key">A unique key identifying the collection.</param>
    /// <param name="ids">The enumerable collection of model identifiers.</param>
    /// <param name="arrivedAt">The timestamp when the collection data was obtained.</param>
    public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
      collections[key] = new Tuple<IEnumerable, long>(ids, arrivedAt);
    }

    /// <summary>
    /// Removes a model instance from the cache based on the given id
    /// </summary>
    /// <param name="id">The identifier of the model to remove.</param>
    /// <returns></returns>
    public Task Remove(object id) {
      return Remove((IdType)id);
    }

    /// <summary>
    /// Removes a model instance from the cache using the typed id
    /// </summary>
    /// <param name="id">The strongly typed identifier of the model to remove.</param>
    public async Task Remove(IdType id) {
      models.TryRemove(id, out var value);
    }
    
    /// <summary>
    /// Clears all models and collections from the cache, 
    /// optionally holding certain elements if specified.
    /// </summary>
    /// <param name="exceptHeld">Indicates whether to exclude certain held items from clearing.</param>
    /// <param name="olderThan">Optionally specifies a date threshold to restrict which items are cleared.</param>
    public async Task ClearAll(bool exceptHeld, DateTime? olderThan = null) {
      if (exceptHeld || olderThan!=null) {
        
        // Process held model instances
        var heldModelIds = Cascade.ListHeldIds<Model>().ToArray();
        var idsToRemove = models.Where(kv => {
            var id = kv.Key;
            var contains = heldModelIds.Contains(id);
            return !contains;
          })
          .Select(kv => kv.Key)
          .ToArray();
        foreach (var id in idsToRemove.Reverse())
          models.TryRemove(id, out var v);
        
        // Process collections
        var heldCollectionNames = Cascade.ListHeldCollections(typeof(Model)).ToArray();
        var namesToRemove = collections.Where(kv => !heldCollectionNames.Contains(kv.Key))
          .Select(kv => kv.Key)
          .ToArray();
        foreach (var name in namesToRemove)
          collections.TryRemove(name, out var v);
      } else {
        models.Clear();
        collections.Clear();
      }
    }
  }
}

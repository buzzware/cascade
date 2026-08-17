using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Buzzware.Cascade {

  /// <summary>
  /// Interface for a cache to store model instances for a specified model class
  /// </summary>
  public interface IModelClassCache {

    /// <summary>
    /// CascadeDataLayer reference
    /// </summary>
    CascadeDataLayer Cascade { get; set; }

    /// <summary>
    /// Implement this for any setup required
    /// </summary>
    Task Setup();
    
    /// <summary>
    /// Caches a model instance with a specific id
    /// </summary>
    /// <param name="id">The identifier for the model to be stored in the cache.</param>
    /// <param name="model">The actual model instance to cache.</param>
    /// <param name="arrivedAt">Timestamp indicating when the model was fetched or created.</param>
    Task Store(object id, object model, long arrivedAt);

    /// <summary>
    /// Caches multiple model instances, each keyed by its own id.
    /// </summary>
    /// <param name="results">The model instances to cache.</param>
    /// <param name="arrivedAt">Timestamp indicating when the models were fetched or created.</param>
    Task StoreAll(IReadOnlyList<object> results, long arrivedAt);
    
    /// <summary>
    /// Stores a collection of model ids under a specific key.
    /// </summary>
    /// <param name="key">String key under which the collection of ids will be stored.</param>
    /// <param name="ids">The collection of identifiers to store.</param>
    /// <param name="aArrivedAt">Timestamp indicating when the collection was fetched or created.</param>
    Task StoreCollection(string key, IEnumerable ids, long aArrivedAt);

    /// <summary>
    /// Fetches data based on a request operation.
    /// </summary>
    /// <param name="requestOp">The operation describing what data should be fetched.</param>
    /// <returns>An OpResponse containing the results of the fetch operation</returns>
    Task<OpResponse> Fetch(RequestOp requestOp);

    /// <summary>
    /// Removes a model instance from the cache for a given id
    /// </summary>
    /// <param name="id">The identifier of the model to be removed from the cache.</param>
    Task Remove(object id);

    /// <summary>
    /// Clears all cached items, with options to preserve certain data based on flags and timestamps.
    /// </summary>
    /// <param name="exceptHeld">If true, items marked as 'held' will not be cleared.</param>
    /// <param name="olderThan">Optional: if provided, only items older than this date will be cleared.</param>
    Task ClearAll(bool exceptHeld, DateTime? olderThan = null);
  }
}

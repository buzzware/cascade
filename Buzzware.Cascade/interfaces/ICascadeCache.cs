using System;
using System.Collections;
using System.Threading.Tasks;

namespace Buzzware.Cascade {

  /// <summary>
  /// Interface for one cache layer
  /// </summary>
	public interface ICascadeCache {

    /// <summary>
    /// Fetches a cached response for a given request operation.
    /// </summary>
    /// <param name="requestOp">The operation representing the request, which is used to locate the cached value</param>
    /// <returns>The operation response</returns> 
		Task<OpResponse> Fetch(RequestOp requestOp);
		
    /// <summary>
    /// Stores the given operation response in the cache.
    /// </summary>
    /// <param name="opResponse">The response from an operation that includes data to be cached.</param>
		Task Store(OpResponse opResponse);
		
    /// <summary>
    /// Stores a model in the cache.
    /// </summary>
    /// <param name="type">The type of the model to store.</param>
    /// <param name="id">The unique identifier for the model being cached.</param>
    /// <param name="model">The actual model object to be cached.</param>
    /// <param name="arrivedAt">Timestamp of when the model data arrived from the origin</param>
		Task Store(Type type, object id, object model, long arrivedAt);

    /// <summary>
    /// Stores a collection of model ids in the cache.
    /// </summary>
    /// <param name="type">The type of models in the collection to be cached.</param>
    /// <param name="key">A string key to reference the cached collection.</param>
    /// <param name="ids">The identifiers of the models contained in the collection.</param>
    /// <param name="arrivedAt">Timestamp indicating when this collection was cached.</param>
		Task StoreCollection(Type type, string key, IEnumerable? ids, long arrivedAt);

    /// <summary>
    /// Cascade reference
    /// </summary>
		CascadeDataLayer Cascade { get; set; }

    /// <summary>
    /// Clears stored data from the cache.
    /// Removes all entries currently held except those that need to be held.
    /// Optionally clears entries older than a specified DateTime.
    /// </summary>
    /// <param name="exceptHeld">If true, entries marked to be held will not be cleared.</param>
    /// <param name="olderThan">If specified, only entries older than this date will be cleared.</param>
		Task ClearAll(bool exceptHeld = true, DateTime? olderThan = null);
	}
}

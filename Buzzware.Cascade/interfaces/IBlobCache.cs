using System;
using System.Threading.Tasks;

namespace Buzzware.Cascade {

  /// <summary>
  /// Defines a cache for storing and retrieving blob data in a Cascade data layer.
  /// </summary>
  public interface IBlobCache {
  
    /// <summary>
    /// the Cascade instance this cache is associated with.
    /// </summary>
    CascadeDataLayer Cascade { get; set; }

    /// <summary>
    /// Clears all entries from the cache, with options to retain certain entries.
    /// </summary>
    /// <param name="exceptHeld">Indicates whether to retain entries held in cache even after clearing.</param>
    /// <param name="olderThan">Optional parameter specifying a date. Clears only entries older than this date.</param>
    Task ClearAll(bool exceptHeld, DateTime? olderThan);

    /// <summary>
    /// Attempts to retrieve an operation response from the cache based on a request operation.
    /// </summary>
    /// <param name="requestOp">The request operation used to query the cache.</param>
    /// <returns>An OpResponse if found in the cache; otherwise, may generate a cache miss.</returns>
    Task<OpResponse> Fetch(RequestOp requestOp);

    /// <summary>
    /// Stores a specific operation response in the cache.
    /// </summary>
    /// <param name="opResponse">The operation response to store in the cache.</param>
    Task Store(OpResponse opResponse);
  }
}

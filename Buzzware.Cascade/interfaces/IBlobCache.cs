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
    /// <returns>An OpResponse containing the blob data when found and fresh enough, otherwise a none/empty response indicating a cache miss.</returns>
    Task<OpResponse> Fetch(RequestOp requestOp);

    /// <summary>
    /// Stores a specific operation response in the cache.
    /// </summary>
    /// <param name="opResponse">The operation response to store in the cache.</param>
    /// <returns>The given opResponse, possibly with a modified result reflecting how it was stored.</returns>
    Task<OpResponse> Store(OpResponse opResponse);

    /// <summary>
    /// Notifies the cache that the blob at the given path is fresh as of the given arrival time, so its arrival time should be updated.
    /// </summary>
    /// <param name="blobPath">The relative path of the blob to mark as fresh.</param>
    /// <param name="arrivedAtMs">The arrival time to set, in ms since 1970.</param>
    Task NotifyBlobIsFresh(string blobPath, long arrivedAtMs);

    /// <summary>
    /// Is this able to provide file paths for blobs ?
    /// </summary>
    bool SupportsGetAbsoluteFilePath { get; }
    
    /// <summary>
    /// Get the absolute file system path for a given blob path
    /// </summary>
    /// <param name="blobPath">The relative path of the blob.</param>
    /// <returns>The absolute file system path where the blob is stored.</returns>
    string GetAbsoluteFilePath(string blobPath);

    /// <summary>
    /// Removes the blob at the given path from the cache.
    /// </summary>
    /// <param name="blobPath">The relative path of the blob to remove.</param>
    Task Clear(string blobPath);
  }
}

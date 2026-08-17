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
    /// <returns>The given opResponse, possibly modified by the cache.</returns>
		Task<OpResponse> Store(OpResponse opResponse);
		
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
    /// Is this cache able to store and retrieve blobs ?
    /// </summary>
		public bool SupportsBlobs { get; }
    
    /// <summary>
    /// Is this cache setup to hold blobs as files, and can return an absolute file system path given a blob path
    /// </summary>
		bool SupportsGetBlobAbsoluteFilePath { get; }
    
    /// <summary>
    /// Given a blob path (always relative) return an absolute local file system path for that blob
    /// </summary>
    /// <param name="blobPath">The relative path of the blob.</param>
    /// <returns>The absolute local file system path for the blob, or null if unsupported.</returns>
		string? GetBlobAbsoluteFilePath(string blobPath);

		/// <summary>
    /// Clears stored data from the cache.
    /// Removes all entries currently held except those that need to be held.
    /// Optionally clears entries older than a specified DateTime.
    /// </summary>
    /// <param name="exceptHeld">If true, entries marked to be held will not be cleared.</param>
    /// <param name="olderThan">If specified, only entries older than this date will be cleared.</param>
		Task ClearAll(bool exceptHeld = true, DateTime? olderThan = null);
    
    /// <summary>
    /// Clears cached data for the given model type only.
    /// </summary>
    /// <param name="type">The model type whose cached data should be cleared.</param>
    /// <param name="exceptHeld">If true, entries marked to be held will not be cleared.</param>
    /// <param name="olderThan">If specified, only entries older than this date will be cleared.</param>
    Task ClearByType(Type type, bool exceptHeld = true, DateTime? olderThan = null);
    
    /// <summary>
    /// Clears all blobs from the cache.
    /// </summary>
    /// <param name="exceptHeld">If true, blobs marked to be held will not be cleared.</param>
    /// <param name="olderThan">If specified, only blobs older than this date will be cleared.</param>
    Task ClearBlobs(bool exceptHeld = true, DateTime? olderThan = null);
    
    /// <summary>
    /// Clears the blob at the given path from the cache.
    /// </summary>
    /// <param name="path">The relative path of the blob to clear.</param>
    Task ClearBlob(string path);
    
    /// <summary>
    /// Set the ArrivedAtMs value for the given blobPath to the given value.
    /// This is not absolutely necessary but for maximum efficiency should be implemented.
    /// It is only used when eTags indicate that a local blob is still fresh.
    /// </summary>
    /// <param name="blobPath">The relative path of the blob to mark as fresh.</param>
    /// <param name="arrivedAtMs">The arrival time to set, in ms since 1970.</param>
		Task NotifyBlobIsFresh(string blobPath, long arrivedAtMs);
	}
}

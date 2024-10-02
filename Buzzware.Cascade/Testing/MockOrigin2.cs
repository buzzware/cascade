using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Buzzware.Cascade.Test;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade.Testing {

  /// <summary>
  /// MockOrigin2 offers a mock implementation of the ICascadeOrigin interface for testing purposes.
  /// It simulates network operations and maintains an in-memory store for blob operations.
  /// </summary>
  public class MockOrigin2 : MockOrigin, ICascadeOrigin {

    /// <summary>
    /// Counts the number of requests processed by the origin.
    /// </summary>
    public int RequestCount { get; protected set; }

    private readonly Dictionary<Type,IModelClassOrigin> classOrigins;
    private readonly Dictionary<string,byte[]> blobs;
    public readonly FriendlyDictionary<string,string> ETags;
    
    /// <summary>
    /// Simulates offline behavior when set to true, throwing a NoNetworkException during request processing.
    /// </summary>
    public bool ActLikeOffline { get; set; }

    /// <summary>
    /// MockOrigin2 Constructor for initializing the class with a set of class origins and an optional timestamp.
    /// </summary>
    /// <param name="classOrigins">Dictionary associating type information with model class origins.</param>
    /// <param name="nowMs">The current timestamp in milliseconds. Defaults to 1000.</param>
    public MockOrigin2(
      Dictionary<Type, IModelClassOrigin> classOrigins, 
      long nowMs = 1000
    ) {
      NowMs = nowMs;
      this.classOrigins = classOrigins;
      blobs = new Dictionary<string, byte[]>();
      ETags = new FriendlyDictionary<string, string>();
      foreach (var pair in classOrigins) {
        pair.Value.Origin = this;
      }
    }

    /// <summary>
    /// Processes a request operation, simulating different operations depending on the type of request and connection status.
    /// </summary>
    /// <param name="request">The request operation to process.</param>
    /// <param name="connectionOnline">Flag indicating if the connection is online or simulated to be offline.</param>
    /// <returns>An OpResponse representing the result of the operation.</returns>
    /// <exception cref="NoNetworkException">Thrown if ActLikeOffline is true simulating a network failure.</exception>
    public override async Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline) {

      RequestCount += 1;

      if (ActLikeOffline)
        throw new NoNetworkException();
      
      object? result = null;
      string? etag = null;

      // Handling blob operations
      
      if (request.Verb == RequestVerb.BlobGet) {
        var path = (string)request.Id!;
        if (request.ETag != null && (request.ETag == ETags[path])) {
          result = null;
          etag = ETags[path];
        } else {
          result = await BlobGet(path);
          etag = ETags[path];
        }
      } else if (request.Verb == RequestVerb.BlobPut) {
        var path = (string)request.Id!;
        result = await BlobPut(path,(request.Value as byte[]));
        etag = ETags[path] = request.ETag;
      } else if (request.Verb == RequestVerb.BlobDestroy) {
        var path = (string)request.Id!;
        await BlobDestroy(path);
        ETags[path] = null;
        result = null;
      } else {
        // Handling class origin operations
        var co = classOrigins[request.Type];
        switch (request.Verb) {
          case RequestVerb.Query:
            result = await co.Query(request.Criteria);
            break;
          case RequestVerb.Get:
            result = await co.Get(request.Id);
            break;
          case RequestVerb.Create:
            result = await co.Create(request.Value!);
            break;
          case RequestVerb.Update:
            if (request != null)
              result = await co.Update(
                request.Id,
                ((IDictionary<string, object?>)request.Value)!,
                request.Extra
              );
            break;
          case RequestVerb.Replace:
            result = await co.Replace(request.Value!);
            break;
          case RequestVerb.Destroy:
            await co.Destroy(request.Value!);
            break;
          default:
            throw new NotImplementedException();
        }
      }
      return new OpResponse(
        request,
        NowMs,
        true,
        NowMs,
        result,
        eTag: etag
      ) {
        SourceName = this.GetType().Name
      };
    }

    /// <summary>
    /// Deletes a blob associated with the given path from the in-memory store.
    /// </summary>
    /// <param name="path">The path associated with the blob to be deleted.</param>
    private async Task BlobDestroy(string path) {
      blobs.Remove(path);
    }

    /// <summary>
    /// Adds or updates a blob value associated with a specific path in the in-memory store.
    /// </summary>
    /// <param name="path">The path to associate with the blob value.</param>
    /// <param name="value">The blob data to store. If null, the blob is removed.</param>
    /// <returns>The blob data that was added or updated, or null if removed.</returns>
    private async Task<byte[]?> BlobPut(string path, byte[]? value) {
      if (value == null)
        blobs.Remove(path);
      else
        blobs[path] = value;
      return value;
    }

    /// <summary>
    /// Retrieves the blob data associated with a specified path from the in-memory store.
    /// </summary>
    /// <param name="path">The path associated with the desired blob.</param>
    /// <returns>The blob data if found, otherwise null.</returns>
    private async Task<byte[]?> BlobGet(string path) {
      if (!blobs.TryGetValue(path, out var result))
        return null;
      return result;
    }

    /// <summary>
    /// Looks up the type associated with a given type name from the class origins.
    /// </summary>
    /// <param name="typeName">The full name of the type to look up.</param>
    /// <returns>The Type matching the specified type name.</returns>
    /// <exception cref="TypeLoadException">Thrown if the type name is not found in the class origins.</exception>
    public override Type LookupModelType(string typeName) {
      foreach (var co in classOrigins) {
        if (co.Key.FullName == typeName)
          return co.Key;
      }
      throw new TypeLoadException($"Type {typeName} not found in origin");
    }

    /// <summary>
    /// Retrieves a model of type M by its identifier.
    /// </summary>
    /// <typeparam name="M">The type of the model to retrieve.</typeparam>
    /// <param name="id">The identifier of the model to retrieve.</param>
    /// <returns>An instance of type M if found, otherwise null.</returns>
    public async Task<M?> Get<M>(object id) where M : SuperModel {
      var co = classOrigins[typeof(M)] as MockModelClassOrigin<M>;
      var model = (await co?.Get(id)) as M;
      return model;
    }

    // public CascadeDataLayer Buzzware.Cascade { get; set; } 
    //
    // public long NowMs { get; set; }
    //
    // public long IncNowMs(long incMs=1000) {
    //  return NowMs += incMs;
    // }
    //
    // public Task<OpResponse> ProcessRequest(RequestOp request) {
    //  if (HandleRequest != null)
    //    return HandleRequest(this,request);
    //  throw new NotImplementedException("Attach HandleRequest or override this");
    // }
  }
}

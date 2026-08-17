using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
    /// <summary>
    /// Maps blob paths to their ETag values, for simulating ETag-based caching of blob requests.
    /// </summary>
    public readonly FriendlyDictionary<string,string> ETags;
    
    /// <summary>
    /// Simulates offline behavior when set to true, causing ProcessRequest to throw
    /// OriginAccessFailure (connection online) or DataNotAvailableOffline (connection offline).
    /// </summary>
    public bool ActLikeOffline { get; set; }

    /// <summary>
    /// MockOrigin2 Constructor for initializing the class with a set of class origins and an optional timestamp.
    /// </summary>
    /// <param name="classOrigins">Dictionary associating type information with model class origins. Each class origin's Origin property is set to this instance.</param>
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
    /// <exception cref="OriginAccessFailure">Thrown if ActLikeOffline is true and connectionOnline is true.</exception>
    /// <exception cref="DataNotAvailableOffline">Thrown if ActLikeOffline is true and connectionOnline is false.</exception>
    public override async Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline) {

      RequestCount += 1;

      if (ActLikeOffline) {
        if (connectionOnline)
          throw new OriginAccessFailure();
        else
          throw new DataNotAvailableOffline();
      }
      
      object? result = null;
      string? etag = null;

      // Handling blob operations
      bool? exists = null;
      if (request.Verb == RequestVerb.BlobGet || request.Verb == RequestVerb.BlobGetFilePath) {
        var path = (string)request.Id!;
        if (request.ETag != null && (request.ETag == ETags[path])) {
          result = null;
          exists = true;
          etag = ETags[path];
        } else {
          result = await BlobGet(path);
          etag = ETags[path];
        }
      } else if (request.Verb == RequestVerb.BlobPut) {
        var path = (string)request.Id!;
        byte[]? bytes;
        if (request.Value is byte[] b)
          bytes = b;
        else if (request.Value is Stream stream)
          bytes = await CascadeUtils.BytesFromStreamAsync(stream);
        else
          bytes = null;
        result = await BlobPut(path, bytes);
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
            result = await co.Query(request.Criteria, request);
            break;
          case RequestVerb.Get:
            result = await co.Get(request.Id, request);
            break;
          case RequestVerb.Create:
            result = await co.Create(request.Value!, request);
            break;
          case RequestVerb.Update:
            if (request != null)
              result = await co.Update(
                request.Id,
                ((IDictionary<string, object?>)request.Value)!,
                request.Extra,
                request
              );
            break;
          case RequestVerb.Replace:
            result = await co.Replace(request.Value!, request);
            break;
          case RequestVerb.Destroy:
            await co.Destroy(request.Value!, request);
            break;
          default:
            throw new NotImplementedException();
        }
      }
      exists ??= result!=null;
      return new OpResponse(
        request!,
        NowMs,
        exists!.Value,
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
      var model = (await co?.Get(id, null)) as M;
      return model;
    }

    /// <summary>
    /// Stores the given model in the matching MockModelClassOrigin, keyed by its cascade id.
    /// </summary>
    /// <typeparam name="M">The type of the model to store.</typeparam>
    /// <param name="model">The model to store.</param>
    public async Task Put<M>(M model) where M : SuperModel {
      var id = CascadeTypeUtils.GetCascadeId(model);
      var co = classOrigins[typeof(M)] as MockModelClassOrigin<M>;
      await co!.Store(id, model);
    }
  }
}

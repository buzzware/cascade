using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Buzzware.Cascade {

  /// <summary>
  /// Represents the response of an operation request in the Cascade library.
  /// This class encapsulates information about the operation's result, 
  /// its connection status, elapsed time, and more.
  /// </summary>
  public class OpResponse {
    /// <summary>
    /// Constructs an instance of OpResponse with the given parameters.
    /// </summary>
    /// <param name="requestOp">The original request operation.</param>
    /// <param name="timeMs">The time in milliseconds when the operation took place.</param>
    /// <param name="exists">Indicates if the item exists in the cache or at origin.</param>
    /// <param name="arrivedAtMs">The time in milliseconds when the response arrived.</param>
    /// <param name="result">The result of the operation, can be null.</param>
    /// <param name="eTag">Entity tag (version identifier) associated with the result, if any.</param>
    [ImmutableObject(true)]
    public OpResponse(
      RequestOp requestOp,
      long timeMs,
      bool exists,
      long? arrivedAtMs,
      object? result,
      string? eTag = null
    ) {
      RequestOp = requestOp;
      TimeMs = timeMs;
      Exists = exists;
      ArrivedAtMs = arrivedAtMs;
      Result = result;
      ETag = eTag;
    }

    /// <summary>
    /// Creates a new OpResponse with updated fields if given, otherwise base fields remain the same.
    /// SourceName and LayerIndex are copied from this instance.
    /// </summary>
    /// <param name="requestOp">Updated request operation.</param>
    /// <param name="timeMs">Updated time in milliseconds.</param>
    /// <param name="exists">Updated existence status.</param>
    /// <param name="arrivedAtMs">Updated arrival time in milliseconds.</param>
    /// <param name="result">Updated operation result.</param>
    /// <param name="eTag">Updated entity tag.</param>
    /// <returns>The new OpResponse instance with specified changes.</returns>
    public OpResponse withChanges(
      RequestOp? requestOp = null,
      long? timeMs = null,
      bool? exists = null,
      long? arrivedAtMs = null,
      object? result = null,
      string? eTag = null
    ) {
      return new OpResponse(
        requestOp: requestOp ?? this.RequestOp,
        timeMs: timeMs ?? this.TimeMs,
        exists: exists ?? this.Exists,
        arrivedAtMs: arrivedAtMs ?? this.ArrivedAtMs,
        result: result ?? this.Result,
        eTag: eTag ?? this.ETag
      ) {
        SourceName = this.SourceName,
        LayerIndex = LayerIndex
      };
    }
    
    /// <summary>The original request operation that produced this response</summary>
    public readonly RequestOp RequestOp;
    /// <summary>The time in milliseconds when the operation took place</summary>
    public readonly long TimeMs;
    /// <summary>Indicates if the item exists in the cache or at origin</summary>
    public readonly bool Exists; // Indicates if the item exists in the cache or at origin
    /// <summary>The result value of the operation, if any</summary>
    public readonly object? Result; // Contains result for create, read, update operations
    /// <summary>The time in milliseconds when the result originally arrived from the origin, or null</summary>
    public long? ArrivedAtMs;
    /// <summary>The index of the layer that provided this response</summary>
    public int LayerIndex;
    /// <summary>The name of the source (layer or origin) that provided this response</summary>
    public string? SourceName;
    /// <summary>Entity tag (version identifier) associated with the result, if any</summary>
    public readonly string? ETag;

    /// <summary>
    /// Determines if the operation result is a binary large object (blob).
    /// </summary>
    /// <returns>True if the verb is BlobGet, BlobGetFilePath or BlobPut and the Result is a byte array or Stream.</returns>
    public bool ResultIsBlob() {
      return (RequestOp.Verb == RequestVerb.BlobGet || RequestOp.Verb == RequestVerb.BlobGetFilePath || RequestOp.Verb == RequestVerb.BlobPut) 
             && Result is byte[] or Stream;
    }

    /// <summary>
    /// Determines if the operation result is considered empty.
    /// </summary>
    /// <returns>True if the result is empty or null.</returns>
    public bool ResultIsEmpty() {
      if (Result == null)
        return true;
      IEnumerable<object>? inumerableObj = Result as IEnumerable<object>;
      if (inumerableObj != null)
        return !inumerableObj.GetEnumerator().MoveNext();
      IEnumerable? inumerable = Result as IEnumerable;
      if (inumerable != null)
        return !inumerable.GetEnumerator().MoveNext();
      object[]? objects = Result as object[];
      if (objects != null)
        return objects.Length == 0;
      ICollection? icollection = Result as ICollection;
      if (icollection != null)
        return !icollection.GetEnumerator().MoveNext();
      return false; // There is something present that cannot be identified
    }

    /// <summary>
    /// The Result normalised to a read-only list: enumerables are converted to an immutable array of object,
    /// while blobs and single objects are wrapped in a single-element array.
    /// </summary>
    public IReadOnlyList<object> Results {
      get {
        if (Result == null)
          return ImmutableArray<object>.Empty;
        if (ResultIsBlob()) {
          return (IReadOnlyList<object>)ImmutableArray.Create(Result); // put blob into an array
        } if (CascadeTypeUtils.IsEnumerableType(Result.GetType())) {
          // Convert the result to an immutable array of type object
          return (IReadOnlyList<object>)CascadeTypeUtils.ImmutableArrayOfType(typeof(object), (IEnumerable) Result);
        } else {
          return ImmutableArray.Create(Result);
        }
      }
    }

    /// <summary>
    /// Returns the first item in the results if they are enumerable, otherwise the result itself.
    /// </summary>
    public object? FirstResult => IsEnumerableResults ? (Result as IEnumerable)?.Cast<object>().FirstOrDefault() : Result;

    /// <summary>
    /// Indicates if the results are a collection that is not a blob.
    /// </summary>
    public bool IsEnumerableResults {
      get {
        var type = Result?.GetType();
        if (type == null)
          return false;
        return CascadeTypeUtils.IsEnumerableType(type) && !ResultIsBlob();
      }
    }

    /// <summary>
    /// Indicates if the results are a model type.
    /// </summary>
    public bool IsModelResults => CascadeTypeUtils.IsModel(FirstResult);

    /// <summary>
    /// Indicates if the results are ids
    /// </summary>
    public bool IsIdResults => CascadeTypeUtils.IsId(FirstResult);

    /// <summary>
    /// Provides an IEnumerable interface for the ids found in the results.
    /// </summary>
    public IEnumerable ResultIds {
      get {
        var results = Results.Cast<object>();
        if (!results.Any())
          return results;
        var first = results.FirstOrDefault();
        if (CascadeTypeUtils.IsId(first))
          return results;
        else
          return results.Select(i => i!=null ? CascadeTypeUtils.GetCascadeId(i) : null).ToImmutableArray();
      }
    }

    /// <summary>
    /// Produces a summary string of the serialized result and existence status. Serialization errors are swallowed.
    /// </summary>
    /// <returns>A string containing the JSON-serialized result and the Exists flag.</returns>
    public string ToSummaryString() {
      string? result = null;

      try {
        result = Result==null ? null : JsonSerializer.Serialize(Result);
      }
      catch (Exception e) {
        // swallow errors
      }
      return $"{result} Exists:{Exists}";
    }
    
    /// <summary>
    /// Constructs an OpResponse indicating that there was no value found
    /// </summary>
    /// <param name="requestOp">The original request operation.</param>
    /// <param name="timeMs">Time when the operation took place.</param>
    /// <param name="sourceName">The name of the source that provided this response.</param>
    /// <returns>A new OpResponse with Exists false and a null Result.</returns>
    public static OpResponse None(RequestOp requestOp,long timeMs,string? sourceName = null) {
      var opResponse = new OpResponse(
        requestOp,
        timeMs,
        exists: false,
        arrivedAtMs: null, result: null);
      opResponse.SourceName = sourceName;
      return opResponse;
    }

    /// <summary>
    /// Constructs an OpResponse indicating a connection failure.
    /// </summary>
    /// <param name="requestOp">The original request operation.</param>
    /// <param name="timeMs">Time when the operation attempt took place.</param>
    /// <param name="sourceName">The name of the source that would have provided this response.</param>
    /// <returns>A new OpResponse indicating a connection failure.</returns>
    public static OpResponse ConnectionFailure(RequestOp requestOp,long timeMs,string sourceName = null) {
      var opResponse = new OpResponse(
        requestOp,
        timeMs,
        exists: false,
        arrivedAtMs: null, result: null);
      opResponse.SourceName = sourceName;
      return opResponse;
    }
  }
}

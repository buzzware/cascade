@page implementing_origin Implementing a Custom ICascadeOrigin for Your Server

## Introduction

This guide explains how to create a custom CascadeOrigin class to enable the Cascade 
library to work with any API server and, optionally, handle binary blob storage. This 
class is crucial for adapting a specific server to work seamlessly with Cascade, providing 
a clean interface for the application layer while handling server-specific intricacies.

## Why CascadeOrigin is Required

The CascadeOrigin class serves as an adapter between your specific API server and the Cascade library. It's required because:

1. It abstracts away the details of your API, allowing Cascade applications to work with various backends
2. It provides a place to handle any irregularities or quirks in your server's API.
3. It enables the application layer to be written using a consistent, clean interface

## Minimum Contract: ICascadeOrigin

The CascadeOrigin class must implement the `ICascadeOrigin` interface:

```csharp
public interface ICascadeOrigin {
    // Receives a request and returns a response eg. from a server. This is the main method,
    // receiving all Get/Query/Create/Update/Replace/Destroy/Execute/Blob* operations.
    Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline);

    // Reference to the CascadeDataLayer, set by CascadeDataLayer on construction
    CascadeDataLayer Cascade { get; set; }

    // Current time in milliseconds since 1970 UTC. Cascade uses this for all freshness calculations,
    // so tests can control time by controlling this property.
    long NowMs { get; }

    // Ensures that the session is authenticated, potentially for a specific model type
    Task EnsureAuthenticated(Type? type = null);

    // Retrieves the model type for a full type name (used eg. when deserializing pending changes)
    Type LookupModelType(string typeName);

    // Generates a new GUID string (used eg. for creating string-id models offline)
    string NewGuid();

    // Lists all the model types this origin handles
    IEnumerable<Type> ListModelTypes();
}
```

`ProcessRequest` receives a `RequestOp` whose `Verb` determines the operation, and returns an `OpResponse` with
`Exists`, `Result`, `ArrivedAtMs` and optionally `ETag` set.

> A complete working reference implementation is `MockOrigin2` (with `MockModelClassOrigin`) in the
> `Buzzware.Cascade.Testing` namespace, which is used by the Cascade test suite.

## Recommended Implementation Approach

While not required, it has been a successful approach to :

1. Use an `IModelClassOrigin` implementation (a library interface) for each model class.
2. Use a blob handler (eg. your own IBlobOrigin abstraction) for binary blob handling.
3. Use a `ICascadeOrigin` implementation to combine the above, dispatching on `RequestOp.Verb` and `RequestOp.Type`.

This modular approach allows for easier maintenance and extensibility with minimum repetition, and is the approach documented here.

## Implementation Guide

### 1. Basic Origin Structure

```csharp
public class CustomCascadeOrigin : ICascadeOrigin
{
    private readonly Dictionary<Type, IModelClassOrigin> classOrigins;
    private readonly IBlobOrigin? blobOrigin;    // your own abstraction, see below
    private readonly CascadeJsonSerialization serialization;

    public CustomCascadeOrigin(
        Dictionary<Type, IModelClassOrigin> classOrigins,
        IBlobOrigin? blobOrigin,
        CascadeJsonSerialization serialization)
    {
        this.classOrigins = classOrigins;
        this.blobOrigin = blobOrigin;
        this.serialization = serialization;
        foreach (var pair in classOrigins)
            pair.Value.Origin = this;
    }

    public CascadeDataLayer Cascade { get; set; }
    public long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string NewGuid() => Guid.NewGuid().ToString();
    public IEnumerable<Type> ListModelTypes() => classOrigins.Keys.ToImmutableArray();

    public Type LookupModelType(string typeName) {
        return classOrigins.Keys.First(t => t.FullName == typeName);
    }

    public async Task EnsureAuthenticated(Type? type = null) {
        // implement authentication here as required
    }

    public async Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline) {
        object? result = null;
        IModelClassOrigin? co = null;
        bool exists = false;

        var isBlobVerb = request.Verb is RequestVerb.BlobGet or RequestVerb.BlobGetFilePath or RequestVerb.BlobPut or RequestVerb.BlobDestroy;
        if (!isBlobVerb && !classOrigins.TryGetValue(request.Type!, out co))
            throw new NotImplementedException($"Unknown origin type {request.Type!.FullName}");

        try {
            switch (request.Verb) {
                case RequestVerb.Query:
                    Debug.Assert(request.Key != null, "request.Key != null");
                    result = await co!.Query(request.Criteria, request);
                    exists = (result is IEnumerable e) && e.GetEnumerator().MoveNext();
                    break;
                case RequestVerb.Get:
                    result = await co!.Get(request.Id, request);
                    exists = result != null;
                    break;
                case RequestVerb.Create:
                    result = await co!.Create(request.Value, request);
                    exists = result != null;
                    break;
                case RequestVerb.Replace:
                    result = await co!.Replace(request.Value, request);
                    exists = result != null;
                    break;
                case RequestVerb.Update:
                    result = await co!.Update(request.Id, (IDictionary<string, object?>)request.Value, request.Extra, request);
                    exists = result != null;
                    break;
                case RequestVerb.Destroy:
                    await co!.Destroy(request.Value, request);
                    exists = false;
                    break;
                case RequestVerb.Execute:
                    result = await co!.Execute(request, connectionOnline);
                    exists = true;
                    break;
                case RequestVerb.BlobGet:
                case RequestVerb.BlobGetFilePath:
                    if (blobOrigin == null)
                        throw new NotImplementedException();
                    result = await blobOrigin.BlobGet(request, connectionOnline);   // request.Id is the blob path
                    exists = result != null;
                    break;
                case RequestVerb.BlobPut:
                    if (blobOrigin == null)
                        throw new NotImplementedException();
                    result = await blobOrigin.BlobPut(request, connectionOnline);
                    exists = result != null;
                    break;
                case RequestVerb.BlobDestroy:
                    if (blobOrigin == null)
                        throw new NotImplementedException();
                    await blobOrigin.BlobDestroy(request, connectionOnline);
                    exists = false;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        catch (NotFoundException) {
            exists = false;
        }

        var opResponse = new OpResponse(
            request,
            NowMs,
            exists,
            NowMs,
            result
        );
        opResponse.SourceName = this.GetType().Name;
        return opResponse;
    }    
}
```

### 2. Basic Class Origin Structure

`IModelClassOrigin` is the library interface for the origin of one model class :

```csharp
public interface IModelClassOrigin {
    Task<IEnumerable> Query(object criteria, RequestOp requestOp);
    Task<object?> Get(object id, RequestOp requestOp);
    Task<object> Create(object value, RequestOp requestOp);
    Task<object> Replace(object value, RequestOp requestOp);
    Task<object> Update(object id, IDictionary<string, object?> changes, object? model, RequestOp requestOp);
    Task Destroy(object model, RequestOp requestOp);
    Task EnsureAuthenticated();
    Task ClearAuthentication();
    Task<object?> Execute(RequestOp request, bool connectionOnline);
    ICascadeOrigin Origin { get; set; }
}
```

The following is a very basic example of implementing the core methods for a typical REST-style HTTP API,
using a generic class so one implementation serves every model class. In practice you may want to support
lambda based handlers for customising each method per model class specifically.

```csharp
  public class SimpleClassOrigin<M> : IModelClassOrigin where M : SuperModel  {
    public BaseHttpApiClient HttpApiClient { get; }
    public string ResourcePath { get; }
    public CascadeJsonSerialization Serialization { get; set; }

    public ICascadeOrigin Origin { get; set; }    
    
    public SimpleClassOrigin(
      string resourcePath, 
      BaseHttpApiClient httpApiClient, 
      CascadeJsonSerialization serialization
    ) {
      HttpApiClient = httpApiClient;
      ResourcePath = StringUtils.EnsureEndingSlash(resourcePath);
      Serialization = serialization;      
    }
    
    public virtual async Task<IEnumerable> Query(object criteria, RequestOp requestOp) {
      var criteriaDict = criteria as IDictionary<string, object?>;
      var response = await HttpApiClient.Get(ResourcePath, criteriaDict);
      response.EnsureSuccessStatusCode();
      var s = await response.Content.ReadAsStringAsync();
      var jsonElement = Serialization.DeserializeElement(s);
      var items = Serialization.DeserializeType<IEnumerable<M>>(jsonElement).ToImmutableArray();
      return items;
    }
        
    public virtual async Task<object?> Get(object id, RequestOp requestOp) {
      var url = $"{ResourcePath}{id}";
      var item = await HttpApiClient.GetAs<M>(url);
      return item;
    }

    public virtual async Task<object> Create(object value, RequestOp requestOp) {
      var item = await HttpApiClient.PostAs<M>(ResourcePath, value);
      return item;
    }
    
    public virtual async Task<object> Replace(object value, RequestOp requestOp) {
      var url = ResourcePath + CascadeTypeUtils.GetCascadeId(value).ToString();
      var item = await HttpApiClient.PutAs<M>(url, value);
      return item;
    }

    public virtual async Task<object> Update(object id, IDictionary<string, object?> changes, object? model, RequestOp requestOp) {
      var url = ResourcePath + id.ToString();
      var item = await HttpApiClient.PatchAs<M>(url, changes);
      return item;
    }
    
    public virtual async Task Destroy(object model, RequestOp requestOp) {
      var url = $"{ResourcePath}{CascadeTypeUtils.GetCascadeId(model)}";  
      await HttpApiClient.Delete(url);
    }

    public virtual async Task<object?> Execute(RequestOp request, bool connectionOnline) {
      throw new NotImplementedException();  // implement app specific actions here
    }

    public virtual Task EnsureAuthenticated() => Task.CompletedTask;
    public virtual Task ClearAuthentication() => Task.CompletedTask;
  }
```

Note `BaseHttpApiClient` and `StringUtils` here stand in for your own HTTP client wrapper and utilities - they
are not part of Cascade.

### 3. Implementing Blob Handling

Blob requests arrive at `ICascadeOrigin.ProcessRequest` with the verbs `BlobGet`, `BlobPut` and `BlobDestroy`,
where `RequestOp.Id` is the blob path (a string) and, for BlobPut, `RequestOp.Value` is the data (a `byte[]` or `Stream`).
The result of a BlobGet should be the blob content as a `byte[]` (or `Stream`), or null when it does not exist.

Cascade does not define a blob origin interface - the `IBlobOrigin` used in the example above is an abstraction you
define yourself, typically implemented over an object storage service (eg. Azure Blob Storage or S3) or a file system.

For large blobs, also consider supporting ETags (setting `OpResponse.ETag` and honouring `RequestOp.ETag`) to avoid
repeated downloads of unchanged content - see [Blobs In Depth](#blobs_in_depth).

## Key Considerations

### Authentication

Handle authentication within your API client or individual methods, and implement
`ICascadeOrigin.EnsureAuthenticated` so applications can ensure a valid session up-front:

```csharp
private async Task<string> GetAuthToken()
{
    // Implement authentication logic
}

public virtual async Task<object?> Get(object id, RequestOp requestOp)
{
    var token = await GetAuthToken();
    // Use token in API request
}
```

### Exception Handling

Cascade uses and includes the StandardExceptions library and ErrorControl class. Exceptions thrown (for example, by a HTTP client library) are recognised, filtered and usually wrapped in an appropriate StandardException subclass by a handler registered with ErrorControl.

For example, `Java.Net.UnknownHostException` is wrapped with a `NoNetworkException` by setting the Inner property of the StandardException. This means that the Cascade library can handle a known exception representing the case where an attempt at a network connection has failed, and the original exception is also available if required.

It is important that your origin (or ErrorControl handlers) throw `NoNetworkException` (or a subclass) for network
connection failures, because Cascade specifically catches that to trigger the fallback freshness behaviour -
see [Freshness, Fallback and Time](#freshness_and_fallback).

### Serialization

The included `CascadeJsonSerialization` provides consistent data handling using the dotnet System.Text.Json library,
including only serializing data properties of models (not association properties).

### Online/Offline Behavior

When CascadeDataLayer#ConnectionOnline is false (offline mode), the origin is normally only used to handle Execute requests, as Cascade simulates the other write operations (queueing them as pending changes) and serves reads from the caches. Execute is passed the connectionOnline parameter to conditionally handle online and offline scenarios. Reads marked localOnly are also passed through to the origin when offline, for origins that can answer queries from local data.

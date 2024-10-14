# Implementing a Custom ICascadeOrigin for Your Server

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

The CascadeOrigin class must implement the `ICascadeOrigin` interface, which includes the following methods:

- `Task<T> Get<T>(object id, bool connectionOnline)`
- `Task<IEnumerable<T>> Query<T>(object criteria, bool connectionOnline)`
- `Task<T> Create<T>(T entity, bool connectionOnline)`
- `Task<T> Update<T>(T entity, bool connectionOnline)`
- `Task Delete<T>(T entity, bool connectionOnline)`
- `Task<byte[]> BlobGet(string path, bool connectionOnline)`
- `Task BlobPut(string path, byte[] data, bool connectionOnline)`

## Recommended Implementation Approach

While not required, it has been a successful approach to :

1. Use `IModelClassOrigin<T>` implementations for each model class.
2. Use a `IBlobOrigin` implementation for binary blob handling.
3. Use a `ICascadeOrigin` implementation to combine the above

This modular approach allows for easier maintenance and extensibility with minimum repetition, and is the approach documented here.

## Implementation Guide

### 1. Basic Origin Structure

```csharp
public class CustomCascadeOrigin : ICascadeOrigin
{
    private readonly Dictionary<Type, IModelClassOrigin> _classOrigins;
    private readonly IBlobOrigin _blobOrigin;
    private readonly CascadeJsonSerialization _serialization;

    public CustomCascadeOrigin(
        Dictionary<Type, IModelClassOrigin> classOrigins,
        IBlobOrigin blobOrigin,
        CascadeJsonSerialization serialization)
    {
        _classOrigins = classOrigins;
        _blobOrigin = blobOrigin;
        _serialization = serialization;
    }

    public async Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline) {
        object? result = null;
        IModelClassOrigin? co = null;
        bool exists = false;
        
        if (request.Verb!=RequestVerb.BlobGet && request.Verb!=RequestVerb.BlobPut && request.Verb!=RequestVerb.BlobDestroy && !classOrigins.TryGetValue(request.Type, out co))
            throw new NotImplementedException($"Unknown origin type ${request.Type.FullName}");

        try {
            switch (request.Verb) {
                case RequestVerb.Query:
                    Debug.Assert(request.Key != null, "request.Key != null");
                    result = await co!.Query(request.Criteria);
                    exists = (result is IEnumerable e) && e.GetEnumerator().MoveNext();
                    break;
                case RequestVerb.Get:
                    result = await co!.Get(request.Id);
                    exists = result != null;
                    break;
                case RequestVerb.Create:
                    result = await co!.Create(request.Value);
                    exists = result != null;
                    break;
                case RequestVerb.Replace:
                    result = await co!.Replace(request.Value);
                    exists = result != null;
                    break;
                case RequestVerb.Update:
                    result = await co!.Update(request.Id, (IDictionary<string, object>)request.Value, request.Extra);
                    exists = result != null;
                    break;
                case RequestVerb.Destroy:
                    await co!.Destroy(request.Value);
                    exists = false;
                    break;
                case RequestVerb.Execute:
                    result = await co!.Execute(request,connectionOnline);
                    exists = true;
                    break;
                case RequestVerb.BlobGet:
                    if (blobOrigin==null)
                        throw new NotImplementedException();	
                    result = await blobOrigin.BlobGet(request,connectionOnline);
                    exists = result != null;
                    break;
                case RequestVerb.BlobPut:
                    if (blobOrigin==null)
                        throw new NotImplementedException();	
                    result = await blobOrigin.BlobPut(request,connectionOnline);
                    exists = result != null;
                    break;
                case RequestVerb.BlobDestroy:
                    if (blobOrigin==null)
                        throw new NotImplementedException();	
                    await blobOrigin.BlobDestroy(request,connectionOnline);
                    exists = false;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        catch (NotFoundException e) {
            exists = false;
        }
        catch (StandardException e) when (
            e is NoNetworkException ||
            e is InternalServerErrorException
        ) {
            throw;
        }
        catch (Exception e) {
            throw;
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

```csharp
  public class CivtracSimpleClassOrigin<M> : IModelClassOrigin where M : SuperModel  {
    public BaseHttpApiClient HttpApiClient { get; }
    public string ResourcePath { get; }
    public CascadeJsonSerialization Serialization { get; set; }

    public ICascadeOrigin Origin { get; set; }    
    
    public CivtracSimpleClassOrigin(
      string resourcePath, 
      BaseHttpApiClient httpApiClient, 
      CascadeJsonSerialization serialization,
      Func<CivtracSimpleClassOrigin<M>,RequestOp,bool,Task<object>> executeHandler = null,
      string queryResponsePath = "items"
    ) {
      HttpApiClient = httpApiClient;
      ResourcePath = StringUtils.EnsureEndingSlash(resourcePath);
      Serialization = serialization;      
      ExecuteHandler = executeHandler;
    }
    
    public virtual async Task<IEnumerable> Query(object criteria) {
      var criteriaDict = criteria as IDictionary<string, object>;
      string aUrl = ResourcePath;
      var response = await HttpApiClient.Get (aUrl,criteriaDict);
      response.EnsureSuccessStatusCode();
      var s = await response.Content.ReadAsStringAsync ();
      var jsonElement = AppCommon.Serialization.DeserializeElement(s);
      var ret = AppCommon.Serialization.DeserializeType<IEnumerable<M>>(jsonElement);
      var items = (ret).ToImmutableArray();
      return items;
    }
        
    public virtual async Task<object> Get(object id) {
        var url = $"{ResourcePath}{id}";
        var item = await HttpApiClient.GetAs<M>(url);
        return item;
    }

    public virtual async Task<object> Create(object value) {
      var item = await HttpApiClient.PostAs<M>(ResourcePath, value);
      return item;
    }
    
    public virtual async Task<object> Replace(object value) {
      var url = this.ResourcePath+CascadeTypeUtils.GetCascadeId(value).ToString();
      var item = await HttpApiClient.PutAs<M>(url, value);
      return item;
    }

    public virtual async Task<object> Update(object id, IDictionary<string, object> changes, object model) {
      var url = this.ResourcePath+CascadeTypeUtils.GetCascadeId(value).ToString();
      var item = await HttpApiClient.UpdateAs<M>(url, changes);
      return item;
    }
    
    public virtual async Task Destroy(object model) {
      var url = $"{ResourcePath}{CascadeTypeUtils.GetCascadeId(model)}";  
      await HttpApiClient.Delete(url);
    }    
  }
```




### 2. Implementing Core Methods

The following is a very basic example. In practice you may want to support lambda based handlers for customising each method per model class specifically.

```csharp
    public virtual async Task<IEnumerable> Query(object criteria) {
      var criteriaDict = criteria as IDictionary<string, object>;
      string aUrl = ResourcePath;
      var response = await HttpApiClient.Get (aUrl,criteriaDict);
      response.EnsureSuccessStatusCode();
      var s = await response.Content.ReadAsStringAsync ();
      var jsonElement = AppCommon.Serialization.DeserializeElement(s);
      var ret = AppCommon.Serialization.DeserializeType<IEnumerable<M>>(jsonElement);
      var items = (ret).ToImmutableArray();
      return items;
    }
        
    public virtual async Task<object> Get(object id) {
        var url = $"{ResourcePath}{id}";
        var item = await HttpApiClient.GetAs<M>(url);
        return item;
    }

    public virtual async Task<object> Create(object value) {
      var item = await HttpApiClient.PostAs<M>(ResourcePath, value);
      return item;
    }
    
    public virtual async Task<object> Replace(object value) {
      var url = this.ResourcePath+CascadeTypeUtils.GetCascadeId(value).ToString();
      var item = await HttpApiClient.PutAs<M>(url, value);
      return item;
    }

    public virtual async Task<object> Update(object id, IDictionary<string, object> changes, object model) {
      var url = this.ResourcePath+CascadeTypeUtils.GetCascadeId(value).ToString();
      var item = await HttpApiClient.UpdateAs<M>(url, changes);
      return item;
    }
    
    public virtual async Task Destroy(object model) {
      var url = $"{ResourcePath}{CascadeTypeUtils.GetCascadeId(model)}";  
      await HttpApiClient.Delete(url);
    }
```

### 3. Implementing Blob Handling

```csharp
public async Task<byte[]> BlobGet(string path, bool connectionOnline)
{
    return await _blobOrigin.Get(path, connectionOnline);
}

public async Task BlobPut(string path, byte[] data, bool connectionOnline)
{
    await _blobOrigin.Put(path, data, connectionOnline);
}
```

## Key Considerations

### Authentication

Handle authentication within your API client or individual methods:

```csharp
private async Task<string> GetAuthToken()
{
    // Implement authentication logic
}

public async Task<T> Get<T>(object id) where T : class
{
    var token = await GetAuthToken();
    // Use token in API request
}
```

### Exception Handling

Cascade uses and includes the StandardExceptions library and ErrorControl class. Exceptions thrown (for example, by a HTTP client library) are recognised, filtered and usually wrapped in an appropriate StandardException subclass by a handler registered with ErrorControl.

For example, `Java.Net.UnknownHostException` is wrapped with a `NoNetworkException` by setting the Inner property of the StandardException. This means that the Cascade library can handle a known exception representing the case where an attempt at a network connection has failed, and the original exception is also available if required.

### Serialization

The included `CascadeJsonSerialization` provides consistent data handling using the dotnet System.Text.Json library. 

### Online/Offline Behavior

When CascadeDataLayer#ConnectionOnline is false (offline mode), the origin is only used to handle Execute requests as Cascade simulates the other operations. Execute is passed the connectionOnline parameter to conditionally handle online and offline scenarios.

# Collections in Depth

In Cascade, a collection is an array of ids. At present the `PrimaryKey` of a Cascade model can be typed int, long or string.
So a collection is actually an array of one of those types. This is not often seen by an application because the Query method takes that array and looks up the matching models using GetModelsForIds() to return an array of models.

However there are edge cases when it may be necessary or desirable to use collections directly.

## Collection Requests

```csharp
public async Task<IEnumerable<object>?> GetCollection<M>(
    string collectionName,
    long? sequenceBeganMs = null
)

and 

public Task<OpResponse> GetCollectionResponse<M>(string collectionName, long? sequenceBeganMs = null)
```

Like Get for collections where the collectionName is like the id. Returns an array of ids.

```csharp
public async Task<IEnumerable<M>> GetWhereCollection<M>(
  string propertyName, 
  string propertyValue, 
  int? freshnessSeconds = null, 
  int? populateFreshnessSeconds = null,
  long? sequenceBeganMs = null
)
    
and

public async Task<OpResponse> GetWhereCollectionResponse<Model>(
  string propertyName, 
  string propertyValue, 
  int? freshnessSeconds = null, 
  int? populateFreshnessSeconds = null,
  long? sequenceBeganMs = null
)
```

Get a collection of models where a given property name has the given value.  Used for populating HasMany or HasOne associations. This is specialised form of Query/QueryResponse<Model>()

```csharp
public async Task<IEnumerable<OpResponse>> GetModelsForIds(
  Type type,
  IEnumerable iids,
  int? freshnessSeconds = null,
  int? fallbackFreshnessSeconds = null,
  bool? hold = null,
  long? sequenceBeganMs = null
)
```

Get the models for the given ids.

## Collection Helper methods

The following methods are used to manage collections directly as a list of ids.

```csharp
async Task<IEnumerable<object>> SetCollection<Model>(string collectionName, IEnumerable<object> ids)
```

Replace a collection with a specified set of ids in all cache layers.

```csharp
async Task<IEnumerable<object>> CollectionPrepend<Model>(string collectionName, object id)
```

Add a single id to the beginning of a collection in all cache layers.

```csharp
async Task<IEnumerable<object>> CollectionAppend<Model>(string collectionName, object id)
```

Add a single id to the end of a collection in all cache layers.

```csharp
async Task ClearCollection<Model>(string collectionName)
```

Clear a collection from all cache layers for the specified collection type.

```csharp
async Task SetCacheWhereCollection(Type modelType, string propertyName, string propertyValue, IEnumerable<object> collection)
```

Replace a where collection in all Cascade caches with the given array of ids  


@page getting_models Getting Models

Cascade provides a straightforward method to retrieve specific individual models from your data origin. 

## Basic Usage

The primary method for retrieving a single model is the `Get` method. It is provided in overloads for int, long and string ids:

```csharp
public async Task<M?> Get<M>(
    int id,                                  // also string id and long id overloads
    IEnumerable<string>? populate = null,
    int? freshnessSeconds = null,
    int? populateFreshnessSeconds = null,
    int? fallbackFreshnessSeconds = null,
    bool? hold = null,
    long? sequenceBeganMs = null
) where M : class
```

### Parameters

- `M`: The type of model you're retrieving.
- `id`: The unique identifier of the model you want to fetch.
- `populate`: Optional association property names to populate on the result - see [Populating Associations](#populating).
- `freshnessSeconds` / `populateFreshnessSeconds` / `fallbackFreshnessSeconds`: Optional cache freshness requirements - see [Freshness, Fallback and Time](#freshness_and_fallback).
- `hold`: Optionally mark the model (and populated associations) to be held in caches.
- `sequenceBeganMs`: Optional common request time for a sequence of related requests, for optimal caching.

### Return Value

The method returns a `Task<M?>`, where `M` is the type of model you're retrieving.

There is also a `GetResponse` variant that returns the full `OpResponse` detail of the operation.

## Example Usage

Here's a simple example of how to use the `Get` method to retrieve a Docket:

```csharp
public async Task<Docket> GetDocket(string docketId)
{
    Docket docket = await AppCommon.Cascade.Get<Docket>(docketId);
    return docket;
}
```

## Null Results

If no model is found with the given ID, the `Get` method will return `null`.

```csharp
Docket docket = await AppCommon.Cascade.Get<Docket>("non-existent-id");
if (docket == null)
{
    Console.WriteLine("Docket not found");
}
```

## Using Get with Different ID Types

The `Get` method can work with string, int and long ID types, depending on how your models are set up. For example:

```csharp
// Using a string ID
User user = await AppCommon.Cascade.Get<User>("user123");

// Using an integer ID
Product product = await AppCommon.Cascade.Get<Product>(42);
```

## Best Practices

1. Use `Get` when you need to retrieve a specific model and you know its ID.
2. Always check for null results when using `Get`, especially if there's a possibility that the model might not exist.
3. Use the `populate` option when you know you will need association properties, rather than accessing them and hoping they are set.
4. Choose a `freshnessSeconds` value appropriate to how stale the data can acceptably be, rather than always insisting on fresh data.

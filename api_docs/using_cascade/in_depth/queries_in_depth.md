@page queries_in_depth Queries In Depth 

Every successful query results in a collection, and so it is worth reading [Collections in Depth](#collections_in_depth) before this page.

## Query Method Overview

The signature of the Query method is:

```csharp
public async Task<IEnumerable<M>> Query<M>(
    string? collectionKey,
    object? criteria = null,
    IEnumerable<string>? populate = null,
    int? freshnessSeconds = null,
    int? populateFreshnessSeconds = null,
    int? fallbackFreshnessSeconds = null,
    bool? hold = null,
    long? sequenceBeganMs = null,
    bool? localOnly = null
)
```

There are also `QueryOne<M>` (returns the first result or null) and `QueryResponse<M>` (returns the full OpResponse) variants.

## Collection Creation and Naming

The first time you call the Query method with a model type, collection key and criteria, the query is executed on the origin and then the resulting ids are stored in all cache layers as a collection under the given collection key (the models themselves are also stored individually).

On future calls with the same model type and collection key, if a cached collection meets the freshness requirement, its ids are used and the models are then retrieved (from cache wherever possible) - avoiding an origin request.

**The collection key alone (per model type) identifies the cached collection - the criteria is not part of the cache key.** It is the application's responsibility to use a distinct collection key for each distinct query. If you use the same key with different criteria, a cached collection from one criteria could wrongly satisfy a query for another. A simple approach is to build the key from the criteria values, eg. :

```csharp
var criteria = new Dictionary<string, object?> { ["Status"] = "Active", ["Category"] = "Electronics" };
var results = await cascade.Query<Product>($"Products__Active__Electronics", criteria);
```

(`CascadeUtils.HashDictionaryStable()` can help by producing a stable hash string from a criteria dictionary.)

Passing a null collectionKey executes the query on the origin every time and does not read or write any cached collection.

## Criteria Processing

The `criteria` object is passed through to the ICascadeOrigin implementation unchanged, and Cascade attaches no meaning to it. The origin is responsible for translating the criteria into a format that the underlying data source can understand. For example, if the Origin is a RESTful API, the criteria might be converted into query parameters. If it's a SQL database, the criteria could be translated into WHERE clauses.

## Freshness and Result Caching

The `freshnessSeconds` parameter plays a vital role in determining whether to execute the query or return cached results:

1. If a collection with this key exists in a cache and is fresh (based on `freshnessSeconds`), Cascade returns the models for its cached ids without executing the query against the Origin. The models themselves are then allowed to come from cache at any age - the assumption is that a fresh-enough collection implies acceptable member models, and this avoids origin requests when offline or the network fails.

2. If the collection doesn't exist or is stale, Cascade executes the query against the Origin, stores the resulting models individually plus the id collection under the key, and then returns the models.

This mechanism allows for efficient caching of query results, reducing unnecessary network calls and database load.

## Query Execution Flow

1. App or Cascade calls Query with model type, collection key and criteria
2. Cascade checks the cache layers in order for a collection with this key that is fresh enough.
3. If a fresh collection is found, it gets the models for its ids (from caches where possible) and returns them.
4. If not, it executes the query through the ICascadeOrigin implementation.
5. The Origin applies the criteria to filter the data and returns the matching models.
6. Cascade receives the results, stores the models and the id collection in the cache layers, populates any requested associations, and returns the models to the caller.

## Example Scenario

Let's walk through an example:

```csharp
var criteria = new Dictionary<string, object?> { ["Department"] = "Sales", ["MinSalary"] = 50000 };
var employees = await cascade.Query<Employee>(
    "EmployeeList__Sales__50000",
    criteria,
    freshnessSeconds: 300 // 5 minutes
);
```

1. Cascade checks if a collection named "EmployeeList__Sales__50000" exists for Employee and was stored less than 5 minutes ago.
2. If so, it returns the cached models for the collection's ids.
3. If not, it sends the query to the Origin.
4. The Origin (e.g. via an API server and database) applies the criteria, filtering for Sales employees with salaries >= 50000.
5. Cascade receives the results, caches them and the collection, and returns them.

## Criteria Flexibility

The `criteria` object can be as simple or complex as needed. It could be a dictionary, an anonymous object, or a custom class. The ICascadeOrigin implementation is responsible for interpreting this object and applying it to the data source.

For instance, an Origin might support advanced querying features:

```csharp
var criteria = new {
    Department = "Sales",
    Salary = new { Min = 50000, Max = 100000 },
    HireDate = new { After = DateTime.Now.AddYears(-5) }
};
```

The Origin would then translate this into appropriate filtering logic for its data source.

> Note that when queueing pending changes offline, criteria is serialized with the request, so serializable
> criteria types such as dictionaries of simple values are the safest choice.

## localOnly

The optional `localOnly` parameter marks the request as one to be satisfied locally. When offline, a localOnly query
is passed to the origin (marked localOnly with connectionOnline false) instead of throwing DataNotAvailableOffline -
supporting origin implementations that can answer queries from local data. Cached model records are not re-stored
from localOnly query results, so their original arrival times (and therefore freshness) are preserved.

## Conclusion

The Query method in Cascade provides a powerful and flexible way to retrieve data, balancing efficiency through caching with the need for fresh data. By caching result ids as a named collection and leveraging the ICascadeOrigin for actual data retrieval, Cascade offers a robust solution for querying data in various scenarios.

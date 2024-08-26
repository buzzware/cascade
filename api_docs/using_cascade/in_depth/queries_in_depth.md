@page queries_in_depth Queries In Depth 

# Queries In Depth

Every successful query results in a collection, and so it is worth reading [Collections in Depth](collections_in_depth.md) before this page.

## Query Method Overview

The basic signature of the Query method is:

```csharp
public async Task<IEnumerable<M>> Query<M>(
    string collectionName,
    object criteria,
    IEnumerable<string> populate = null,
    int? freshnessSeconds = null,
    int? populateFreshnessSeconds = null,
    bool? hold = null
) where M : class
```

## Collection Creation and Naming

The first time you call the Query method with a model type, collection name and criteria, the query is executed on the origin and then the resulting ids are stored in all cache layers using the collection name.

On future calls, the collection name is used to retrieve any past results that match the model type, collection name and criteria. If freshness requirements are met, the cached value is returned.

When you call the Query method, Cascade creates or uses an existing collection to store the results. The collection name is a combination of the provided `collectionName` parameter and a hash of the `criteria` object. This approach ensures that different query criteria result in distinct collections, even if they share the same base collection name.

For example:

```csharp
var criteria = new { Status = "Active", Category = "Electronics" };
var results = await cascade.Query<Product>("ProductList", criteria);
```

Internally, Cascade might generate a collection name like "ProductList_hash(criteria)", where the hash is derived from the criteria object.

## Criteria Processing

The `criteria` object is serialized and becomes part of the collection name. This is crucial for distinguishing between different queries and their respective result sets. The actual interpretation and application of the criteria are handled by the ICascadeOrigin implementation.

The ICascadeOrigin is responsible for translating the criteria into a format that the underlying data source can understand. For example, if the Origin is a RESTful API, the criteria might be converted into query parameters. If it's a SQL database, the criteria could be translated into WHERE clauses.

## Freshness and Result Caching

The `freshnessSeconds` parameter plays a vital role in determining whether to execute the query or return cached results:

1. If the collection exists and is fresh (based on `freshnessSeconds`), Cascade returns the cached results without executing the query against the Origin.

2. If the collection doesn't exist or is stale, Cascade executes the query against the Origin, stores the results in the collection, and then returns them.

This mechanism allows for efficient caching of query results, reducing unnecessary network calls and database load.

## Query Execution Flow

1. App or Cascade calls Query with model type, collection name and criteria
2. Cascade checks if a collection with this name exists and is fresh enough.
3. If the collection is fresh, it returns the cached results.
4. If not, it executes the query through the ICascadeOrigin implementation.
5. The Origin applies the criteria to filter the data.
6. Cascade receives the results, stores them in the cache layers with the collection name, and returns them to the caller.

## Example Scenario

Let's walk through an example:

```csharp
var criteria = new { Department = "Sales", MinSalary = 50000 };
var employees = await cascade.Query<Employee>(
    "EmployeeList",
    criteria,
    freshnessSeconds: 300 // 5 minutes
);
```

1. Cascade generates a collection name, e.g., "EmployeeList_hash(criteria)".
2. It checks if this collection exists and was last updated less than 5 minutes ago.
3. If so, it returns the cached results.
4. If not, it sends the query to the Origin.
5. The Origin (e.g., a database) applies the criteria, filtering for Sales employees with salaries >= 50000.
6. Cascade receives the results, caches them in the collection, and returns them.

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

## Conclusion

The Query method in Cascade provides a powerful and flexible way to retrieve data, balancing efficiency through caching with the need for fresh data. By incorporating the criteria into the collection name and leveraging the ICascadeOrigin for actual data retrieval, Cascade offers a robust solution for querying data in various scenarios.

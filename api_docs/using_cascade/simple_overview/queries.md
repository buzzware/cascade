@page queries Simple Querying with Cascade

Cascade provides a straightforward way to query models from your data source. This guide will walk you through the process of setting up and executing queries to retrieve lists of models.

## Basic Query

To query models using Cascade, you typically follow these steps:

1. Define your query criteria
2. Execute the query with a collection name
3. Handle the results

### Defining Query Criteria

Query criteria are defined using a dictionary of key-value pairs. For example:

```csharp
var criteria = new Dictionary<string, object?>
{
    ["FilterText"] = "SearchTerm",
    ["Status"] = "Active"
};
```

The interpretation of the criteria is entirely up to your `ICascadeOrigin` implementation - Cascade passes it
through unchanged. See [Queries In Depth](#queries_in_depth).

### Executing the Query

To execute a query:

```csharp
IEnumerable<Docket> dockets = await AppCommon.Cascade.Query<Docket>(
    "ActiveDockets",
    criteria
);
```

The first parameter is the collection name (key) that the resulting collection of ids will be cached under.
Different criteria should be given different collection names, because on later calls a cached collection
of that name will be returned when it meets the freshness requirement.

`Query` also accepts the same optional parameters as `Get` : `populate`, `freshnessSeconds`,
`populateFreshnessSeconds`, `fallbackFreshnessSeconds`, `hold` and `sequenceBeganMs`.

### Handling Results

`Query` returns an `IEnumerable<M>` of immutable model instances (empty when nothing matched).

```csharp
foreach (var docket in dockets)
    Console.WriteLine(docket.description);
```

### Querying a Single Model

To query for a single model, or null :

```csharp
Docket docket = await AppCommon.Cascade.QueryOne<Docket>(
    "MainDocket",
    criteria
);
```

## Best Practices

1. Use a distinct collection name for each distinct query (model type + criteria). A common convention is to build the name from the criteria values.
2. Use the `populate` option to fetch associations for all the results efficiently.
3. Use `freshnessSeconds` to control how often the query is re-run against the origin versus answered from cache.

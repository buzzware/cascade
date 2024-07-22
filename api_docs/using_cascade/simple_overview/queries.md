# Simple Querying with Cascade

Cascade provides a straightforward way to query models from your data source. This guide will walk you through the process of setting up and executing queries to retrieve lists of models.

## Basic Query

To query models using Cascade, you typically follow these steps:

1. Define your query criteria
2. Execute the query
3. Handle the results

### Defining Query Criteria

Query criteria are defined using a dictionary of key-value pairs. For example:

```csharp
var criteria = new Dictionary<string, object>
{
    ["FilterText"] = "SearchTerm",
    ["Status"] = "Active"
};
```

### Executing the Query

To execute a query:

```csharp
IEnumerable<Docket> dockets = await AppCommon.Cascade.Query<Docket>(
    "QueryName",
    criteria
);
```

### Handling Results




### Querying a Single Model

To query for a single model, or null :

```csharp
Docket docket = await AppCommon.Cascade.QueryOne<Docket>(
    "QueryName",
    criteria
);
```

## Best Practices



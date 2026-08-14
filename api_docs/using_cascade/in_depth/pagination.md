@page pagination Pagination with CascadePaginator 

To understand this page, it is first worth reading about [queries](#queries_in_depth) and [collections](#collections_in_depth).

The `CascadePaginator` class provides a powerful mechanism for handling paginated data retrieval in Cascade. This document explains how to use `CascadePaginator`, its internal workings, and how to customize it for specific application needs.

## Overview

`CascadePaginator` is an abstract base class that manages the complexities of paginated queries. It keeps track of queried pages, handles collection naming, and provides methods for querying, clearing, and refreshing paginated data.

## Using CascadePaginator

To use `CascadePaginator`, you need to create a subclass that implements the `AddPaginationToCriteria` method. Here's an example using `MyAppPaginator`:

```csharp
public class MyAppPaginator<Model> : CascadePaginator<Model> where Model : class
{
    public MyAppPaginator(
        CascadeDataLayer cascade,
        object criteria,
        string collectionPrefix,
        int perPage,
        IEnumerable<string> populate = null, 
        int? freshnessSeconds = null,
        int? populateFreshnessSeconds = null,
        bool? hold = null,
        bool localOnly = false
    ) : base(cascade, criteria, collectionPrefix, perPage, populate, freshnessSeconds, populateFreshnessSeconds, hold, localOnly) {
    }

    protected override object AddPaginationToCriteria(object criteria, int page)
    {
        var criteriaClone = ((IDictionary<string,object>)Criteria).ToDictionary(entry => entry.Key, entry => entry.Value);
        criteriaClone["MaxResultCount"] = PerPage;
        criteriaClone["SkipCount"] = page * PerPage;
        return criteriaClone;
    }
}
```

## How It Works

1. **Initialization**: When you create a `MyAppPaginator` instance, you provide the necessary parameters including the `CascadeDataLayer`, criteria, collection prefix, and items per page.

2. **Collection Naming**: For each page, a unique collection name is generated using the format: `CollectionPrefix + "__" + page.ToString("D3")`. Pages are numbered from 0, so the first page of "UserList" is "UserList__000".

3. **Querying**: When you call the `Query` method with a page number:
    - It checks if the query is already in progress to prevent re-entrancy.
    - It adds pagination information to the criteria using `AddPaginationToCriteria`.
    - It calls `Cascade.Query<Model>` with the generated collection name and modified criteria.
    - It updates internal state (HighestPage, LastPageLoaded) based on the results.

4. **State Management**: The paginator keeps track of:
    - Queried pages (using a HashSet)
    - The highest page queried
    - Whether the last page has been loaded (when a query returns fewer items than `PerPage`)

5. **Caching**: The results for each page are cached in Cascade collections, allowing for efficient subsequent retrievals.

## Key Methods

- `Query(int page, int? freshnessSeconds = null)`: Retrieves a specific page of data.
- `QueryAllPages(int? freshnessSeconds = null)`: Queries pages from 0 upward until the last page is reached, returning a list of pages.
- `GetAllCached()`: Returns all pages currently available in the caches (freshness ANY), without any origin requests.
- `HighestPageNumberInCache()`: Returns the highest consecutive page number found in the caches, or null when none.
- `HasAnyPageExpired()`: Returns true when any cached page has passed the configured fallback freshness for the model type.
- `Clear()`: Clears all cached page collections and resets the paginator state.
- `Refresh(int freshnessSeconds)`: Currently a placeholder that does nothing - clear and re-query instead.
- `Prepend(Model newItem)`: Adds a new item's id to the beginning of the first page's collection.
- `CollectionNameForPage(string collectionPrefix, int page)` (static): Constructs the collection name used for a given page.

## Customizing for Your Application

To adapt `CascadePaginator` for your specific needs:

1. Subclass `CascadePaginator<Model>`.
2. Implement `AddPaginationToCriteria` to match your backend's pagination mechanism.
3. Optionally, override other methods if you need custom behavior.

## Example Usage

```csharp
var paginator = new MyAppPaginator<User>(
    AppCommon.Cascade,
    new Dictionary<string,object> { ["Department"] = "Sales" },
    "UserList",
    perPage: 20,
    populate: new[] { "Profile", "Roles" },
    freshnessSeconds: 300
);

var firstPageUsers = await paginator.Query(0);
var secondPageUsers = await paginator.Query(1);
```

In this example, `MyAppPaginator` will create collections named "UserList__000", "UserList__001", etc., each containing up to 20 users from the Sales department.

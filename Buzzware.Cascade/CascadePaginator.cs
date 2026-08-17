using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {

  /// <summary>
  /// The CascadePaginator class is an abstract base class designed to handle
  /// paginated retrieval of model data from a CascadeDataLayer. This class provides
  /// mechanisms to query, clear, and manage state associated with pagination such as
  /// the highest page queried and whether the last page has been fully loaded.
  /// </summary>
  /// <typeparam name="Model">The model class being paginated.</typeparam>
  public abstract class CascadePaginator<Model> where Model : class {
    /// <summary>
    /// Stores a set of queried page numbers to keep track of what has been loaded.
    /// </summary>
    private HashSet<int> queriedPages = new HashSet<int>();

    /// <summary>
    /// Indicates whether the paginator should be held, potentially preventing certain operations.
    /// </summary>
    private bool? Hold;

    /// <summary>
    /// The CascadeDataLayer from which data is queried.
    /// </summary>
    public CascadeDataLayer Cascade { get; }

    /// <summary>
    /// Criteria used to filter the data during queries.
    /// </summary>
    public object Criteria { get; }

    /// <summary>
    /// A prefix used to construct unique collection names associated with each page of data.
    /// </summary>
    public string CollectionPrefix { get; }

    /// <summary>
    /// The number of items to be fetched per page.
    /// </summary>
    public int PerPage { get; }

    /// <summary>
    /// The highest page number that has been successfully queried.
    /// </summary>
    public int HighestPage { get; protected set; } = -1;

    /// <summary>
    /// Indicates whether the last page of the dataset has been fully loaded.
    /// </summary>
    public bool LastPageLoaded { get; protected set; }

    /// <summary>
    /// Indicates whether a query operation is currently in progress.
    /// </summary>
    public bool Loading { get; protected set; }

    /// <summary>
    /// CascadePaginator Constructor
    /// </summary>
    /// <param name="cascade">The CascadeDataLayer object for data operations.</param>
    /// <param name="criteria">Criteria object for filtering data in queries.</param>
    /// <param name="collectionPrefix">Prefix for the collection names for each page.</param>
    /// <param name="perPage">Number of items per page to be retrieved.</param>
    /// <param name="populate">Optional; fields to populate during query.</param>
    /// <param name="freshnessSeconds">Optional; time in seconds for data freshness consideration.</param>
    /// <param name="populateFreshnessSeconds">Optional; time in seconds for population data freshness.</param>
    /// <param name="hold">Optional; indicator to hold paginator operations.</param>
    /// <param name="localOnly">Optional; when true, queries are answered only from the local cache.</param>
    public CascadePaginator(
      CascadeDataLayer cascade,
      object criteria,
      string collectionPrefix,
      int perPage,
      IEnumerable<string>? populate = null,
      int? freshnessSeconds = null,
      int? populateFreshnessSeconds = null,
      bool? hold = null,
      bool localOnly = false
    ) {
      Cascade = cascade;
      Criteria = criteria;
      CollectionPrefix = collectionPrefix;
      PerPage = perPage;
      Populate = populate;
      FreshnessSeconds = freshnessSeconds;
      PopulateFreshnessSeconds = populateFreshnessSeconds;
      Hold = hold;
      LocalOnly = localOnly;
    }

    /// <summary>
    /// When true, queries are answered only from the local cache (no origin request).
    /// </summary>
    public bool LocalOnly { get; protected set; }

    /// <summary>
    /// Time in seconds for considering the freshness of the data queried.
    /// </summary>
    public int? FreshnessSeconds { get; protected set; }

    /// <summary>
    /// Time in seconds for considering the freshness of any populated data.
    /// </summary>
    public int? PopulateFreshnessSeconds { get; protected set; }

    /// <summary>
    /// Fields or relationships to be populated during query operations.
    /// </summary>
    public IEnumerable<string>? Populate { get; protected set; }

    /// <summary>
    /// Constructs a collection name for a given page number by appending it to the CollectionPrefix.
    /// </summary>
    /// <param name="page">The page number for which the collection name is being constructed.</param>
    /// <returns>The constructed collection name including the page number formatted with leading zeros.</returns>
    public string CollectionName(int page) {
      return CollectionNameForPage(CollectionPrefix, page);
    }

    /// <summary>
    /// Checks the cached page collections consecutively from page 0 for expiry against the fallback freshness configured for the Model type.
    /// Stops at the first page with no recorded arrival time.
    /// </summary>
    /// <returns>True if any consecutively cached page collection has expired, otherwise false.</returns>
    public async Task<bool> HasAnyPageExpired() {
      var fallbackSeconds = Cascade.Config.GetFallbackFreshnessSeconds(typeof(Model));
      var nowMs = Cascade.NowMs;
      var i = 0;
      do {
        var arrivedAtMs = await Cascade.GetCollectionArrivedAt<Model>(CollectionName(i++));
        if (arrivedAtMs == null)
          return false;
        if (CascadeUtils.HasArrivedAtExpired(nowMs, arrivedAtMs.Value, fallbackSeconds))
          return true;
      } while (true);
      return false;
    }
    
    /// <summary>
    /// Constructs the collection name for a given prefix and page number, matching the format used internally per page.
    /// </summary>
    /// <param name="collectionPrefix">The collection prefix.</param>
    /// <param name="page">The page number.</param>
    /// <returns>The collection name including the page number formatted with leading zeros.</returns>
    public static string CollectionNameForPage(string collectionPrefix, int page) {
      return collectionPrefix + "__" + page.ToString("D3");
    }

    /// <summary>
    /// Queries a specific page of data using the CascadeDataLayer and updates the state regarding loaded pages.
    /// </summary>
    /// <param name="page">The page number of the data to be queried.</param>
    /// <param name="freshnessSeconds">Optional freshness override in seconds; defaults to the paginator's FreshnessSeconds.</param>
    /// <returns>An IEnumerable of Model containing the data for the specified page.</returns>
    public async Task<IEnumerable<Model>> Query(int page, int? freshnessSeconds = null) {
      Log.Debug($"BEGIN Paginator Query page {page}");
      if (Loading)
        throw new ConstraintException("CascadePaginator Query cannot be re-entered when it has not completed");
      try {
        Loading = true;

        // Add pagination information to the criteria
        var criteriaWithPagination = AddPaginationToCriteria(Criteria, page);
        var results = (await Cascade.Query<Model>(
          CollectionName(page),
          criteriaWithPagination,
          populate: this.Populate,
          freshnessSeconds: freshnessSeconds ?? this.FreshnessSeconds,
          populateFreshnessSeconds: this.PopulateFreshnessSeconds,
          hold: this.Hold,
          localOnly: this.LocalOnly
        )).ToImmutableArray();
        queriedPages.Add(page);
        if (!LastPageLoaded && page > HighestPage) {
          HighestPage = page;
          LastPageLoaded = results.Length < PerPage;
        }
        Log.Debug($"END Paginator Query page {page} returning {results.Length}");
        return results;
      }
      finally {
        // Ensure the loading state is reset after operation
        Loading = false;
      }
    }

    /// <summary>
    /// Queries pages sequentially from page 0 until an empty page is returned or the last page has been loaded.
    /// </summary>
    /// <param name="freshnessSeconds">Optional freshness override in seconds passed to each page query.</param>
    /// <returns>A list of pages, each a list of Model instances in page order.</returns>
    public async Task<IReadOnlyList<IReadOnlyList<Model>>> QueryAllPages(int? freshnessSeconds = null) {
      var results = new List<IReadOnlyList<Model>>();
      var i = 0;
      while (true) {
        var items = (await Query(i,freshnessSeconds)).ToArray();
        if (items.Length == 0)
          break;
        results.Add(items);
        if (LastPageLoaded && i==HighestPage)
          break;
        i++;
      }
      return results;
    }

    /// <summary>
    /// Reads the cached page collections consecutively from page 0, loading the models for each page's cached ids without contacting the origin.
    /// Stops at the first missing or empty collection.
    /// </summary>
    /// <returns>A list of pages from the cache, each a list of Model instances in page order.</returns>
    public async Task<IReadOnlyList<IReadOnlyList<Model>>> GetAllCached() {
      var results = new List<IReadOnlyList<Model>>();
      var i = 0;
      while (true) {
        var ids = (await Cascade.GetCollection<Model>(CollectionName(i++),freshnessSeconds: RequestOp.FRESHNESS_ANY))?.ToArray();
        if (ids?.Any() ?? false) {
          var models = (await Cascade.GetModelsForIds<Model>(ids, freshnessSeconds: RequestOp.FRESHNESS_ANY)).ToImmutableArray(); 
          results.Add(models);          
        } else {
          break;
        }
      }
      return results;
    }

    /// <summary>
    /// Determines the highest consecutively numbered page, starting from page 0, that has a collection present in the cache.
    /// </summary>
    /// <returns>The highest consecutive cached page number, or null if page 0 is not cached.</returns>
    public async Task<int?> HighestPageNumberInCache() {
      var i = 0;
      int? highest = null;
      while (true) {
        var collectionName = CollectionName(i);
        var collectionExists = (await Cascade.GetArrivedAt<Model>(collectionName))!=null;
        if (collectionExists)
          highest = i;
        else
          break;
        i++;
      }
      return highest;
    }

    /// <summary>
    /// Adds pagination parameters to the criteria to filter query operations.
    /// Must be implemented by derived classes.
    /// </summary>
    /// <param name="criteria">The original query criteria before pagination is applied.</param>
    /// <param name="page">The current page number to be applied for pagination.</param>
    /// <returns>An object representing the modified criteria with pagination included.</returns>
    protected abstract object AddPaginationToCriteria(object criteria, int page);

    /// <summary>
    /// Clears the cached data in the collections for all the queried pages.
    /// Resets the highest queried page and last page loaded status.
    /// </summary>
    public async Task Clear() {
      foreach (var page in queriedPages) {
        await Cascade.ClearCollection<Model>(CollectionName(page));
      }
      HighestPage = -1;
      LastPageLoaded = false;
    }

    /// <summary>
    /// Intended to refresh the cached data for all queried pages with the specified freshness.
    /// Currently a no-op: the implementation is commented out.
    /// </summary>
    /// <param name="freshnessSeconds">The number of seconds for the data to be considered fresh on query.</param>
    public async Task Refresh(int freshnessSeconds = 0) {
      // foreach (var page in queriedPages) {
      // 	await Query(page, freshnessSeconds);
      // }
    }

    /// <summary>
    /// Inserts a new model item at the beginning of the collection for the first page.
    /// Adjusts the first page's collection to include the new item.
    /// </summary>
    /// <param name="newDocket">The new instance of Model to be prepended to the collection.</param>
    public async Task Prepend(Model newDocket) {
      var collection0Name = CollectionName(0);
      var collection0 = await Cascade.GetCollection<Model>(collection0Name);
      if (collection0 == null)
        return;

      // Insert new docket at the beginning of the first page's collection
      var newCollection0 = collection0.ToImmutableArray().Insert(0, CascadeTypeUtils.GetCascadeId(newDocket));
      await Cascade.SetCollection<Model>(collection0Name, newCollection0);
    }
  }
}

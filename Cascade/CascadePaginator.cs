using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Cascade {
	public abstract class CascadePaginator<Model> where Model : class {
		private HashSet<int> queriedPages = new HashSet<int>();
		private bool? Hold;
		public CascadeDataLayer Cascade { get; }
		public object Criteria { get; }
		public string CollectionPrefix { get; }
		public int PerPage { get; }
		public int HighestPage { get; protected set; } = -1;
		public bool LastPageLoaded { get; protected set; }
		public bool Loading { get; protected set; }

		public CascadePaginator(
			CascadeDataLayer cascade, 
			object criteria, 
			string collectionPrefix, 
			int perPage,
			IEnumerable<string>? populate = null, 
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			bool? hold = null
		) {
			Cascade = cascade;
			Criteria = criteria;
			CollectionPrefix = collectionPrefix;
			PerPage = perPage;
			Populate = populate;
			FreshnessSeconds = freshnessSeconds;
			PopulateFreshnessSeconds = populateFreshnessSeconds;
			Hold = hold;
		}

		public int? FreshnessSeconds { get; protected set; }
		public int? PopulateFreshnessSeconds { get; protected set; }
		public IEnumerable<string>? Populate { get; protected set; }

		string collectionName(int page) {
			return CollectionPrefix+"__"+page.ToString("D3");
		}

		public async Task<IEnumerable<Model>> Query(int page) {
			Log.Debug($"BEGIN Paginator Query page {page}");
			if (Loading)
				throw new ConstraintException("CascadePaginator Query cannot be re-entered when it has not completed");
			try {
				Loading = true;
				var criteriaWithPagination = AddPaginationToCriteria(Criteria,page);
				IEnumerable<Model> results = null;
				try {
					results = await Cascade.Query<Model>(
						collectionName(page), 
						criteriaWithPagination, 
						populate: this.Populate, 
						freshnessSeconds: this.FreshnessSeconds,
						populateFreshnessSeconds: this.PopulateFreshnessSeconds,
						hold: this.Hold
					);
				}
				catch (DataNotAvailableOffline e) {
					Console.WriteLine($"DataNotAvailableOffline page {page}");
				}

				if (results == null) {
					LastPageLoaded = true;
				} else {
					queriedPages.Add(page);
					if (!LastPageLoaded && page > HighestPage) {
						HighestPage = page;
						LastPageLoaded = results.Count() < PerPage;
					}
				}
				Log.Debug($"END Paginator Query page {page} returning {results?.Count()}");
				return results;
			}
			finally {
				Loading = false;
			}
		}
		
		protected abstract object AddPaginationToCriteria(object criteria, int page);

		public async Task Clear() {
			foreach (var page in queriedPages) {
				await Cascade.ClearCollection<Model>(collectionName(page));
			}
			HighestPage = -1;
			LastPageLoaded = false;
		}

		public async Task Refresh(int freshnessSeconds = 0) {
			// foreach (var page in queriedPages) {
			// 	await Query(page, freshnessSeconds);
			// }
		}

		public async Task Prepend(Model newDocket) {
			var collection0Name = collectionName(0);
			var collection0 = await Cascade.GetCollection<Model>(collection0Name);
			if (collection0 == null)
				return;
			var newCollection0 = collection0.ToImmutableArray().Insert(0, CascadeTypeUtils.GetCascadeId(newDocket));
			await Cascade.SetCollection<Model>(collection0Name, newCollection0);
		}
	}
}

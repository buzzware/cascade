using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Cascade {
	public abstract class CascadePaginator<Model> {
		private HashSet<int> queriedPages = new HashSet<int>();
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
			int? freshnessSeconds = null
		) {
			Cascade = cascade;
			Criteria = criteria;
			CollectionPrefix = collectionPrefix;
			PerPage = perPage;
			Populate = populate;
			FreshnessSeconds = freshnessSeconds;
		}

		public int? FreshnessSeconds { get; protected set; }
		public IEnumerable<string>? Populate { get; protected set; }

		string collectionName(int page) {
			return CollectionPrefix+"__"+page.ToString("D3");
		}

		public async Task<IEnumerable<Model>> Query(int page) {
			if (Loading)
				throw new ConstraintException("CascadePaginator Query cannot be re-entered when it has not completed");
			try {
				Loading = true;
				var criteriaWithPagination = AddPaginationToCriteria(Criteria,page);
				queriedPages.Add(page);
				var results = await Cascade.Query<Model>(
					collectionName(page), 
					criteriaWithPagination, 
					populate: this.Populate, 
					freshnessSeconds: this.FreshnessSeconds
				);
				if (!LastPageLoaded && page > HighestPage) {
					HighestPage = page;
					LastPageLoaded = results.Count() < PerPage;
				}
				return results;
			}
			finally {
				Loading = false;
			}
		}
		
		protected abstract object AddPaginationToCriteria(object criteria, int page);

		public async Task Clear() {
			foreach (var page in queriedPages) {
				var key = collectionName(page);
				await Cascade.ClearCollection(key);
			}
			HighestPage = -1;
			LastPageLoaded = false;
		}

		public async Task Refresh(int freshnessSeconds = 0) {
			// foreach (var page in queriedPages) {
			// 	await Query(page, freshnessSeconds);
			// }
		}
	}
}
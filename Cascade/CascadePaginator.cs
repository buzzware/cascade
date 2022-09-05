using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cascade {
	public abstract class CascadePaginator<Model> {
		private HashSet<int> queriedPages = new HashSet<int>();
		public CascadeDataLayer Cascade { get; }
		public object Criteria { get; }
		public string CollectionPrefix { get; }
		public int PerPage { get; }

		public CascadePaginator(CascadeDataLayer cascade, object criteria, string collectionPrefix, int perPage) {
			Cascade = cascade;
			Criteria = criteria;
			CollectionPrefix = collectionPrefix;
			PerPage = perPage;
		}
		
		string collectionName(int page) {
			return CollectionPrefix+"__"+page.ToString("D3");
		}

		//async 
		public Task<IEnumerable<Model>> Query(int page, int? freshnessSeconds = null) {
			var criteriaWithPagination = AddPaginationToCriteria(Criteria,page);
			queriedPages.Add(page);
			return Cascade.Query<Model>(collectionName(page), criteriaWithPagination, freshnessSeconds);
		}

		protected abstract object AddPaginationToCriteria(object criteria, int page);

		public async Task Clear() {
			foreach (var page in queriedPages) {
				var key = collectionName(page);
				await Cascade.ClearCollection(key);
			}
		}

		public async Task Refresh(int freshnessSeconds = 0) {
			foreach (var page in queriedPages) {
				await Query(page, freshnessSeconds);
			}
		}
	}
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cascade {
	public interface ICascadeStore
	{
		// may leak exceptions for connection etc
		// do not leak exceptions for not found
		Task<CascadeStoreResponse<M>> Read<M>(string aResourceId) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> Read<M>(long aResourceId) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<List<M>>> ReadAll<M>() where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> Write<M>(M value) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> Destroy<M>(string aResourceId) where M : class, ICascadeModel, new();
		Task<CascadeStoreResponse<M>> DestroyExcept<M>(IEnumerable<string> aResourceIds) where M : class, ICascadeModel, new();
	}
}
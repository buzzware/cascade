using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Buzzware.Cascade {
	public interface IModelClassOrigin {
		Task<IEnumerable> Query(object criteria);
		Task<object?> Get(object id);
		Task<object> Create(object value);
		Task<object> Replace(object value);
		Task<object> Update(object id, IDictionary<string, object> changes, object? model);
		Task Destroy(object model);
		Task EnsureAuthenticated();
		Task ClearAuthentication();
		Task<object> Execute(RequestOp request, bool connectionOnline);
		ICascadeOrigin Origin { get; set; }
	}
}

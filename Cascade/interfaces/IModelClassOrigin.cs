using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cascade {
	public interface IModelClassOrigin {
		Task<IEnumerable> Query(object criteria);
		Task<object?> Get(object id);
		Task<object> Create(object value);
		Task<object> Replace(object value);
		Task EnsureAuthenticated();
	}
}

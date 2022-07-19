using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cascade {
	public interface IModelClassOrigin {
		Task<IEnumerable<object>> Query(object criteria, string key);
		Task<object?> Get(object id);
	}
}

using System.Threading.Tasks;

namespace Cascade {
	public interface ICascadeOrigin {
		Task<OpResponse> ProcessRequest(RequestOp request);
	}
}
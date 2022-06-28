using System.ComponentModel;

namespace Cascade {
	public interface ICascadeModel<I> {
		string CascadeResource();
		I CascadeId();
	}
}

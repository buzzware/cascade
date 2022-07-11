using System.ComponentModel;

namespace Cascade {
	public interface ICascadeModel {
		string CascadeResource();
		object CascadeId();
	}
}

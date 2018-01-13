using System.ComponentModel;

namespace Cascade {
	public interface ICascadeModel {
		string GetResource();			// rename to CascadeResource
		string GetResourceId();		// rename to CascadeId
	}
}

using System.Runtime.CompilerServices;

namespace Cascade {
	public abstract class CascadeModel : ICascadeModel {
		public string GetResource() {
			return this.GetType().Name;
		}

		public abstract string GetResourceId();
	}
}
using System.ComponentModel;
using PropertyChanged;

namespace Cascade {
	
	[AddINotifyPropertyChangedInterface]
	public abstract class CascadeModel : ICascadeModel,INotifyPropertyChanged {
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		public string CascadeResource() {
			return this.GetType().Name;
		}

		public abstract string CascadeId();
	}
}

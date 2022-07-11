using System.ComponentModel;

namespace Cascade {
	
	public abstract class CascadeModel : SuperModel.SuperModel, ICascadeModel,INotifyPropertyChanged {
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		public string CascadeResource() {
			return this.GetType().Name;
		}

		public abstract object CascadeId();
	}
}

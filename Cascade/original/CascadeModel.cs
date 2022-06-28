using System.ComponentModel;

namespace Cascade {
	
	public abstract class CascadeModel<I> : SuperModel.SuperModel, ICascadeModel<I>,INotifyPropertyChanged {
		
		public event PropertyChangedEventHandler PropertyChanged;
		
		public string CascadeResource() {
			return this.GetType().Name;
		}

		public abstract I CascadeId();
	}
}

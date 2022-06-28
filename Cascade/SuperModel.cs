using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SuperModel {
	public class MutationAttemptException : Exception {
		public MutationAttemptException(string message) : base(message) {
		}
	}

	public class SuperModel : INotifyPropertyChanged {
		
		protected readonly SuperModel? _proxyFor;
		protected readonly Dictionary<string, bool> _propertySet = new Dictionary<string, bool>();

		public SuperModel(SuperModel? proxyFor = null)
		{
			_proxyFor = proxyFor;
			if (_proxyFor != null) _proxyFor.PropertyChanged += OnProxyForOnPropertyChanged;
		}
		
		public bool __mutable {
			get => ___mutable; 
			set => ___mutable = value;
		}
		private bool ___mutable = true;
		
		protected bool SetProperty<T>(ref T backingStore, T value,
			[CallerMemberName] string propertyName = "",
			Action onChanged = null)
		{
			if (!__mutable)
				throw new MutationAttemptException("Attempted to mutate " + propertyName + " when __mutable = false");

			if (_proxyFor != null)
			{
				_propertySet[propertyName] = true;
			}
			
			if (EqualityComparer<T>.Default.Equals(backingStore, value))
				return false;

			backingStore = value;
			onChanged?.Invoke();
			OnPropertyChanged(propertyName);
			return true;
		}

		protected T GetProperty<T>(ref T backingStore,
			[CallerMemberName] string propertyName = "",
			Action onChanged = null)
		{
			if (_proxyFor != null)
			{
				if (_propertySet.ContainsKey(propertyName))
					return backingStore;
				else
					return (T)_proxyFor.GetType().GetProperty(propertyName).GetValue(_proxyFor);
			} 
			else
				return backingStore;
		}
		
		#region INotifyPropertyChanged
		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
		{
			var changed = PropertyChanged;
			if (changed == null)
				return;

			changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
		#endregion

		protected void OnProxyForOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{ 
			OnPropertyChanged(e.PropertyName);
		}
	}
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cascade {
	
	public class MutationAttemptException : Exception {
		public MutationAttemptException(string message) : base(message) {
		}
	}

	public class SuperModel : INotifyPropertyChanged {
		
		protected readonly SuperModel? _proxyFor;
		protected readonly ConcurrentDictionary<string, bool> _propertySet = new ConcurrentDictionary<string, bool>();

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
		
		public SuperModel? __ProxyFor => _proxyFor;
		
		public IDictionary<string, object> __GetChanges() {
			var result = new Dictionary<string, object>();
			foreach (var kv in _propertySet) {
				var prop = this.GetType().GetProperty(kv.Key);
				result[kv.Key] = prop.GetValue(this);
			}
			return result;
		}

		public void __ApplyChanges(IDictionary<string, object> changes) {
			foreach (var kv in changes) {
				var prop = this.GetType().GetProperty(kv.Key);
				try {
					prop?.SetValue(this,kv.Value);
				}
				catch (Exception e) {
					Console.WriteLine($"SuperModel.__ApplyChanges exception fired setting ${kv.Key}=${kv.Value}");
					Console.WriteLine(e.ToString());
					prop?.SetValue(this,CascadeTypeUtils.ConvertTo(prop.PropertyType,kv.Value));
					Console.WriteLine($"SuperModel.__ApplyChanges CascadeTypeUtils.ConvertTo succeeded");
					//throw;
				}
			}
		}

		public void __ClearChanges() {
			_propertySet.Clear();
		}

		public void __mutateWith(Action<SuperModel> action) {
			if (__mutable) {
				action.Invoke(this);
			}
			else {
				var mutable = ___mutable;
				try {
					___mutable = true;
					action.Invoke(this);
				}
				finally {
					___mutable = mutable;
				}
			}
		}
		
		// public IDictionary<string, object> __GetProxyChanges() {
		// 	if (__ProxyFor == null)
		// 		throw new Exception("Cannot __GetProxyChanges when __ProxyFor==null");
		// 	var result = new Dictionary<string, object>();
		// 	foreach (var kv in _propertySet) {
		// 		var prop = this.GetType().GetProperty(kv.Key);
		// 		var localValue = prop.GetValue(this);
		// 		var proxiedValue = prop.GetValue(_proxyFor);
		// 		if (local)
		// 		result[kv.Key] = localValue;
		// 	}
		// 	return result;
		// }

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
			OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
			return true;
		}

		// protected bool SetAssociation<T>(ref T backingStore, T value,
		// 	[CallerMemberName] string propertyName = "",
		// 	Action onChanged = null)
		// {
		//
		// 	if (_proxyFor != null)
		// 	{
		// 		_propertySet[propertyName] = true;
		// 	}
		// 	
		// 	if (EqualityComparer<T>.Default.Equals(backingStore, value))
		// 		return false;
		//
		// 	backingStore = value;
		// 	onChanged?.Invoke();
		// 	OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		// 	return true;
		// }

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
		
		public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
		{
			OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		}
		
		#region INotifyPropertyChanged
		public event PropertyChangedEventHandler? PropertyChanged;
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
		{
			var changed = PropertyChanged;
			if (changed == null)
				return;

			changed.Invoke(this, args);
		}
		#endregion

		protected void OnProxyForOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{ 
			OnPropertyChanged(new PropertyChangedEventArgs(e.PropertyName));
		}

		// public Dictionary<string, object> ToDictionary(Func<string,object,Tuple<string,object>> filter = null) {
		// 	var result = new Dictionary<string, object>();
		// 	foreach (var pi in this.GetType().GetProperties()) {
		// 		var key = pi.Name;
		// 		var value = pi.GetValue(this);
		// 	}
		// }
		
		public SuperModel Clone(IDictionary<string,object>? changes = null) {
			var result = (SuperModel)this.MemberwiseClone();
			if (changes!=null)
				result.__mutateWith(result=>result.__ApplyChanges(changes));
			return result;
		}
	}
}

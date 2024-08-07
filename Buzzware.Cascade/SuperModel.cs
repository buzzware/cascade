using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Buzzware.Cascade {
	
	public class MutationAttemptException : Exception {
		public MutationAttemptException(string message) : base(message) {
		}
	}

	public class SuperModel : INotifyPropertyChanged {
		
		protected SuperModel? _proxyFor;
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

		public void __SetProxyFor(
			SuperModel? value, 
			bool keepChanges,		// try to maintain property changes even though we are changing the proxied instance (assume is for the same id)   
			bool raiseIncoming	// raise PropertyChanged events for properties where the new proxied instance has different values
		) {
			if (value == _proxyFor)
				return;
			if (_proxyFor!=null)
				_proxyFor.PropertyChanged -= OnProxyForOnPropertyChanged;
			if (value!=null)
				value.PropertyChanged += OnProxyForOnPropertyChanged;

			var hasChangesBefore = __HasChanges;
			
			Dictionary<string, object?>? changes = null;
			if (raiseIncoming) {
				changes = new Dictionary<string, object?>();
				var selectedProperties = FastReflection.getProperties(this.GetType())
					.Where(n=>n.Key[0]!='_')
					.ToArray();
				foreach (var prop in selectedProperties) {
					var wasProxyChange = _propertySet.TryGetValue(prop.Key, out var setValue) && setValue; 
					var oldValue = FastReflection.invokeGetter(this, prop.Key);
					var newActualValue = FastReflection.invokeGetter(value, prop.Key);
					var newValue = wasProxyChange && keepChanges ? oldValue : newActualValue;
					var sameValue = Nullable.Equals(newValue, oldValue);
					if (keepChanges && wasProxyChange && Nullable.Equals(newActualValue,oldValue))
						_propertySet.TryRemove(prop.Key, out var outValue);
					if (!sameValue)
						changes[prop.Key] = newValue;
				}
			}
			
			if (!keepChanges)
				_propertySet.Clear();
			
			_proxyFor = value;
			
			if (raiseIncoming) {
				if (changes != null) foreach (var prop in changes) {
					RaisePropertyChanged(prop.Key);
				}
				if (__HasChanges!=hasChangesBefore)
					RaisePropertyChanged(nameof(__HasChanges));
			}
		}
		
		public IDictionary<string, object?> __GetChanges() {
			var result = new Dictionary<string, object?>();
			foreach (var kv in _propertySet) {
				var prop = this.GetType().GetProperty(kv.Key);
				result[kv.Key] = prop.GetValue(this);
			}
			return result;
		}

		public bool __HasChanges => !_propertySet.IsEmpty;

		public void __ApplyChanges(IDictionary<string, object?> changes) {
			foreach (var kv in changes) {
				var prop = this.GetType().GetProperty(kv.Key);
				try {
					prop?.SetValue(this,kv.Value);
				}
				catch (Exception e) {
					Console.WriteLine($"SuperModel.__ApplyChanges exception fired setting {kv.Key}={kv.Value}");
					Console.WriteLine(e.ToString());
					prop?.SetValue(this,CascadeTypeUtils.ConvertTo(prop.PropertyType,kv.Value));
					Console.WriteLine($"SuperModel.__ApplyChanges CascadeTypeUtils.ConvertTo succeeded");
					//throw;
				}
			}
		}

		public void __ClearChanges() {
			_propertySet.Clear();
			RaisePropertyChanged(nameof(__HasChanges));
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
		
		// public IDictionary<string, object?> __GetProxyChanges() {
		// 	if (__ProxyFor == null)
		// 		throw new Exception("Cannot __GetProxyChanges when __ProxyFor==null");
		// 	var result = new Dictionary<string, object?>();
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
			RaisePropertyChanged(nameof(__HasChanges));
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

		// public Dictionary<string, object?> ToDictionary(Func<string,object,Tuple<string,object?>> filter = null) {
		// 	var result = new Dictionary<string, object?>();
		// 	foreach (var pi in this.GetType().GetProperties()) {
		// 		var key = pi.Name;
		// 		var value = pi.GetValue(this);
		// 	}
		// }
		
		public SuperModel Clone(IDictionary<string,object?>? changes = null) {
			var result = (SuperModel)this.MemberwiseClone();
			if (changes!=null)
				result.__mutateWith(result=>result.__ApplyChanges(changes));
			return result;
		}
	}
}

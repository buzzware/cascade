using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Buzzware.Cascade {

  /// <summary>
  /// Custom exception thrown when a mutation attempt is made on an immutable property.
  /// </summary>
  public class MutationAttemptException : Exception {
    public MutationAttemptException(string message) : base(message) {
    }
  }

  /// <summary>
  /// A base class for Cascade models that handles property change notifications, property mutations, and proxy functionality.
  /// </summary>
  public class SuperModel : INotifyPropertyChanged {
    
    protected SuperModel? _proxyFor;
    protected readonly ConcurrentDictionary<string, bool> _propertySet = new ConcurrentDictionary<string, bool>();

    /// <summary>
    /// Initializes a new instance of the SuperModel with an optional proxy model.
    /// </summary>
    /// <param name="proxyFor">(optional) a SuperModel that this new instance will act as a proxy for.</param>
    public SuperModel(SuperModel? proxyFor = null)
    {
      _proxyFor = proxyFor;
      if (_proxyFor != null) _proxyFor.PropertyChanged += OnProxyForOnPropertyChanged;
    }

    /// <summary>
    /// Gets or sets the mutable state of the model.
    /// When false, attempting to modify properties will throw a MutationAttemptException.
    /// </summary>
    public bool __mutable {
      get => ___mutable; 
      set => ___mutable = value;
    }
    private bool ___mutable = true;

    /// <summary>
    /// The proxy model that this model is currently proxying for, if any.
    /// </summary>
    public SuperModel? __ProxyFor => _proxyFor;

    /// <summary>
    /// Sets a new proxy model and optionally retains changes or raises property change events.
    /// </summary>
    /// <param name="value">The new proxy model to set.</param>
    /// <param name="keepChanges">maintain property changes while changing proxied instance</param>
    /// <param name="raiseIncoming">raise PropertyChanged events for differing values in new proxy instance</param>
    public void __SetProxyFor(
      SuperModel? value, 
      bool keepChanges,
      bool raiseIncoming 
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
        var selectedProperties = FastReflection.GetClassInfo(this.GetType())!.DataAndAssociationInfos.ToArray();
        foreach (var prop in selectedProperties) {
          var wasProxyChange = _propertySet.TryGetValue(prop.Key, out var setValue) && setValue; 
          var oldValue = FastReflection.GetValue(this, prop.Key);
          var newActualValue = FastReflection.GetValue(value, prop.Key);
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

    /// <summary>
    /// When acting as a proxy to another object, this method returns changes that have been made on this object vs the proxied object 
    /// </summary>
    /// <returns>A dictionary of property names and their values.</returns>
    public IDictionary<string, object?> __GetChanges() {
      var result = new Dictionary<string, object?>();
      var ci = FastReflection.GetClassInfo(this.GetType());
      foreach (var kv in _propertySet) {
        result[kv.Key] = ci.GetValue(this,kv.Key);
      }
      return result;
    }

    /// <summary>
    /// True if there are any uncommitted property changes in this model.
    /// </summary>
    public bool __HasChanges => !_propertySet.IsEmpty;

    /// <summary>
    /// Applies a set of changes to the properties of the model.
    /// </summary>
    /// <param name="changes">A dictionary containing property names and the values to apply.</param>
    public void __ApplyChanges(IDictionary<string, object?> changes) {
      var ci = FastReflection.GetClassInfo(this.GetType());
      foreach (var kv in changes) {
        var prop = ci.GetPropertyInfo(kv.Key);
        try {
          prop?.SetValue(this,kv.Value);
        }
        catch (Exception e) {
          prop!.SetValue(this,CascadeTypeUtils.ConvertTo(prop.Type,kv.Value,CascadeTypeUtils.GetDefaultValue(prop.Type)));
        }
      }
    }

    /// <summary>
    /// Clears all changes made to the model, resetting the property state to the proxy model values.
    /// </summary>
    public void __ClearChanges() {
      _propertySet.Clear();
      RaisePropertyChanged(nameof(__HasChanges));
    }

    /// <summary>
    /// Executes an action that mutates the model, restoring the original mutation state afterwards.
    /// </summary>
    /// <param name="action">The action to perform which mutates the model.</param>
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

    /// <summary>
    /// Sets a property value and raises property change notifications if the value has changed.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="backingStore">The reference to the private backing field of the property.</param>
    /// <param name="value">The new value to set.</param>
    /// <param name="propertyName">The name of the property, automatically filled in by the compiler.</param>
    /// <param name="onChanged">An optional action to perform when the property value changes.</param>
    /// <returns>True if the value has changed, otherwise false.</returns>
    /// <exception cref="MutationAttemptException">Thrown if a mutation is attempted when __mutable is false.</exception>
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

    /// <summary>
    /// Gets a property value, taking into account whether it's overridden by a proxy.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="backingStore">The reference to the private backing field of the property.</param>
    /// <param name="propertyName">The name of the property, automatically filled in by the compiler.</param>
    /// <param name="onChanged">An optional action to perform when the property value is accessed.</param>
    /// <returns>The current value of the property.</returns>
    protected T GetProperty<T>(ref T backingStore,
      [CallerMemberName] string propertyName = "",
      Action onChanged = null)
    {
      if (_proxyFor != null)
      {
        if (_propertySet.ContainsKey(propertyName))
          return backingStore;
        else
          return (T)FastReflection.GetValue(_proxyFor,propertyName);
      } 
      else
        return backingStore;
    }

    /// <summary>
    /// Raises a property changed notification for the specified property name.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
    {
      OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event with the provided arguments.
    /// </summary>
    /// <param name="args">The property changed event arguments.</param>
    protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
    {
      var changed = PropertyChanged;
      if (changed == null)
        return;

      changed.Invoke(this, args);
    }
    #endregion

    /// <summary>
    /// Handles property change events from the proxy model and re-raises them for this model.
    /// </summary>
    /// <param name="sender">The sender of the property changed event.</param>
    /// <param name="e">The property changed event arguments.</param>
    protected void OnProxyForOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    { 
      OnPropertyChanged(new PropertyChangedEventArgs(e.PropertyName));
    }

    // public Dictionary<string, object?> ToDictionary(Func<string,object,Tuple<string,object?>> filter = null) {
    //  var result = new Dictionary<string, object?>();
    //  foreach (var pi in this.GetType().GetProperties()) {
    //    var key = pi.Name;
    //    var value = pi.GetValue(this);
    //  }
    // }

    /// <summary>
    /// Creates a shallow copy of the current SuperModel, optionally applying additional property changes.
    /// </summary>
    /// <param name="changes">Optional dictionary of changes to apply to the cloned instance.</param>
    /// <returns>A new SuperModel instance that is a clone of the current instance with applied changes.</returns>
    public SuperModel Clone(IDictionary<string,object?>? changes = null) {
      var result = (SuperModel)this.MemberwiseClone();
      if (changes!=null)
        result.__mutateWith(result=>result.__ApplyChanges(changes));
      return result;
    }
  }
}

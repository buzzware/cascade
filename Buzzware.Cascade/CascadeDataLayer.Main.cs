using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade {

  /// <summary>
  /// The main class for all data requests and other operations.
  /// This would generally only be created once on startup of an application.
  /// </summary>
  public partial class CascadeDataLayer : INotifyPropertyChanged {
    public const int FRESHNESS_ANY = Int32.MaxValue;
    
    private readonly IEnumerable<ICascadeCache> CacheLayers;
    private readonly ICascadePlatform cascadePlatform;
    public readonly CascadeConfig Config;
    private readonly ErrorControl errorControl;
    private readonly object lockObject;
    public readonly ICascadeOrigin Origin;
    private readonly CascadeJsonSerialization serialization;

    private bool _connectionOnline = true;

    /// <summary>
    /// CascadeDataLayer main constructor
    /// </summary>
    /// <param name="origin">Origin server</param>
    /// <param name="cacheLayers">Cache layers in order. Typically this would be an instance of a memory cache followed by a file based cache.</param>
    /// <param name="config">configuration for cascade</param>
    /// <param name="cascadePlatform">platform specific implementation</param>
    /// <param name="errorControl">instance for managing exceptions</param>
    /// <param name="serialization">instance for serializing models</param>
    public CascadeDataLayer(
      ICascadeOrigin origin,
      IEnumerable<ICascadeCache> cacheLayers,
      CascadeConfig config,
      ICascadePlatform cascadePlatform,
      ErrorControl errorControl, 
      CascadeJsonSerialization serialization
    ) {
      Origin = origin;
      Origin.Cascade = this;
      CacheLayers = cacheLayers;
      foreach (var cache in cacheLayers)
        cache.Cascade = this;
      Config = config;
      this.cascadePlatform = cascadePlatform;
      this.errorControl = errorControl;
      this.serialization = serialization;
    }
    

    /// <summary>
    /// Use this timestamp to keep in sync with the framework. Especially useful for testing
    /// as time can then be controlled by your origin implementation. 
    /// Milliseconds since 1970
    /// </summary>
    public long NowMs => Origin.NowMs;
    
    
    /// <summary>
    /// This property determines whether the framework acts in online (true) or offline (false) mode.
    /// It can be set to offline at any time, but should not be set to online unless the changes pending list is empty.
    /// <see cref="ReconnectOnline">ReconnectOnline() uploads changes and sets ConnectionOnline=true for you.</see>  
    /// </summary>
    public bool ConnectionOnline {
      get => _connectionOnline;
      set {
        if (value != _connectionOnline) 
          cascadePlatform.InvokeOnMainThreadNow(() => {
            _connectionOnline = value;
            OnPropertyChanged(nameof(ConnectionOnline));
          });  
      }
    }

    /// <summary>
    /// Used for watching properties on this (normally ConnectionOnline)
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises this object's PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">Name of the property used to notify listeners. This
    /// value is optional and can be provided automatically when invoked from compilers
    /// that support <see cref="CallerMemberNameAttribute"/>.</param>
    public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
    {
      OnPropertyChanged(propertyName);
    }
    
    /// <summary>
    /// Sets a field's value and triggers a property change notification if the value has changed.
    /// </summary>
    /// <typeparam name="T">Type of the field value.</typeparam>
    /// <param name="field">The reference to the backing field.</param>
    /// <param name="value">The new value to set.</param>
    /// <param name="propertyName">The name of the property (automatically provided).</param>
    /// <returns>true if the value has changed, false otherwise.</returns>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
      if (EqualityComparer<T>.Default.Equals(field, value)) return false;
      field = value;
      OnPropertyChanged(propertyName);
      return true;
    }
    
    /// <summary>
    /// Ensures that the user is authenticated. If not, it will attempt to authenticate
    /// the user based on the provided type.
    /// </summary>
    /// <param name="type">The type used for authentication, can be null.</param>
    /// <returns><c>true</c> if the user is authenticated; otherwise, <c>false</c>.</returns>
    public async Task EnsureAuthenticated(Type? type = null) {
      await Origin.EnsureAuthenticated(type);
    }
    
    /// <summary>
    /// Obtains a list of model types that the Origin can handle.
    /// </summary>
    /// <returns>An enumerable list of model types.</returns>
    public IEnumerable<Type> ListModelTypes() {
      return Origin.ListModelTypes();
    }
    
  }
}

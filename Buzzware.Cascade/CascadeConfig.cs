using System;
using System.Collections.Generic;
using System.IO;

namespace Buzzware.Cascade {

  /// <summary>
  /// Per-model-type freshness configuration, used via CascadeConfig.ModelConfig to override the CascadeConfig defaults.
  /// </summary>
  public class ModelConfig {
    /// <summary>
    /// Duration, in seconds, for which data of this model type is considered fresh.
    /// </summary>
    public readonly int FreshnessSeconds;
    /// <summary>
    /// Freshness, in seconds, accepted for cached data of this model type when network requests fail.
    /// </summary>
    public readonly int FallbackFreshnessSeconds;
    /// <summary>
    /// Optional upper limit, in seconds, constraining any requested freshness for this model type.
    /// </summary>
    public int? MaximumFreshnessSeconds;

    /// <summary>
    /// ModelConfig Constructor
    /// </summary>
    /// <param name="freshnessSeconds">Duration, in seconds, for which data of this model type is considered fresh.</param>
    /// <param name="fallbackFreshnessSeconds">Freshness, in seconds, accepted for cached data when network requests fail.</param>
    /// <param name="maximumFreshnessSeconds">Optional upper limit, in seconds, constraining any requested freshness.</param>
    public ModelConfig(int freshnessSeconds, int fallbackFreshnessSeconds, int? maximumFreshnessSeconds = null) {
      FreshnessSeconds = freshnessSeconds;
      FallbackFreshnessSeconds = fallbackFreshnessSeconds;
      MaximumFreshnessSeconds = maximumFreshnessSeconds;
    }
  }
  
  /// <summary>
  /// User configuration class for the Cascade library
  /// </summary>
  public class CascadeConfig {
    /// <summary>
    /// Maximum number of requests Cascade will execute in parallel.
    /// </summary>
    public int MaxParallelRequests = 8;

    /// <summary>
    /// Limit to freshness for blobs and model types that don't have a ModelConfig. Use this for testing ie set it to 120 seconds and you will only have to wait 2 minutes for data to be loaded fresh
    /// </summary>
    public int? DefaultMaximumFreshnessSeconds = null;
    
    /// <summary>
    /// Default duration, in seconds, for which data is considered fresh.
    /// </summary>
    public int DefaultFreshnessSeconds = RequestOp.FRESHNESS_DEFAULT;

    /// <summary>
    /// Default duration, in seconds, for which populated data is considered fresh.
    /// </summary>
    public int DefaultPopulateFreshnessSeconds = RequestOp.FRESHNESS_DEFAULT;

    /// <summary>
    /// Freshness value for when ConnectionOnline = True and network requests fail
    /// </summary>
    public int DefaultFallbackFreshnessSeconds = RequestOp.FALLBACK_ANY;

    /// <summary>
    /// Default freshness value for blobs
    /// </summary>
    public int BlobFreshnessSeconds = RequestOp.FRESHNESS_DEFAULT;
    
    /// <summary>
    /// Default fallback freshness value for blobs
    /// </summary>
    public int BlobFallbackFreshnessSeconds = RequestOp.FALLBACK_ANY;

    /// <summary>
    /// Root directory path for all files written by Cascade 
    /// </summary>
    public string StoragePath;

    /// <summary>
    /// Path for storing pending changes
    /// </summary>
    public string PendingChangesPath => Path.Combine(StoragePath, "PendingChanges");

    /// <summary>
    /// Path for storing the hold status of records
    /// </summary>
    public string HoldPath => Path.Combine(StoragePath, "Hold");

    /// <summary>
    /// Path for storing user metadata
    /// </summary>
    public string MetaPath => Path.Combine(StoragePath, "Meta");

    /// <summary>
    /// Path for storing cache files
    /// </summary>
    public string FileCachePath => Path.Combine(StoragePath, "FileCache");

    /// <summary>
    /// Per-model-type freshness configuration, keyed by model type, overriding the defaults above.
    /// </summary>
    public IDictionary<Type, ModelConfig> ModelConfig { get; init; }  = new Dictionary<Type, ModelConfig>();

    /// <summary>
    /// Computes freshness seconds for a type depending on an optional passed in customValue, ModelConfig.FreshnessSeconds, DefaultFreshnessSeconds, ModelConfig.MaximumFreshnessSeconds and DefaultMaximumFreshnessSeconds. 
    /// Attempts to get a ModelConfig for the type, and defaults to DefaultFreshnessSeconds.
    /// A given customValue may override the above.
    /// If a MaximumFreshnessSeconds or DefaultMaximumFreshnessSeconds is set, the value is constrained by that and returned.
    /// </summary>
    /// <param name="model">The model type to compute freshness seconds for.</param>
    /// <param name="customValue">Optional freshness value overriding the configured defaults.</param>
    /// <returns>freshness value to use</returns>
    public int GetFreshnessSeconds(Type model, int? customValue = null) {
      ModelConfig.TryGetValue(model,out var modelConfig);
      var freshnessSeconds = customValue ?? modelConfig?.FreshnessSeconds ?? DefaultFreshnessSeconds;
      var maximum = modelConfig?.MaximumFreshnessSeconds ?? DefaultMaximumFreshnessSeconds;
      return maximum==null ? freshnessSeconds : Math.Min(freshnessSeconds, maximum.Value);
    }
    
    /// <summary>
    /// Gets the fallback freshness seconds for a type from its ModelConfig, defaulting to DefaultFallbackFreshnessSeconds.
    /// </summary>
    /// <param name="model">The model type to get the fallback freshness seconds for.</param>
    /// <returns>fallback freshness value to use</returns>
    public int GetFallbackFreshnessSeconds(Type model) {
      ModelConfig.TryGetValue(model,out var modelConfig);
      return modelConfig?.FallbackFreshnessSeconds ?? DefaultFallbackFreshnessSeconds;
    }

    /// <summary>
    /// Computes freshness seconds for blobs, using customValue if given, otherwise BlobFreshnessSeconds,
    /// constrained by DefaultMaximumFreshnessSeconds when that is set.
    /// </summary>
    /// <param name="customValue">Optional freshness value overriding BlobFreshnessSeconds.</param>
    /// <returns>blob freshness value to use</returns>
    public int GetBlobFreshnessSeconds(int? customValue = null) {
      var freshnessSeconds = customValue ?? BlobFreshnessSeconds;
      return DefaultMaximumFreshnessSeconds==null ? freshnessSeconds : Math.Min(freshnessSeconds, DefaultMaximumFreshnessSeconds.Value);
    }
  }
}

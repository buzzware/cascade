using System;
using System.Collections.Generic;
using System.IO;

namespace Buzzware.Cascade {

  public class ModelConfig {
    public readonly int FreshnessSeconds;
    public readonly int FallbackFreshnessSeconds;
    public int? MaximumFreshnessSeconds;

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

    public IDictionary<Type, ModelConfig> ModelConfig { get; init; }  = new Dictionary<Type, ModelConfig>();

    /// <summary>
    /// Computes freshness seconds for a type depending on an optional passed in customValue, ModelConfig.FreshnessSeconds, DefaultFreshnessSeconds, ModelConfig.MaximumFreshnessSeconds and DefaultMaximumFreshnessSeconds. 
    /// Attempts to get a ModelConfig for the type, and defaults to DefaultFreshnessSeconds.
    /// A given customValue may override the above.
    /// If a MaximumFreshnessSeconds or DefaultMaximumFreshnessSeconds is set, the value is constrained by that and returned.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="customValue"></param>
    /// <returns>freshness value to use</returns>
    public int GetFreshnessSeconds(Type model, int? customValue = null) {
      ModelConfig.TryGetValue(model,out var modelConfig);
      var freshnessSeconds = customValue ?? modelConfig?.FreshnessSeconds ?? DefaultFreshnessSeconds;
      var maximum = modelConfig?.MaximumFreshnessSeconds ?? DefaultMaximumFreshnessSeconds;
      return maximum==null ? freshnessSeconds : Math.Min(freshnessSeconds, maximum.Value);
    }
    
    public int GetFallbackFreshnessSeconds(Type model) {
      ModelConfig.TryGetValue(model,out var modelConfig);
      return modelConfig?.FallbackFreshnessSeconds ?? DefaultFallbackFreshnessSeconds;
    }

    public int GetBlobFreshnessSeconds(int? customValue = null) {
      var freshnessSeconds = customValue ?? BlobFreshnessSeconds;
      return DefaultMaximumFreshnessSeconds==null ? freshnessSeconds : Math.Min(freshnessSeconds, DefaultMaximumFreshnessSeconds.Value);
    }
  }
}

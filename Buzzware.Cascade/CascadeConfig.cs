using System;
using System.Collections.Generic;
using System.IO;

namespace Buzzware.Cascade {

  public class ModelConfig {
    public readonly int FreshnessSeconds;
    public readonly int FallbackFreshnessSeconds;

    public ModelConfig(int freshnessSeconds, int fallbackFreshnessSeconds) {
      FreshnessSeconds = freshnessSeconds;
      FallbackFreshnessSeconds = fallbackFreshnessSeconds;
    }
  }
  
  /// <summary>
  /// User configuration class for the Cascade library
  /// </summary>
  public class CascadeConfig {
    public int MaxParallelRequests = 8;

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

    public int GetFreshnessSeconds(Type model) {
      ModelConfig.TryGetValue(model,out var modelConfig);
      return modelConfig?.FreshnessSeconds ?? DefaultFreshnessSeconds;
    }
    
    public int GetFallbackFreshnessSeconds(Type model) {
      ModelConfig.TryGetValue(model,out var modelConfig);
      return modelConfig?.FallbackFreshnessSeconds ?? DefaultFallbackFreshnessSeconds;
    }
  }
}

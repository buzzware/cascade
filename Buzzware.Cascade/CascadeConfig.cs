using System.IO;

namespace Buzzware.Cascade {

  /// <summary>
  /// User configuration class for the Cascade library
  /// </summary>
  public class CascadeConfig {
    public int MaxParallelRequests = 8;

    /// <summary>
    /// Default duration, in seconds, for which data is considered fresh.
    /// </summary>
    public int DefaultFreshnessSeconds = 5 * 60;

    /// <summary>
    /// Default duration, in seconds, for which populated data is considered fresh.
    /// </summary>
    public int DefaultPopulateFreshnessSeconds = 12 * 3600;

    /// <summary>
    /// Freshness value for when ConnectionOnline = True and network requests fail
    /// </summary>
    public int DefaultFallbackFreshnessSeconds = RequestOp.FALLBACK_NEVER;

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
  }
}

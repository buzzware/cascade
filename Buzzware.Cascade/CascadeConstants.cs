namespace Buzzware.Cascade {

  /// <summary>
  /// Represents how fresh a cached value is, relative to the freshness and fallback requirements of a request.
  /// </summary>
  public enum FreshnessState {
    /// <summary>The value arrived within the required freshness period</summary>
    Fresh,
    /// <summary>The value is older than the freshness period but within the fallback period</summary>
    Stale,
    /// <summary>The value is older than both the freshness and fallback periods</summary>
    Expired,
    /// <summary>No value</summary>
    Absent
  }  
  
  /// <summary>
  /// Provides constant values used throughout the Cascade library.
  /// </summary>
  public static class CascadeConstants {

    /// <summary>
    /// A constant for the "Hold" feature - the root meta path folder name under which hold records are stored
    /// </summary>
    public const string HOLD = "Hold";

    /// <summary>
    /// A constant for the "Blob" feature - the pseudo model type folder name used for blobs in hold meta paths
    /// </summary>
    public const string BLOB = "Blob";

  }
}

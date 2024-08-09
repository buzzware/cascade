using System.Collections.Generic;

namespace Buzzware.Cascade {

  /// <summary>
  /// Represents the options for a Cascade query including various settings 
  /// and preferences that can influence the query execution and results.
  /// </summary>
  class CascadeQueryOptions {

    /// <summary>
    /// A collection of strings that specify which associations 
    /// to populate as part of the query results.
    /// </summary>
    public IEnumerable<string> Populate;

  }
}

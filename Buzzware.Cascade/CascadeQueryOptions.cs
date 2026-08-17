using System.Collections.Generic;

namespace Buzzware.Cascade {

  /// <summary>
  /// Represents options for a Cascade query. Currently only specifies which associations to populate.
  /// </summary>
  class CascadeQueryOptions {

    /// <summary>
    /// A collection of strings that specify which associations 
    /// to populate as part of the query results.
    /// </summary>
    public IEnumerable<string> Populate;

  }
}

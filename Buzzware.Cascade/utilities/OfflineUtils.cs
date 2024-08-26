using System;

namespace Buzzware.Cascade {

  /// <summary>
  /// Provides utilities for creating and managing offline instances of SuperModel objects.
  /// </summary>
	public static class OfflineUtils {

    /// <summary>
    /// Creates an offline version of the given SuperModel instance. 
    /// The method assigns a new negative ID if the model's ID is zero or null, ensuring the object has a unique offline identity.
    /// </summary>
    /// <param name="value">The SuperModel instance to create an offline version of.</param>
    /// <param name="newGuid">A delegate function that generates a new GUID in string format.</param>
    /// <returns>A new SuperModel instance with a unique offline ID.</returns>
		public static SuperModel CreateOffline(SuperModel value, Func<string> newGuid) {
			// Clone the original SuperModel object to create a new instance
      SuperModel result = value.Clone();
      
      // Retrieve the current ID and its type of the SuperModel
			var id = CascadeTypeUtils.GetCascadeId(value);
			var idType = CascadeTypeUtils.GetCascadeIdType(value.GetType());
      
      // Assign a new negative integer ID if the current one is zero
			if (idType == typeof(int)) {
				if ((int)id == 0) {
					CascadeTypeUtils.SetCascadeId(result, id = RandomUtils.IntNegativeId());
				}
			}
      // Assign a new negative long ID if the current one is zero
			else if (idType == typeof(long)) {
				if ((long)id == 0)
					CascadeTypeUtils.SetCascadeId(result, id = RandomUtils.LongNegativeId());
			}
      // Assign a newly generated GUID string ID if the current one is null
			else if (idType == typeof(string)) {
				if (id == null) {
					id = newGuid();
					CascadeTypeUtils.SetCascadeId(result, id);
				}
			}
      // Throw an exception for unsupported ID types
			else {
				throw new Exception("Unsupported IdType");
			}

      // Return the new SuperModel instance with its offline ID
			return result;
		}
	}
}
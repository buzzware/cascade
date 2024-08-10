using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buzzware.Cascade {

  /// <summary>
  /// Interface for blob conversion within the Cascade library. Responsible for
  /// converting binary data to a specified type.
  /// </summary>
	public interface IBlobConverter {
  
    /// <summary>
    /// Converts a blob of binary data into an instance of the specified destination type.
    /// </summary>
    /// <param name="blob">The binary data to be converted. Can be null if there is no data to convert.</param>
    /// <param name="destinationPropertyType">The target type to convert the binary data into.</param>
    /// <returns>The converted object, or null if the conversion cannot be performed.</returns>
		object? Convert(byte[]? blob, Type destinationPropertyType);
	}
}

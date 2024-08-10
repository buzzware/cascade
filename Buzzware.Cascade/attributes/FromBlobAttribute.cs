using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Easy.Common.Extensions;

namespace Buzzware.Cascade {

  /// <summary>
  /// Attribute marking a property as the destination and type for conversion from a blob of a given path.
  /// The converter is also specified.
  ///
  /// https://buzzware.github.io/cascade/api_reference/md_api__docs_2using__cascade_2associations_2associations.html 
  /// </summary>
  public class FromBlobAttribute : Attribute {
		
    /// <summary>
    /// Name of a property holding the path to the blob
    /// </summary>
    public string PathProperty { get; }
		
    /// <summary>
    /// The converter to be used for converting the blob data.
    /// </summary>
    public IBlobConverter? Converter { get; }
		
    /// <summary>
    /// FromBlobAttribute Constructor. Validates converter and initializes properties.
    /// </summary>
    /// <param name="pathProperty">The name of the property containing the path to the blob data.</param>
    /// <param name="converter">The type of the converter class that should implement IBlobConverter for converting blob data.</param>
    public FromBlobAttribute(string pathProperty, Type? converter) {
      // Check if the provided converter type implements IBlobConverter
      if (!converter.Implements<IBlobConverter>())
        throw new ArgumentException("Converter must implement IBlobConverter");

      PathProperty = pathProperty;

      // Create an instance of the converter or assign null if the converter is not provided
      Converter = converter!=null ? (Activator.CreateInstance(converter) as IBlobConverter)! : null;
    }

    /// <summary>
    /// Converts the provided blob data into the specified destination property type.
    /// Uses the provided converter if available, otherwise returns the original blob data.
    /// </summary>
    /// <param name="blob">The byte array representing the data to be converted.</param>
    /// <param name="destinationPropertyType">The type to which the blob data should be converted.</param>
    /// <returns>The converted object to the destination property type or the original blob data if no converter is available.</returns>
    public object? ConvertToPropertyType(byte[] blob, Type destinationPropertyType) {
      return Converter != null ? Converter.Convert(blob, destinationPropertyType) : blob;
    }
  }
}

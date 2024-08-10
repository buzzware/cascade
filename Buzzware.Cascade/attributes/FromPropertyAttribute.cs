using System;
using System.Threading.Tasks;
using Easy.Common.Extensions;

namespace Buzzware.Cascade {

  /// <summary>
  /// Interface that defines a converter to convert a property value from one type to another.
  /// </summary>
  public interface IPropertyConverter {
    
    /// <summary>
    /// Converts an input object to a specified destination type using additional optional arguments.
    /// </summary>
    /// <param name="input">The object to be converted.</param>
    /// <param name="destinationType">The type to which the object should be converted.</param>
    /// <param name="args">Optional arguments that might be needed for conversion.</param>
    /// <returns>The converted object or null if conversion is not possible.</returns>
    Task<object?> Convert(object? input, Type destinationType, params object[] args);
  }

  /// <summary>
  /// Attribute to specify the source property and optional converter and arguments for 
  /// property mapping, allowing customization of how a property gets populated.
  /// </summary>
  public class FromPropertyAttribute : Attribute {

    /// <summary>
    /// The name of the source property from which to copy or convert a value.
    /// </summary>
    public string SourcePropertyName { get; }

    /// <summary>
    /// The converter used to transform the property value from the source to the target type. 
    /// It may be null if no conversion is required.
    /// </summary>
    public IPropertyConverter? Converter { get; }

    /// <summary>
    /// Optional arguments that may influence the conversion process.
    /// </summary>
    public object[] Arguments { get; }
    
    /// <summary>
    /// FromPropertyAttribute Constructor. 
    /// Validates and sets the converter class, source property name, and arguments.
    /// Throws an exception if the converter class does not implement IPropertyConverter.
    /// </summary>
    /// <param name="sourcePropertyName">Name of the property to be used as the source for conversion.</param>
    /// <param name="converterClass">Type of the converter class that must implement IPropertyConverter.</param>
    /// <param name="args">Additional arguments required by the converter.</param>
    /// <exception cref="ArgumentException">Thrown when converterClass does not implement IPropertyConverter.</exception>
    public FromPropertyAttribute(string sourcePropertyName, Type converterClass, params object[] args) {
      if (!converterClass.Implements<IPropertyConverter>())
        throw new ArgumentException("Converter must implement IBlobConverter");
      SourcePropertyName = sourcePropertyName;
      Converter = Activator.CreateInstance(converterClass) as IPropertyConverter;
      Arguments = args;
    }
  }
}

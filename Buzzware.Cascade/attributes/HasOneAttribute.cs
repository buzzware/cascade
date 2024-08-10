using System;

namespace Buzzware.Cascade {
  
  /// <summary>
  /// This attribute class is used within the Cascade library to indicate a 'has-one' relationship between entities.
  /// When applied to a property, it signifies that the property represents a single related entity 
  /// and specifies the property name that holds the foreign key to the related entity.
  /// </summary>
  public class HasOneAttribute : Attribute {
    
    /// <summary>
    /// The name of the property that contains the foreign key used to access the related entity.
    /// </summary>
    public string ForeignIdProperty { get; }

    /// <summary>
    /// HasOneAttribute Constructor.
    /// Initializes the attribute with the specified property name that represents the foreign key relationship.
    /// </summary>
    /// <param name="foreignIdProperty">Specifies the name of the property holding the foreign key.</param>
    public HasOneAttribute(string foreignIdProperty) {
      ForeignIdProperty = foreignIdProperty;
    }
  }
}
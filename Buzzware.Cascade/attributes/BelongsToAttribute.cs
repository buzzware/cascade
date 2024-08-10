using System;

namespace Buzzware.Cascade {

  /// <summary>
  /// Attribute to denote that a property is a reference to another record (of specified id) of another type.
  /// The id is read from the property specified by IdProperty, and the type is taken from the local property. 
  /// </summary>
  public class BelongsToAttribute : Attribute {
  
    /// <summary>
    /// The property name in the class that holds the id of the related entity.
    /// </summary>
    public string IdProperty { get; }

    /// <summary>
    /// BelongsToAttribute Constructor
    /// </summary>
    /// <param name="idProperty">The name of the property that contains the id of another entity.</param>
    public BelongsToAttribute(string idProperty) {
      IdProperty = idProperty;
    }
  }
}

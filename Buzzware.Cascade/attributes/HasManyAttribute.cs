using System;

namespace Buzzware.Cascade {

  /// <summary>
  /// Represents an attribute that specifies a one-to-many relationship.
  /// Used to mark a property intended to store a collection of child entities related to the local parent
  /// by a foreign key, identified by the specified property.
  /// </summary>
	public class HasManyAttribute : Attribute {
    
    /// <summary>
    /// The property name in the child entity that acts as the foreign key.
    /// </summary>
		public string ForeignIdProperty { get; }

    /// <summary>
    /// HasManyAttribute Constructor
    /// </summary>
    /// <param name="foreignIdProperty">The name of the property that represents the foreign key in the related entity.</param>
		public HasManyAttribute(string foreignIdProperty) {
			ForeignIdProperty = foreignIdProperty;
		}
	}
}

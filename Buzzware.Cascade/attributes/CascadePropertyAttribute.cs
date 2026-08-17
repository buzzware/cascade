using System;

namespace Buzzware.Cascade {

  /// <summary>
  /// Attribute used to explicitly specify the CascadePropertyKind of a property on a Cascade managed model.
  /// This overrides the default classification logic in FastReflection.
  /// </summary>
  public class CascadePropertyAttribute : Attribute {
    /// <summary>
    /// The kind of the property this attribute is applied to.
    /// </summary>
    public CascadePropertyKind Kind { get; }

    /// <summary>
    /// CascadePropertyAttribute Constructor
    /// </summary>
    /// <param name="kind">The kind to classify the property as, defaulting to CascadePropertyKind.Data.</param>
    public CascadePropertyAttribute(CascadePropertyKind kind = CascadePropertyKind.Data) {
      Kind = kind;
    }
  }

}

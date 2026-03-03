using System;

namespace Buzzware.Cascade {

  /// <summary>
  /// Attribute used to explicitly specify the CascadePropertyKind of a property on a Cascade managed model.
  /// This overrides the default classification logic in FastReflection.
  /// </summary>
  public class CascadePropertyAttribute : Attribute {
    public CascadePropertyKind Kind { get; }

    public CascadePropertyAttribute(CascadePropertyKind kind = CascadePropertyKind.Data) {
      Kind = kind;
    }
  }

}

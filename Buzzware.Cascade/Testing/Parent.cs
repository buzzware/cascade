using System.Collections.Generic;
namespace Buzzware.Cascade.Testing {

  /// <summary>
  /// A Parent model for demonstrating SuperModel and associations (with Child). Not a human parent.
  /// </summary>
  public class Parent : SuperModel {
        
    /// <summary>
    /// The unique identifier
    /// </summary>
    [Cascade.CascadeId]
    public long id {
      get => GetProperty(ref _id);
      set => SetProperty(ref _id, value);
    }
    private long _id;
        
    /// <summary>
    /// Represents a collection of Child entities associated with this Parent.
    /// It is mapped by a foreign key relationship based on the parentId property.
    /// </summary>
    [Cascade.HasMany(foreignIdProperty: "parentId")]
    public IEnumerable<Child>? Children {
      get => GetProperty(ref _children);
      set => SetProperty(ref _children, value);
    }
    private IEnumerable<Child>? _children;
        
    /// <summary>
    /// </summary>
    public string? colour {
      get => GetProperty(ref _colour);
      set => SetProperty(ref _colour, value);
    }
    private string? _colour;

    /// <summary>
    /// </summary>
    public string? Size {
      get => GetProperty(ref _size);
      set => SetProperty(ref _size, value);
    }
    private string? _size;

    /// <summary>
    /// Timestamp, in milliseconds, when the Parent entity was last updated.
    /// </summary>
    public long updatedAtMs {
      get => GetProperty(ref _updatedAtMs);
      set => SetProperty(ref _updatedAtMs, value);
    }
    private long _updatedAtMs;

    /// <summary>
    /// Parent Constructor
    /// </summary>
    public Parent() {
    }
        
    /// <summary>
    /// Creates a new Parent object with the same id, and optionally
    /// modified values for colour, size, and updatedAtMs. If these are
    /// not provided, the current object's values will be used.
    /// </summary>
    /// <param name="colour">colour</param>
    /// <param name="size">size</param>
    /// <param name="updatedAtMs">updatedAtMs</param>
    /// <returns>A new Parent object with specified or existing values.</returns>
    public Parent withChanges(
      string? colour = null,
      string? size = null,
      long? updatedAtMs = null
    ) {
      var result = new Parent() {id = this.id};
      result.colour = colour ?? this.colour;
      result.Size = size ?? this.Size;
      result.updatedAtMs = updatedAtMs ?? this.updatedAtMs;
      return result;
    }
  }
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade.Testing {

  /// <summary>
  /// A sample parent model with a string id, used by tests for associations with Child2.
  /// </summary>
  public class Parent2 : SuperModel {

    /// <summary>
    /// The unique identifier
    /// </summary>
    [CascadeId]
    public string? id {
      get => GetProperty(ref _id);
      set => SetProperty(ref _id, value);
    }
    private string? _id;

    /// <summary>
    /// The collection of Child2 models associated with this parent via Child2.parentId.
    /// </summary>
    [HasMany(foreignIdProperty: nameof(Child2.parentId))]
    public IReadOnlyList<Child2>? Children {
      get => GetProperty(ref _children);
      set => SetProperty(ref _children, value);
    }
    private IReadOnlyList<Child2>? _children;

    /// <summary>
    /// The name of the parent.
    /// </summary>
    public string? name {
      get => GetProperty(ref _name);
      set => SetProperty(ref _name, value);
    }
    private string? _name;
  }
}

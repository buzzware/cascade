using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade.Testing {

  public class Parent2 : SuperModel {

    [CascadeId]
    public string? id {
      get => GetProperty(ref _id);
      set => SetProperty(ref _id, value);
    }
    private string? _id;

    [HasMany(foreignIdProperty: nameof(Child2.parentId))]
    public IReadOnlyList<Child2>? Children {
      get => GetProperty(ref _children);
      set => SetProperty(ref _children, value);
    }
    private IReadOnlyList<Child2>? _children;

    public string? name {
      get => GetProperty(ref _name);
      set => SetProperty(ref _name, value);
    }
    private string? _name;
  }
}
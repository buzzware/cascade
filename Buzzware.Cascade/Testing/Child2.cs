using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade.Testing {

  public class Child2 : SuperModel {

    [JsonIgnore]
    [CascadeId]
    public string? CascadeKey => $"{parentId}__{id}";

    public string? id {
      get => GetProperty(ref _id);
      set => SetProperty(ref _id, value);
    }
    private string? _id;

    public string? parentId {
      get => GetProperty(ref _parentId);
      set => SetProperty(ref _parentId, value);
    }
    private string? _parentId;

    [BelongsTo(idProperty: nameof(parentId))]
    public Parent2? Parent { get; set; }

    [HasMany(foreignIdProperty: nameof(Toy.childId))]
    public IReadOnlyList<Toy>? Toys {
      get => GetProperty(ref _toys);
      set => SetProperty(ref _toys, value);
    }
    private IReadOnlyList<Toy>? _toys;

    public string? name {
      get => GetProperty(ref _name);
      set => SetProperty(ref _name, value);
    }
    private string? _name;
  }
}

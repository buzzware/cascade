using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade.Testing {

  /// <summary>
  /// A sample child model with a compound Cascade key, used by tests for associations with Parent2 and Toy.
  /// </summary>
  public class Child2 : SuperModel {

    /// <summary>
    /// The compound Cascade id key, combining parentId and id.
    /// </summary>
    [JsonIgnore]
    [CascadeId]
    public string? CascadeKey => $"{parentId}__{id}";

    /// <summary>
    /// The identifier of this child, combined with parentId to form the CascadeKey.
    /// </summary>
    public string? id {
      get => GetProperty(ref _id);
      set => SetProperty(ref _id, value);
    }
    private string? _id;

    /// <summary>
    /// The identifier of the Parent2 this child belongs to.
    /// </summary>
    public string? parentId {
      get => GetProperty(ref _parentId);
      set => SetProperty(ref _parentId, value);
    }
    private string? _parentId;

    /// <summary>
    /// The Parent2 association model property, resolved from parentId.
    /// </summary>
    [BelongsTo(idProperty: nameof(parentId))]
    public Parent2? Parent { get; set; }

    /// <summary>
    /// The collection of Toy models associated with this child via Toy.childId.
    /// </summary>
    [HasMany(foreignIdProperty: nameof(Toy.childId))]
    public IReadOnlyList<Toy>? Toys {
      get => GetProperty(ref _toys);
      set => SetProperty(ref _toys, value);
    }
    private IReadOnlyList<Toy>? _toys;

    /// <summary>
    /// The name of the child.
    /// </summary>
    public string? name {
      get => GetProperty(ref _name);
      set => SetProperty(ref _name, value);
    }
    private string? _name;
  }
}

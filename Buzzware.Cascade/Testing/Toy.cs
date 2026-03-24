using System.Text.Json.Serialization;

namespace Buzzware.Cascade.Testing {

  public class Toy : SuperModel {

    [CascadeId]
    public string id {
      get => GetProperty(ref _id);
      set => SetProperty(ref _id, value);
    }
    private string _id;

    public string childId {
      get => GetProperty(ref _childId);
      set => SetProperty(ref _childId, value);
    }
    private string _childId;
    
    [BelongsTo(idProperty: nameof(childId))]
    public Child2 Child { get; set; }

    public string name {
      get => GetProperty(ref _name);
      set => SetProperty(ref _name, value);
    }
    private string _name;
  }
}

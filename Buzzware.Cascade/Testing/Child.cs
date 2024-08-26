namespace Buzzware.Cascade.Testing {
  
  /// <summary>
  /// A Child model for demonstrating SuperModel and associations (with Parent). Not a human child.
  /// </summary>
  public class Child : SuperModel {

    /// <summary>
    /// The unique identifier
    /// </summary>
    [Cascade.CascadeId]
    public string id {
      get => GetProperty(ref _id);
      set => SetProperty(ref _id, value);
    }
    private string _id;

    /// <summary>
    /// The identifier of the Parent entity to which this Child belongs.
    /// </summary>
    public int? parentId {
      get => GetProperty(ref _parentId);
      set => SetProperty(ref _parentId, value);
    }
    private int? _parentId;
   
    /// <summary>
    /// The Parent association model property
    /// </summary>
    [Cascade.BelongsTo(idProperty: "parentId")]
    public Parent? Parent {
      get => GetProperty(ref _parent);
      set => SetProperty(ref _parent, value);
    }
    private Parent? _parent;
   
    /// <summary>
    /// The weight attribute of the Child model.
    /// </summary>
    public double? weight {
      get => GetProperty(ref _weight);
      set => SetProperty(ref _weight, value);
    }
    private double? _weight;

    /// <summary>
    /// The power attribute of the Child model.
    /// </summary>
    public double? power {
      get => GetProperty(ref _power);
      set => SetProperty(ref _power, value);
    }
    private double? _power;

    /// <summary>
    /// The age attribute of the Child model.
    /// </summary>
    public int age {
      get => GetProperty(ref _age);
      set => SetProperty(ref _age, value);
    }
    private int _age;

    /// <summary>
    /// A timestamp representing when this model was last updated, in milliseconds since 1970.
    /// </summary>
    public long updatedAtMs {
      get => GetProperty(ref _updatedAtMs);
      set => SetProperty(ref _updatedAtMs, value);
    }
    private long _updatedAtMs;

    /// <summary>
    /// Child Constructor
    /// </summary>
    public Child() {
    }

    /// <summary>
    /// Creates a new instance with modified properties, leaving unspecified properties unchanged.
    /// </summary>
    /// <param name="weight">weight</param>
    /// <param name="power">power</param>
    /// <param name="updatedAtMs">updatedAtMs</param>
    /// <returns>A new instance with updated properties</returns>
    public Child withChanges(
      double? weight = null,
      double? power = null,
      long? updatedAtMs = null
    ) {
      var result = new Child() {id = this.id};
      result.weight = weight ?? this.weight;
      result.power = power ?? this.power;
      result.updatedAtMs = updatedAtMs ?? this.updatedAtMs;
      return result;
    }
  }
}

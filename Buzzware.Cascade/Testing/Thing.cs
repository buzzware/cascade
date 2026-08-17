namespace Buzzware.Cascade.Testing {

  /// <summary>
  /// A sample Thing model inheriting from SuperModel, used for testing.
  /// </summary>
  public class Thing : SuperModel {

    /// <summary>
    /// Thing Constructor. Initializes a new Thing instance with an optional proxyFor parameter. 
    /// Allows the entity to proxy for another Thing instance.
    /// </summary>
    /// <param name="proxyFor">An optional Thing instance that this Thing can act as a proxy for. Default is null for non-proxy instances.</param>
    public Thing(Thing? proxyFor = null) : base(proxyFor) {
    }

    /// <summary>
    /// Thing Constructor. Initializes a new non-proxy Thing instance, as used when deserializing from JSON.
    /// </summary>
    public Thing() : base(null) {
    }

    /// <summary>
    /// A unique identifier for the Thing instance, marked with a CascadeId for internal referencing.
    /// </summary>
    [CascadeId]
    public int id {
      get => GetProperty(ref _id); 
      set => SetProperty(ref _id, value);
    }
    private int _id;

    /// <summary>
    /// The name property represents the name of the Thing.
    /// </summary>
    public string? name {
      get => GetProperty(ref _name); 
      set => SetProperty(ref _name, value);
    }
    private string? _name;

    /// <summary>
    /// The colour property indicates the color attribute of the Thing.
    /// </summary>
    public string? colour {
      get => GetProperty(ref _colour);
      set => SetProperty(ref _colour, value);
    }
    private string? _colour;
  }
}

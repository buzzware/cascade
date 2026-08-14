@page defining_models Defining Models 

Cascade requires that application models managed by Cascade inherit from a `SuperModel` base class. Here's how to use `SuperModel` to define your models. `SuperModel` has special functionality that requires the following patterns to be followed.

## Inheriting from SuperModel

To create a Cascade-compatible model, inherit from the `SuperModel` class:

```csharp
public class Docket : SuperModel
{
    // Model properties and methods
}
```

## Defining Properties

When defining properties in your model, the `GetProperty(ref backingField)` and `SetProperty(ref backingField, value)` methods provided by `SuperModel` must be used, together with a private backing field, to support the special functionality of `SuperModel` and Cascade (immutability, change tracking, the editable proxy feature and property change notifications).

### ID Property

Cascade-managed models require a property with the `[CascadeId]` attribute. Theoretically, any value type could work, but only string, int and long have been tested.

```csharp
public class Docket : SuperModel
{
    [CascadeId]
    public string id
    {
        get => GetProperty(ref _id);
        set => SetProperty(ref _id, value);
    }
    private string _id;

    // Or for integer IDs:
    // [CascadeId]
    // public int id
    // {
    //     get => GetProperty(ref _id);
    //     set => SetProperty(ref _id, value);
    // }
    // private int _id;
}
```

### Value Properties

The property names of your model should typically match exactly (including casing and any underscores) the property names from your server API (ignore the C# Style Guide property name capitalisation here).

```csharp
public class Docket : SuperModel
{    
    [CascadeId]
    public string id
    {
        get => GetProperty(ref _id);
        set => SetProperty(ref _id, value);
    }
    private string _id;

    public string description
    {
        get => GetProperty(ref _description);
        set => SetProperty(ref _description, value);
    }
    private string _description;

    public DateTime docketDate
    {
        get => GetProperty(ref _docketDate);
        set => SetProperty(ref _docketDate, value);
    }
    private DateTime _docketDate;

    public int quantity
    {
        get => GetProperty(ref _quantity);
        set => SetProperty(ref _quantity, value);
    }
    private int _quantity;
}
```

## Constructors

Due to the rules of C# and special functionality of Cascade, your models can either :

1. Not provide any constructor - this model will not support the proxy feature eg. for editing in forms
2. Provide both a constructor with the proxyFor parameter, and a default constructor for full functionality 

eg.
```csharp
		public Docket() {
		}

		public Docket(SuperModel proxyFor = null) : base(proxyFor) {
		}
```

See [SuperModel In Depth](#supermodel) for how the proxy feature is used for form editing.

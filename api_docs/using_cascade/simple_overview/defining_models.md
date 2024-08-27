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

When defining properties in your model, the `GetProperty<T>()` and `SetProperty(value)` methods provided by `SuperModel` must be used to support special functionality of `SuperModel` and Cascade. 

### ID Property

Cascade-managed models require a property with the `[CascadeId]` attribute. Theoretically, any value type could work, but only string, int and long have been tested.

```csharp
public class Docket : SuperModel
{
    [CascadeId]
    public string id
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    // Or for integer IDs:
    // public int id
    // {
    //     get => GetProperty<int>();
    //     set => SetProperty(value);
    // }    
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
        get => GetProperty<string>();
        set => SetProperty(value);
    }
       
    public string description
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public DateTime docketDate
    {
        get => GetProperty<DateTime>();
        set => SetProperty(value);
    }

    public int quantity
    {
        get => GetProperty<int>();
        set => SetProperty(value);
    }
}
```

## Constructors

Due to the rules of C# and special functionality of Cascade, your models can either :

1. Not provide a constructor - this model will not support the proxy feature eg. for editing in forms
2. Provide both a constructor with the proxyFor parameter, and a default constructor for full functionality 

eg.
```
		public Docket() {
		}

		public Docket(SuperModel proxyFor = null) : base(proxyFor) {
		}
```


# Populating Associations

A Child has an association property Parent for holding a reference to its Parent.

```csharp
	public class Child : SuperModel {
		
		[Cascade.CascadeId]
		public string id { get; set; }
        
		public int? parentId { get; set; }
		
		[Cascade.BelongsTo(idProperty: "parentId")]
		public Parent? Parent { get; set; }
		
		public int age { get; set; }
    }
```


Let's assume that a Child with id=1 exists and it has parentId=5 and a Parent with id 5 also exists. The first time that `child = await Cascade.Get<Child>(1)` is called,
the returned child.Parent property will always be null because it has not been populated.

To populate Parent we can do :

```csharp
await Cascade.Populate(child,nameof(Child.Parent));
```

and then child.Parent will equal the instance of Parent with id=5.

Likewise, we can do : 

```csharp
await Cascade.Populate(parent,nameof(Parent.Children));
```

> Populate will populate any association property ie a property with one of the supported attributes BelongsTo, HasMany, HasOne or FromBlob.

We can also pass multiple associations 

```csharp
await Cascade.Populate(order, new []{ nameof(Order.Supplier),nameof(Order.Address) });
```

For convenience, we can also pass the `populate` option to other methods like `Get` and `Query`, 
which call `Populate()` internally :

```csharp
child = await Cascade.Get<Child>(1,populate: new []{nameof(Child.Parent)});
```

## How it Works

1. Values required to retrieve the value for the association property are retrieved as referenced by the attribute. 
2. Populate internally uses these values with other methods such as Get, Query and/or BlobGet to perform the necessary operation to get the value 
for the association property.
3. the association property is set on the main thread (necessary for any bound UI that will update) and using SuperModel's `__mutateWith` method in order to set the property regardless of whether `__mutable` is true or false.

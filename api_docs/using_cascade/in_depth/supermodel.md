# SuperModel In Depth

## Normally Immutable

Much has been written about the benefits of immutable data. The modern language Rust makes parameters, 
variables etc immutable by default. Of particular benefit with Cascade is that the single current instance 
per id of each model class can be held in a 
memory cache and also referenced freely in application data structures without fear of being modified 
by one part of the application and that affecting other parts. The current instance reflects exactly the state of 
the server record at the point in time it was sent by the server, without any changes.

All Cascade methods that return models do so with __mutable == False. That means that any attempt to change 
their properties (properly implemented using GetProperty/SetProperty) will throw a MutationAttemptException.

### Form Editing

Forms are intended to be implemented using the "editable proxy" feature of SuperModel :

```csharp
var serverThing = Cascade.Get<Thing>(1);
// serverThing.colour == "red"
var editableThing = new Thing(serverThing);
// editableThing.colour == "red"
editableThing.colour = "blue";
// editableThing.colour == "blue"
// Dictionary<string,object?> changes = editableThing.__GetChanges();
// changes == { ["colour"] = "blue" }
```

`editableThing` can be used like any other model in dotnet applications eg. can be bound to UI controls, even with TwoWay binding.

As shown above, __GetChanges() then returns a dictionary of changes to be sent to the server like so :

```csharp
serverThing = await Cascade.Update(serverThing,changes);
// serverThing.colour == "blue"
editableThing = new Thing(serverThing);
// editableThing.colour == "blue"
```

This means that the data properties of models returned by Cascade remain unmodified from when they arrived from the server. 

### Updating Association Properties

The `Populate()` method and `populate` option on `Get()` and `Query()` methods are used to ensure association properties are set or updated as required.

@page supermodel SuperModel In Depth

## Normally Immutable

Much has been written about the benefits of immutable data. The modern language Rust makes parameters, 
variables etc immutable by default. Of particular benefit with Cascade is that the single current instance 
per id of each model class can be held in a 
memory cache and also referenced freely in application data structures without fear of being modified 
by one part of the application and that affecting other parts. The current instance reflects exactly the state of 
the server record at the point in time it was sent by the server, without any changes.

All Cascade methods that return models do so with __mutable == False. That means that any attempt to change 
their properties (properly implemented using GetProperty/SetProperty) will throw a MutationAttemptException.

### Editable Proxy Form Editing

Forms are intended to be implemented using the "editable proxy" feature of SuperModel :

```csharp
var serverThing = await Cascade.Get<Thing>(1);
// serverThing.colour == "red"
var editableThing = new Thing(serverThing);
// editableThing.colour == "red"
editableThing.colour = "blue";
// editableThing.colour == "blue"
// IDictionary<string,object?> changes = editableThing.__GetChanges();
// changes == { ["colour"] = "blue" }
```

`editableThing` can be used like any other model in dotnet applications eg. can be bound to UI controls, even with TwoWay binding.
It reads through to the proxied instance for properties that have not been changed, stores any changed values itself,
and raises PropertyChanged events as normal.

As shown above, __GetChanges() then returns a dictionary of changes to be sent to the server like so :

```csharp
serverThing = await Cascade.Update(serverThing,changes);
// serverThing.colour == "blue"
editableThing = new Thing(serverThing);
// editableThing.colour == "blue"
```

This means that the data properties of models returned by Cascade remain unmodified from when they arrived from the server. 

### Useful Proxy Members

- `__HasChanges` : true when any property has been changed relative to the proxied instance. Raises PropertyChanged so it can be bound eg. to enable a Save button.
- `__GetChanges()` : returns a dictionary of the changed property names and values.
- `__ClearChanges()` : discards changes, reverting the proxy to the values of the proxied instance.
- `__SetProxyFor(value, keepChanges, raiseIncoming)` : switch the proxy to wrap a different (eg. newly arrived) instance, optionally keeping the user's uncommitted changes.
- `__mutateWith(action)` : perform a mutation on a model regardless of __mutable (used internally by Cascade eg. to set association properties).
- `Clone(changes)` : create a shallow copy, optionally with the given changes applied.

### Updating Association Properties

The `Populate()` method and `populate` option on `Get()` and `Query()` methods are used to ensure association properties are set or updated as required.

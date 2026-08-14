@page association_helpers Association Helper Methods

## Generic Helper Methods

`SetAssociation(object target, string propertyName, object value)`

`SetAssociation(IEnumerable targets, string propertyName, object value)`

SetAssociation simply sets an association property on the given model (or the same property on each of the given models),
but ensures it is set on the main thread and regardless of the state of the __mutable property on the model.

SetAssociation is rarely needed and should be avoided because it does not attempt to set the equivalent entry in the cache layers.

`SetModelCollectionProperty(object target, CascadePropertyInfo propertyInfo, object value)`

SetModelCollectionProperty is like SetAssociation except that it is for enumerable (eg. HasMany) properties, and it
ensures that the given value is materialized and matches the singular type of the property (converting to an
ImmutableArray of the property item type as necessary).

## HasMany Helper Methods

The following helper methods are provided to address many of the [HasMany limitations](#how_associations_work).
They update both the association property on the model and the cached "where collection" backing it.

`HasManyReplace(SuperModel model, string property, IEnumerable<object> models)`

Replaces the value of the given HasMany property with the given IEnumerable of models and updates the caches appropriately.
This is needed eg. when you add models to a HasMany association.

`HasManyAddItem(SuperModel model, string property, SuperModel hasManyItem)`

Adds an item to the HasMany association of a model, updating both the property and the caches.

`HasManyRemoveItem(SuperModel model, string property, SuperModel hasManyItem)`

Removes an item from the association property and cached collection, matching by id.

`HasManyReplaceItem(SuperModel model, string property, SuperModel hasManyItem)`

Replaces an item in the association property and cached collection, matching by id (does nothing if not present).

`HasManyEnsureItem(SuperModel model, string property, SuperModel hasManyItem)`

Ensures that an item occurs in the association property and cached collection, matching by id (adds or replaces as necessary to avoid duplicates).

## HasOne Helper Methods

`UpdateHasOne(SuperModel model, string property, object value)`

Replaces the value of the given HasOne property with the given model.

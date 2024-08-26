@page association_helpers

# Association Helper Methods



## Generic Helper Methods

`SetAssociation(object target, string propertyName, object value)`

SetAssociation simply sets and association property on the given model, but ensures it is set on the main thread and regardless of the state of the __mutable property on the model.

SetAssociation is rarely needed and should be avoided because it does not attempt to set the equivalent entry in the cache layers.

`SetModelCollectionProperty(object target, PropertyInfo propertyInfo, object value)`

SetModelCollectionProperty ia like SetAssociation (it calls it) except that for enumerable properties it ensures that the value matches the type of the property.



## HasMany Helper Methods

The following helper methods are provided to address many of the above limitations.

`HasManyReplace(SuperModel model, string property, IEnumerable<object> models)`

Replaces the value of the given HasMany property with the given IEnumerable of models and updates the caches appropriately.
This is needed eg. when you add models to a HasMany association.

`HasManyAddItem(SuperModel model, string property, SuperModel hasManyItem)`

Adds an item to the HasMany association of a model, updating both the property and the caches.

`HasManyRemoveItem(SuperModel model, string property, SuperModel hasManyItem, bool remove = false, bool ensureItem = false)`

Removes an item from the association property and cached collection, matching by id.

`HasManyReplaceItem(SuperModel model, string property, SuperModel hasManyItem)`

Replaces an item in the association property and cached collection, matching by id.

`HasManyEnsureItem(SuperModel model, string property, SuperModel hasManyItem)`

Ensures that an item occurs in the association property and cached collection, matching by id (adds or replaces as necessary to avoid duplicates).


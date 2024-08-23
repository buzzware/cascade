# How Associations Work

> Please read [associations](./associations.html) before this

Association properties are treated differently to normal data properties.

1. Their value is typically derived from their attribute parameters and what they reference, such as foreign and local keys.
2. When models are created as an instance eg. deserialized from a server response or loaded from a file based cache association properties are null by default.
3. An association set on a model instance will remain set, and if that same instance is retrieved later from the memory cache the association will still
   be set whether it was requested to be populated or not. This cannot be relied on.
4. So if your application code requires an association to be set, **you must request it to be populated to be sure that it is**, and **don't be surprised if it is
   already populated when you did not ask for it to be**.

## Association Helper Methods

`SetAssociation(object target, string propertyName, object value)`

SetAssociation simply sets and association property on the given model, but ensures it is set on the main thread and regardless of the state of the __mutable property on the model.

SetAssociation is rarely needed and should be avoided because it does not attempt to set the equivalent entry in the cache layers.

`SetModelCollectionProperty(object target, PropertyInfo propertyInfo, object value)`

SetModelCollectionProperty ia like SetAssociation (it calls it) except that for enumerable properties it ensures that the value matches the type of the property.

## How HasMany Works

A HasMany property holds an array of models of a foreign type (specified by the singular type of the array) that refer to the local model by
the specified foreign key property holding the local model primary key value.

```csharp
    [HasMany(foreignIdProperty: nameof(DocketItem.docketId))]
    public ImmutableArray<DocketItem> Items
    {
        get => GetProperty(ref _Items);
        set => SetProperty(ref _Items, value);
    }
    private ImmutableArray<DocketItem> _Items;
```

Cascade populates this using a Query, including generating a unique collection key using CascadeUtils.WhereCollectionKey() and criteria.

### HasMany Limitations

Its important to note the following intentional limitations (by design) :

1. Like all Cascade queries, the records that match is determined by the origin. While there are generally no locally executed queries in Cascade, there is nothing to stop an ICascadeOrigin implementation from executing local queries.
2. Like all Cascade queries, if a Docket HasMany DocketItems, and a new DocketItem is created or updated such that the docketId newly matches a HasMany query, the query collection and association property does not automatically have the new DocketItem added. 
3. Models are also not automatically removed from associations eg. when changes on the origin mean they no longer belong  
4. Model instances held in a HasMany association array are the current instances as of when the property was last populated. They are not automatically refreshed when new instances arrive from the origin.

### HasMany Helper Methods

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


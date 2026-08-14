@page glossary Glossary

### Freshness

How recently did a data record arrive from the origin (server)? Freshness requirements are expressed in integer seconds
and are inverse ie a value of 0 means the most fresh (must have just arrived from the origin), while a higher value
means older cached data is acceptable. See [Freshness, Fallback and Time](#freshness_and_fallback).

### Fallback Freshness

A value of acceptable freshness (in seconds) applied when the origin cannot be reached ie when either

1. ConnectionOnline == False (offline mode), or
2. ConnectionOnline == True and a request to the origin has failed due to network or server failure.

In those situations, if the required value is in a cache and is fresher (more recent) than the
fallback freshness value, it will be returned; otherwise an exception is thrown.

### Origin

The implementation of `ICascadeOrigin` that adapts Cascade to your server API. The origin is the source of truth
that caches are filled from. See [Implementing a Custom ICascadeOrigin](#implementing_origin).

### Collection

A named, cached array of model ids of a single model type. Query results are stored as collections.
See [Collections in Depth](#collections_in_depth).

### Hold

Marking a model, collection or blob to be preserved in caches even when the caches are cleared, normally
to keep it available offline.

### Data Properties

Properties of a model that hold data values (numbers, strings, dates etc). These are serialized and deserialized
when models are transferred to and from the origin and file based caches.

### Association Properties

Properties of a model that hold a reference or array of references to other models, or the result of a blob conversion (using `[FromBlob]`).
These properties are not serialized or deserialized as they are not recognised by the origin.
For example :

```csharp
public class Child : SuperModel {

    // a data property (the foreign key)
    public string parentId {
        get => GetProperty(ref _parentId);
        set => SetProperty(ref _parentId, value);
    }
    private string _parentId;

    // an association property (not serialized; set by Populate)
    [BelongsTo(idProperty: nameof(parentId))]
    public Parent Parent {
        get => GetProperty(ref _Parent);
        set => SetProperty(ref _Parent, value);
    }
    private Parent _Parent;
}
```

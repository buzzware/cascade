@page updating_models Updating Models

Cascade provides two primary methods for updating existing models in your Origin: `Update` and `Replace`. This guide explains how to use these methods effectively.

Remember that model instances returned by Cascade are immutable (`__mutable == false`) - you cannot simply set
properties on them. Instead you describe the changes to apply (`Update`), or provide a whole new instance (`Replace`).
Both methods return a new immutable instance; the instance passed in is not modified.

## Update

The `Update` method allows you to modify specific fields of an existing model.

### Basic Usage

```csharp
public async Task<M?> Update<M>(M model, IDictionary<string, object?> changes) where M : class
```

### Parameters

- `M`: The type of model you're updating.
- `model`: The existing model instance.
- `changes`: A dictionary of property names and their new values.

### Return Value

The updated model instance from the origin, or `null` if the record no longer exists.

### Example

```csharp
var changes = new Dictionary<string, object?>
{
    ["description"] = "Updated description",
    ["status"] = "In Progress"
};

Docket updatedDocket = await AppCommon.Cascade.Update(existingDocket, changes);
```

A common pattern for forms is to bind an editable proxy instance to the UI and then get the changes dictionary from
it with `__GetChanges()` - see [SuperModel In Depth](#supermodel).

## Replace

The `Replace` method replaces an entire model with a new version.

### Basic Usage

```csharp
public async Task<M> Replace<M>(M model)
```

### Parameters

- `M`: The type of model you're replacing.
- `model`: The new model instance that will replace the existing one.

### Example

Because instances returned by Cascade are immutable, construct the new version using `Clone` with changes
(or build a new instance yourself with the same id):

```csharp
var newVersion = (Docket)existingDocket.Clone(new Dictionary<string, object?> {
    ["description"] = "Completely new description",
    ["status"] = "Completed"
});

Docket replacedDocket = await AppCommon.Cascade.Replace(newVersion);
```

## Key Differences

- `Update` modifies only the specified fields, leaving others unchanged.
- `Replace` overwrites the entire model with the new instance provided.

## Associations

Association properties set on the given model are "maintained" - carried over to the returned instance where
consistent with any changed foreign keys. See
[Associations Maintained through Create, Update and Replace Operations](#maintained_associations).

## Offline

When `ConnectionOnline == false`, both `Update` and `Replace` are queued as pending changes and applied to the
origin later (see `UploadChangesPending`). The returned instance reflects the changes as if they had been applied.

## Model IDs

Both `Update` and `Replace` operations require the model to have a valid ID (the property marked with
`[CascadeId]`). Cascade uses this ID to identify which model in the Origin should be updated or replaced.
Ensure your model has the correct ID before calling `Update` or `Replace`.

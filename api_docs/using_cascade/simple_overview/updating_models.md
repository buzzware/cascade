@page updating_models Updating Models

Cascade provides two primary methods for updating existing models in your Origin: `Update` and `Replace`. This guide explains how to use these methods effectively.

## Update

The `Update` method allows you to modify specific fields of an existing model.

### Basic Usage

```csharp
public async Task<M> Update<M>(M model, IDictionary<string, object> changes) where M : class
```

### Parameters

- `M`: The type of model you're updating.
- `model`: The existing model instance.
- `changes`: A dictionary of property names and their new values.

### Example

```csharp
var changes = new Dictionary<string, object>
{
    ["Description"] = "Updated description",
    ["Status"] = "In Progress"
};

Docket updatedDocket = await AppCommon.Cascade.Update(existingDocket, changes);
```

## Replace

The `Replace` method replaces an entire model with a new version.

### Basic Usage

```csharp
public async Task<M> Replace<M>(M model) where M : class
```

### Parameters

- `M`: The type of model you're replacing.
- `model`: The new model instance that will replace the existing one.

### Example

```csharp
existingDocket.Description = "Completely new description";
existingDocket.Status = "Completed";

Docket replacedDocket = await AppCommon.Cascade.Replace(existingDocket);
```

## Key Differences

- `Update` modifies only the specified fields, leaving others unchanged.
- `Replace` overwrites the entire model with the new instance provided.

## Model IDs

Both `Update` and `Replace` operations require the model to have a valid ID. Cascade uses this ID to identify which model in the Origin should be updated or replaced.

```csharp
// For string IDs
public string Id { get; set; }

// For integer IDs
public int Id { get; set; }
```

Ensure your model has the correct ID before calling `Update` or `Replace`.

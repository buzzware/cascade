@page deleting_models Deleting Models

Cascade provides a `Destroy` method to remove model instances from your origin and any caches.

```csharp
public async Task Destroy<M>(M model)
```

### Parameters

- `M`: The type of model you're deleting.
- `model`: The instance of the model you want to delete.

### Return Value

The method returns a `Task`, as it's an asynchronous operation with no return value.

There is also a `DestroyResponse` variant that returns the full `OpResponse` detail of the operation.

## Basic Usage

Here's a simple example of how to use the `Destroy` method to delete a Docket:

```csharp
public async Task DeleteDocket(Docket docket)
{
    await AppCommon.Cascade.Destroy(docket);
}
```

## Offline

When `ConnectionOnline == false`, the destroy is queued as a pending change and applied to the origin later
(see `UploadChangesPending`).

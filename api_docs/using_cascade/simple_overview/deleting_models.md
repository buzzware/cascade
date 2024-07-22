# Deleting Models

Cascade provides a `Destroy` method to remove model instances from your origin and any caches.

```csharp
public async Task Destroy<M>(M model) where M : class
```

### Parameters

- `M`: The type of model you're deleting.
- `model`: The instance of the model you want to delete.

### Return Value

The method returns a `Task`, as it's an asynchronous operation with no return value.

## Basic Usage

Here's a simple example of how to use the `Destroy` method to delete a Docket:

```csharp
public async Task DeleteDocket(Docket docket)
{
    await AppCommon.Cascade.Destroy(docket);
}
```

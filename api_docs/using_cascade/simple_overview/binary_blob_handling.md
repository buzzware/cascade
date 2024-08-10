# Binary Blob Handling

Cascade provides methods to handle binary large objects (blobs) stored in your Origin. 
This document outlines the process of using Cascade's `BlobGet`, `BlobPut`, and `BlobDestroy` 
methods to manage binary data.

Blobs are normally cached by at least one cache layer, and also handled by the origin.

## Getting a Blob

To retrieve a blob from the Origin, use the `BlobGet` method:

```csharp
public async Task<byte[]> BlobGet(string path)
{
    byte[] blobData = await AppCommon.Cascade.BlobGet(path);
    return blobData;
}
```

The `path` parameter is a string that uniquely identifies the blob in the Origin.

## Putting a Blob

To store or update a blob in the Origin, use the `BlobPut` method:

```csharp
public async Task BlobPut(string path, byte[] data)
{
    await AppCommon.Cascade.BlobPut(path, data);
}
```

- `path`: A string that uniquely identifies where the blob should be stored.
- `data`: The binary data to be stored.

## Destroying a Blob

To remove a blob from the Origin, use the `BlobDestroy` method:

```csharp
public async Task BlobDestroy(string path)
{
    await AppCommon.Cascade.BlobDestroy(path);
}
```

The `path` parameter specifies which blob to delete from the Origin.


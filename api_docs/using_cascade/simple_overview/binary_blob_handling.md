# Binary Blob Handling

Cascade provides methods to handle binary large objects (blobs). It provides the same benefits as for models : 
caching, persistance, offline, and abstraction.
This document outlines the process of using Cascade's `BlobGet`, `BlobPut`, and `BlobDestroy` 
methods to manage binary data.

Typically blobs would be cached by a file based cache, and handled by the origin using an object 
storage service or file system.

Unlike models, blobs do not have an Update method because in Cascade blobs cannot be modified, 
but they can be replaced.

## Getting a Blob

To retrieve a blob, use the `BlobGet` method:

```csharp
public async Task<byte[]> BlobGet(string path)
{
    byte[] blobData = await AppCommon.Cascade.BlobGet(path);
    return blobData;
}
```

The `path` parameter is a string that uniquely identifies the blob in the Origin.

BlobGet works just like Get, where the path is equivalent to the id.  

## Putting a Blob

To store or replace a blob, use the `BlobPut` method:

```csharp
public async Task BlobPut(string path, byte[] data)
{
    await AppCommon.Cascade.BlobPut(path, data);
}
```

- `path`: A string that uniquely identifies where the blob should be stored.
- `data`: The binary data to be stored.

BlobPut works just like Replace, where the path is equivalent to the id.

## Destroying a Blob

To remove a blob, use the `BlobDestroy` method:

```csharp
public async Task BlobDestroy(string path)
{
    await AppCommon.Cascade.BlobDestroy(path);
}
```

The `path` parameter specifies which blob to delete from the Origin.


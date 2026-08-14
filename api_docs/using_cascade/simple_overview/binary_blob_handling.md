@page binary_blob_handling Binary Blob Handling

Cascade provides methods to handle binary large objects (blobs). It provides the same benefits as for models : 
caching, persistence, offline, and abstraction.
This document outlines the process of using Cascade's `BlobGet`, `BlobPut`, and `BlobDestroy` 
methods to manage binary data.

Typically blobs would be cached by a file based cache, and handled by the origin using an object 
storage service or file system.

Unlike models, blobs do not have an Update method because in Cascade blobs cannot be modified, 
but they can be replaced.

## Getting a Blob

To retrieve a blob, use the `BlobGet` method:

```csharp
byte[]? blobData = await AppCommon.Cascade.BlobGet(path);
```

The `path` parameter is a string that uniquely identifies the blob in the Origin.

BlobGet works just like Get, where the path is equivalent to the id. It accepts the same optional
`freshnessSeconds`, `fallbackFreshnessSeconds`, `hold` and `sequenceBeganMs` parameters.

Related methods :

- `BlobGetStream(path, ...)` returns the blob as a `Stream` instead of a byte array.
- `BlobGetFilePath(path, ...)` returns an absolute local file path to the cached blob file (requires a cache layer with `SupportsGetBlobAbsoluteFilePath == true` such as one including FileBlobCache).
- `BlobDownload(path, ...)` downloads the blob into the local cache without returning the data.
- `BlobExists(path, ...)` tests whether the blob exists.

## Putting a Blob

To store or replace a blob, use the `BlobPut` method:

```csharp
await AppCommon.Cascade.BlobPut(path, data);
```

- `path`: A string that uniquely identifies where the blob should be stored.
- `data`: The binary data to be stored, as a `byte[]` or a `Stream`.

BlobPut works just like Replace, where the path is equivalent to the id.

## Destroying a Blob

To remove a blob, use the `BlobDestroy` method:

```csharp
await AppCommon.Cascade.BlobDestroy(path);
```

The `path` parameter specifies which blob to delete from the Origin.

## Clearing Cached Blobs

- `BlobClear(path)` removes a blob from the caches only, without affecting the origin.
- `ClearBlobs(exceptHeld, olderThan)` clears cached blobs in bulk.

## Models and Blobs

The `[FromBlob]` attribute can declare a model property to be populated from a blob, converted to a friendly
type such as a bitmap - see [Associations](#associations). For more detail on blob caching and ETags see
[Blobs In Depth](#blobs_in_depth).

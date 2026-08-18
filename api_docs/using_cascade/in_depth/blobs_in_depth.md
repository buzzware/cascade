@page blobs_in_depth Blobs In Depth

## Why ETags

An [Etag](https://en.wikipedia.org/wiki/HTTP_ETag) is a hash or "fingerprint" of the
binary content of a file, and is normally used for comparison of two versions of a file
to determine whether they are the same without comparing every byte. This can avoid
unnecessary transfers of files that have already been transferred, and enable efficient
comparison of large files.

The Cascade "freshness" method reduces server requests by using cached values when the time since the last request is
less than the "freshness" seconds. This means there will still be server requests when serverside values have not changed,
which is hard or impossible to avoid, and isn't normally a great cost considering the reduction achieved by caching.
However it is different for blobs because they can be of a very large size.
ETags provide an effective method to eliminate repeat binary downloads of the same file content. 

## Using ETags to reduce binary downloads from cache

Once implemented correctly, ETags are invisible to Cascade application code except for the performance increase in 
avoiding repeat downloads of the same binary content. 

## Supporting ETags

The sequence for ETag support in Cascade goes like this :

1. The application calls BlobGet which creates a RequestOp
2. Cascade passes the RequestOp to the origin
3. The origin passes the request to the blob server eg. Azure storage  
4. Blob server returns the binary data and the OpResponse.ETag to Cascade
5. Cascade stores the blob binary and ETag in the cache
6. The application later calls BlobGet again with the same path and a freshness requirement the cached blob does not meet (eg. 0)
7. Cascade finds the cache version of the blob and its ETag
8. Cascade copies the cached ETag into the new RequestOp.Etag
9. Cascade passes the RequestOp to the Origin
10. The origin passes the request with the ETag to the blob server
11. If ETag matches the ETag calculated for the current blob content, a special code 
eg. HTTP 304 Not Modified is returned and not the binary content.
12. The origin returns a OpResponse with the ETag and Result = null
13. Cascade tests whether returned ETag matches the ETag from the cache, and if so, 
the cached value is returned and then the cached time of arrival is updated to the response time 

For ETags to work :

1) the blob cache must store and return OpResponse.ETag (FileBlobCache does) 
2) the ICascadeOrigin implementation must pass RequestOp.ETag to the blob server, and return the ETag from the blob server in OpResponse.ETag correctly

## Best Practices for Blob Handling eg. Images

Let's consider the case where an app needs to provide a photo gallery like feature, with photo taking, uploading and downloading, online and offline.

Initially it was tried storing binary blobs on model properties that were populated using a path string property, a 
Bitmap or similar property with the `[FromBlob]` attribute, a converter and the `cascade.Populate` method.

An image must be stored in at least 2 formats and places :

1. in JPEG format in a file and/or on the server
2. in the internal memory structures of the component displaying it

and through experimentation it has been decided that storing the blob in memory on the model, a third place, 
is an unproductive waste of memory.

A better approach is to keep the path on a model, set up `BlobFileCache`, and use 
`cascade.BlobGetFilePath(model.image_path)` 
to get a file system path that can be handed to UI components to load and show that blob.

Beware that the absolute file system path returned by `cascade.BlobGetFilePath` should not be persisted long term because 
mobile platform such as iOS and Android provide ethereal container paths to apps, which are unstable and should only be 
used only for short term operations. Instead, store relative image paths on the model, and combine them with operating 
system root paths at runtime.  



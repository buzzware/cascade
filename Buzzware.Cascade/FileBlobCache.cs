using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {

  /// <summary>
  /// This class is responsible for managing cached blobs within a local file system.
  /// It implements IBlobCache to store, fetch, and clear data.
  /// </summary>
  public class FileBlobCache : IBlobCache {
    private readonly string _tempDir;

    /// <summary>
    /// Name of the meta collection used to store blob etags.
    /// </summary>
    public const string BLOB_ETAGS = "BlobEtags";

    /// <summary>
    /// Separator substituted for '/' when encoding a blob path into a single etag file name.
    /// </summary>
    public const string BLOB_PATH_ALT_SEPARATOR = "_%_";

    /// <summary>
    /// Encodes a blob path into a flat name by trimming leading slashes and replacing '/' with BLOB_PATH_ALT_SEPARATOR.
    /// </summary>
    /// <param name="path">The blob path to encode.</param>
    /// <returns>The encoded path as a single flat name.</returns>
    public string EncodeBlobEtagPath(string path) {
      path = path.TrimStart('/', '\\');
      return path.Replace("/", BLOB_PATH_ALT_SEPARATOR);
    }

    /// <summary>
    /// Returns the meta path where the etag for the given blob path is stored.
    /// </summary>
    /// <param name="blobPath">The blob path, or null for the root etags collection path.</param>
    /// <returns>The BLOB_ETAGS collection path when blobPath is null, otherwise the path of the etag text file for the blob.</returns>
    public string BlobEtagPath(string? blobPath) {
      if (blobPath == null) {
        return BLOB_ETAGS;
      }
      else {
        return Path.Combine(BLOB_ETAGS, EncodeBlobEtagPath(blobPath)+".txt");
      }
    }

    /// <summary>
    /// Stores the etag for the given blob path using the Cascade meta store.
    /// </summary>
    /// <param name="blobPath">The blob path whose etag is being stored.</param>
    /// <param name="etag">The etag value to store, or null to clear it.</param>
    public void StoreBlobEtag(string blobPath, string? etag) {
      Cascade.MetaSet(BlobEtagPath(blobPath), etag);
    }

    /// <summary>
    /// Fetches the stored etag for the given blob path from the Cascade meta store.
    /// </summary>
    /// <param name="blobPath">The blob path whose etag is requested.</param>
    /// <returns>The stored etag string, or null if none is stored.</returns>
    public string? FetchBlobEtag(string blobPath) {
      return Cascade.MetaGet(BlobEtagPath(blobPath));
    }

    /// <summary>
    /// Clears the stored etag for the given blob path, or all stored blob etags when blobPath is null.
    /// </summary>
    /// <param name="blobPath">The blob path whose etag should be cleared, or null to clear all blob etags.</param>
    public void ClearBlobEtags(string? blobPath = null) {
      Cascade.MetaClearPath(BlobEtagPath(blobPath));
    }
    
    /// <summary>
    /// The Cascade data layer instance associated with this cache.
    /// </summary>
    public CascadeDataLayer Cascade { get; set; }

    /// <summary>
    /// The full path to the root directory where blobs are stored.
    /// </summary>
    private string FullBlobPath { get; }

    private readonly string _blobDirectory = "Blob";		

    /// <summary>
    /// FileBlobCache Constructor. Creates the necessary directory structure.
    /// </summary>
    /// <param name="tempDir">The base temporary directory used for storing blobs.</param>
    public FileBlobCache(string tempDir) {
      _tempDir = tempDir;
      FullBlobPath = ToFilePath(_blobDirectory);
      Directory.CreateDirectory(FullBlobPath);
    }

    /// <summary>
    /// Transforms a relative path to an absolute file path based on _tempDir
    /// </summary>
    /// <param name="path">The relative path to append to the temporary directory base.</param>
    /// <returns>The full file path as a string.</returns>
    protected string ToFilePath(string path) {
      path = path.TrimStart('/', '\\');
      return Path.Combine(_tempDir, path);
    }

    
    /// <summary>
    /// Constructs a path within the blob storage directory.
    /// </summary>
    /// <param name="path">The path relative to the blob directory.</param>
    /// <returns>The path of the blob relative to the cache root (blob directory name plus the given path).</returns>
    protected string GetBlobPath(string path) {
      path = path.TrimStart('/', '\\');
      return Path.Combine(_blobDirectory, path);
    }

    /// <summary>
    /// Does support GetAbsoluteFilePath below
    /// </summary>    
    public bool SupportsGetAbsoluteFilePath => true;
    
    /// <summary>
    /// Gets the absolute file path for a blob at the specified relative path.
    /// </summary>
    /// <param name="path">The relative path of the blob within the blob directory.</param>
    /// <returns>The complete file system path to the blob file as a string.</returns>
    public string GetAbsoluteFilePath(string path) {
      return ToFilePath(GetBlobPath(path)); 
    }

    /// <summary>
    /// Deletes the cached file for the given blob path and clears its stored etag.
    /// </summary>
    /// <param name="blobPath">The relative path of the blob to remove from the cache.</param>
    public async Task Clear(string blobPath) {
      var file = GetAbsoluteFilePath(blobPath);
      CascadeUtils.EnsureFileOperationSync(() => {
        File.Delete(file);
        ClearBlobEtags(blobPath);
      });
    }

    /// <summary>
    /// Clears the entire cache based on the specified parameters. 
    /// It can clear all files or only those older than a specific date and time, optionally preserving held blobs.
    /// </summary>
    /// <param name="exceptHeld">Specifies if the held blobs should be excluded from deletion.</param>
    /// <param name="olderThan">Specifies the cutoff date for deleting files. Files older than this date will be deleted.</param>
    public async Task ClearAll(bool exceptHeld, DateTime? olderThan) {
      if (exceptHeld || olderThan!=null) {
        // Conditionally delete files based on the olderThan date and whether they are held
        foreach (var file in Directory.GetFiles(FullBlobPath,"*",SearchOption.AllDirectories)) {
          if (olderThan != null) {
            var fileTime = File.GetLastWriteTimeUtc(file);
            if (fileTime.IsGreaterOrEqual(olderThan.Value))
              continue;
          }
          var path = CascadeUtils.GetRelativePath(FullBlobPath, file);
          if (exceptHeld) {
            if (Cascade!.IsHeldBlob(path))
              continue;
          }
          CascadeUtils.EnsureFileOperationSync(() => {
            File.Delete(file);
            ClearBlobEtags(path);
          });
        }
      } else {
        // Delete all files directly within the FullBlobPath
        foreach (var file in Directory.GetFiles(FullBlobPath,"*",SearchOption.AllDirectories)) {
          CascadeUtils.EnsureFileOperationSync(() => {
            File.Delete(file);
          });
        }
        ClearBlobEtags();
      }
    }

    /// <summary>
    /// Fetches a blob from the file cache based on the provided request operation.
    /// </summary>
    /// <param name="requestOp">The request operation containing the request details including the required freshness.</param>
    /// <returns>An OpResponse indicating the operation's result and associated data if the blob is found and valid.</returns>
    public async Task<OpResponse> Fetch(RequestOp requestOp) {
      if (requestOp.Verb != RequestVerb.BlobGet && requestOp.Verb != RequestVerb.BlobGetFilePath)
        throw new Exception("requestOp.Verb != Blob");
      bool exists;
      long arrivedAtMs;

      var path = requestOp.Id as string;
      if (path == null)
        throw new Exception("Id must be a string");
 
      path = path.TrimStart('/','\\');
      
      // Determine the path and existence of the blob file
      string blobFilePath = GetAbsoluteFilePath(path);
      exists = File.Exists(blobFilePath);
      arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(blobFilePath)) : -1;
      if (
        exists && 
        requestOp.FreshnessSeconds >= 0 && 
        (requestOp.FreshnessSeconds == RequestOp.FRESHNESS_ANY || (arrivedAtMs >= requestOp.FreshAfterMs))
      ) {
        object? result = null;
        if (requestOp.Verb == RequestVerb.BlobGet) {
          result = new FileStream(blobFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        } else if (requestOp.Verb == RequestVerb.BlobGetFilePath) {
          result = blobFilePath;
        }
        var etag = FetchBlobEtag(path);
        return new OpResponse(
          requestOp,
          Cascade?.NowMs ?? 0,
          exists: true,
          arrivedAtMs: arrivedAtMs, 
          result: result,
          eTag: etag
        ) {
          SourceName = this.GetType().Name
        };
      } else {
        return OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name);
      }
    }

    /// <summary>
    /// Stores the result of a given operation to the blob cache.
    /// If the result is empty, it deletes the corresponding file instead.
    /// </summary>
    /// <param name="opResponse">The response operation which includes the data to be stored or the command to delete.</param>
    /// <returns>The given opResponse, with the result replaced by a new stream over the stored file when a Stream was stored.</returns>
    public async Task<OpResponse> Store(OpResponse opResponse) {
      var path = opResponse.RequestOp.Id as string;
      path = path?.TrimStart('/','\\');
      long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;

      // Validate and process the path for storage
      if (path == null)
        throw new Exception("Bad path");
      if (!(opResponse.Result is null or byte[] or Stream))
        throw new ArgumentException("Result must be null, byte[] or Stream");
      try {
        string modelFilePath = GetAbsoluteFilePath(path)!;
        if (opResponse.ResultIsEmpty()) {
          File.Delete(modelFilePath);
        } else {
          if (opResponse.Result is byte[] bytes) {
            await StoreBlobBytes(modelFilePath, bytes, arrivedAt);
          } else if (opResponse.Result is Stream stream) {
            var newStream = await StoreBlobStream(modelFilePath, stream, arrivedAt);
            opResponse = opResponse.withChanges(result: newStream);
          } else {
            throw new ArgumentException("Result must be byte[] or Stream");
          }
        }
        StoreBlobEtag(path, opResponse.ETag);
      } catch (Exception e) {
        Log.Debug(e.Message);   // sharing violation exception sometimes happens here
      }
      return opResponse;
    }
    
    /// <summary>
    /// Marks the cached blob file as fresh by setting its last write time to the given arrival timestamp, if the file exists.
    /// </summary>
    /// <param name="blobPath">The relative path of the blob to mark as fresh.</param>
    /// <param name="arrivedAtMs">The arrival time to set, in ms since 1970.</param>
    public async Task NotifyBlobIsFresh(string blobPath, long arrivedAtMs) {
      var modelFilePath = GetAbsoluteFilePath(blobPath);
      if (File.Exists(modelFilePath))
        File.SetLastWriteTimeUtc(modelFilePath, CascadeUtils.fromUnixMilliseconds(arrivedAtMs));
    }
    
    /// <summary>
    /// Loads a blob from a file in the file cache, reading it as a byte array.
    /// </summary>
    /// <param name="path">The full path of the file to be loaded.</param>
    /// <returns>A byte array containing the file contents if the file exists; otherwise, null.</returns>
    private async Task<byte[]?> LoadBlob(string path) {
      byte[]? result = null;
      await CascadeUtils.EnsureFileOperation(async () => {
        if (File.Exists(path))
          result = await CascadeUtils.ReadBinaryFile(path, 8192);
      });
      return result;
    }

    /// <summary>
    /// Writes a byte array to the given absolute file path, creating the directory if needed, and sets the file's modification timestamp.
    /// </summary>
    /// <param name="path">The absolute path of the file to be written to.</param>
    /// <param name="blob">The byte array data to be stored in the file.</param>
    /// <param name="arrivedAt">The timestamp to set as the file's last modification time, in ms since 1970.</param>
    private async Task StoreBlobBytes(string path, byte[] blob, long arrivedAt) {
      await Task.Run(async () => {
        if (!Directory.Exists(Path.GetDirectoryName(path)))
          Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await CascadeUtils.EnsureFileOperation(async () => {
          await CascadeUtils.WriteBinaryFile(path, blob, 64*1024);
        });
        File.SetLastWriteTimeUtc(path, CascadeUtils.fromUnixMilliseconds(arrivedAt));
      });
    }
    
    /// <summary>
    /// Writes a stream to the given absolute file path, creating the directory if needed, and sets the file's modification timestamp.
    /// </summary>
    /// <param name="path">The absolute path of the file to be written to.</param>
    /// <param name="blob">The stream of data to be stored in the file.</param>
    /// <param name="arrivedAt">The timestamp to set as the file's last modification time, in ms since 1970.</param>
    /// <returns>A new readable stream over the stored file.</returns>
    private async Task<Stream> StoreBlobStream(string path, Stream blob, long arrivedAt) {
      Stream? result = null;
      await Task.Run(async () => {
        if (!Directory.Exists(Path.GetDirectoryName(path)))
          Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        result = await CascadeUtils.StreamToFileAndNewStream(blob, path);
        File.SetLastWriteTimeUtc(path, CascadeUtils.fromUnixMilliseconds(arrivedAt));
      });
      return result!;
    }
  }
}

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

    public const string BLOB_ETAGS = "BlobEtags";
    public const string BLOB_PATH_ALT_SEPARATOR = "_%_";

    public string EncodeBlobEtagPath(string path) {
      return path.Replace("/", BLOB_PATH_ALT_SEPARATOR);
    }

    public string BlobEtagPath(string? blobPath) {
      if (blobPath == null) {
        return BLOB_ETAGS;
      }
      else {
        return Path.Combine(BLOB_ETAGS, EncodeBlobEtagPath(blobPath)+".txt");
      }
    }

    public void StoreBlobEtag(string blobPath, string? etag) {
      Cascade.MetaSet(BlobEtagPath(blobPath), etag);
    }

    public string? FetchBlobEtag(string blobPath) {
      return Cascade.MetaGet(BlobEtagPath(blobPath));
    }

    public void ClearBlobEtags(string blobPath) {
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
      return Path.Combine(_tempDir, path);
    }

    /// <summary>
    /// Constructs a path within the blob storage directory.
    /// </summary>
    /// <param name="path">The path relative to the blob directory.</param>
    /// <returns>The full path to the specified blob as a string.</returns>
    protected string GetBlobPath(string path) { 
      return Path.Combine(_blobDirectory, path);
    }

    /// <summary>
    /// Gets the absolute file path for a model associated with a specified relative path.
    /// </summary>
    /// <param name="path">The relative path to the model file within the blob directory.</param>
    /// <returns>The complete path to the model file as a string.</returns>
    public string GetModelFilePath(string path) {
      return ToFilePath(GetBlobPath(path)); 
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
        foreach (var file in Directory.GetFiles(FullBlobPath)) {
          CascadeUtils.EnsureFileOperationSync(() => {
            File.Delete(file);
          });
        }
        ClearBlobEtags(FullBlobPath);
      }
    }

    /// <summary>
    /// Fetches a blob from the file cache based on the provided request operation.
    /// </summary>
    /// <param name="requestOp">The request operation containing the request details including the required freshness.</param>
    /// <returns>An OpResponse indicating the operation's result and associated data if the blob is found and valid.</returns>
    public async Task<OpResponse> Fetch(RequestOp requestOp) {
      if (requestOp.Verb != RequestVerb.BlobGet)
        throw new Exception("requestOp.Verb != Blob");
      bool exists;
      long arrivedAtMs;

      var path = requestOp.Id as string;
      if (path == null)
        throw new Exception("Id must be a string");
 
      path = path.TrimStart('/');
      
      // Determine the path and existence of the blob file
      string blobFilePath = GetModelFilePath(path);
      exists = File.Exists(blobFilePath);
      arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(blobFilePath)) : -1;
      if (
        exists && 
        requestOp.FreshnessSeconds >= 0 && 
        (requestOp.FreshnessSeconds == CascadeDataLayer.FRESHNESS_ANY || (arrivedAtMs >= requestOp.FreshAfterMs))
      ) {
        var loaded = await LoadBlob(blobFilePath);
        var etag = FetchBlobEtag(path);
        return new OpResponse(
          requestOp,
          Cascade?.NowMs ?? 0,
          exists: true,
          arrivedAtMs: arrivedAtMs, 
          result: loaded,
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
    public async Task Store(OpResponse opResponse) {
      var path = opResponse.RequestOp.Id as string;
      path = path?.TrimStart('/');
      long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;

      // Validate and process the path for storage
      if (path == null)
        throw new Exception("Bad path");
      try {
        string modelFilePath = GetModelFilePath(path)!;
        if (opResponse.ResultIsEmpty()) {
          File.Delete(modelFilePath);
        } else {
          if (!(opResponse.Result is byte[]))
            throw new ArgumentException("Result must be null or byte[]");
          await StoreBlob(modelFilePath, (byte[]) opResponse.Result, arrivedAt);
        }
        StoreBlobEtag(path, opResponse.ETag);
      } catch (Exception e) {
        Log.Debug(e.Message);   // sharing violation exception sometimes happens here
      }
    }

    public async Task NotifyBlobIsFresh(string blobPath, long arrivedAtMs) {
      var modelFilePath = GetModelFilePath(blobPath);
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
    /// Writes a byte array to a relative path and updates the file's modification timestamp.
    /// </summary>
    /// <param name="path">The path of the file to be written to.</param>
    /// <param name="blob">The byte array data to be stored in the file.</param>
    /// <param name="arrivedAt">The timestamp to set as the file's last modification time.</param>
    private async Task StoreBlob(string path, byte[] blob, long arrivedAt) {
      await Task.Run(async () => {
        if (!Directory.Exists(Path.GetDirectoryName(path)))
          Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await CascadeUtils.EnsureFileOperation(async () => {
          await CascadeUtils.WriteBinaryFile(path, blob, 64*1024);
        });
        File.SetLastWriteTimeUtc(path, CascadeUtils.fromUnixMilliseconds(arrivedAt));
      });
    }
  }
}

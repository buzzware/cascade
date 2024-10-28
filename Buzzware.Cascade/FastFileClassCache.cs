using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {

  /// <summary>
  /// Provides a file based IModelClassCache which stores serialized model classes and their collections 
  /// in directory-based file storage.
  /// It uses hashing and memory storage to speed up some reads and writes
  /// </summary>
  public class FastFileClassCache<Model, IdType> : IModelClassCache where Model : class {

    protected const int MAX_STORED_LENGTH = 255;
    private const int EnsureFileOperationMaxAttempts = 10;
    private const int EnsureFileOperationSleepMs = 50;

    /// <summary>
    /// Represents a memory record in the internal cache
    /// </summary>
    public record FastFileCacheRecord {
      public string Path { get; }
      public long TimeMs { get; set; }
      public uint Hash { get; init; }
      public bool HasContent { get; init; }
      public string? Content { get; init; }

      /// <summary>
      /// Initializes a new instance of the FastFileCacheRecord class with specified details.
      /// </summary>
      /// <param name="path">The file path associated with the record.</param>
      /// <param name="timeMs">The timestamp in milliseconds indicating when the record was created.</param>
      /// <param name="hash">The hash of the content for quick comparison.</param>
      /// <param name="hasContent">Flag indicating whether the record contains content.</param>
      /// <param name="content">The actual cached content if available.</param>
      public FastFileCacheRecord(
          string path,
          long timeMs,
          uint hash,
          bool hasContent=false,
          string? content = null
      ) {
        this.Path = path;
        this.TimeMs = timeMs;
        this.Hash = hash;
        this.HasContent = hasContent;
        if (hasContent)
          this.Content = content!;
      }
    }

    private const string ValueKey = "Value";
    private readonly string _fileDir;
    private readonly string _modelsDirectory = typeof(Model).Name+"/Models";
    private readonly string _collectionsDirectory = typeof(Model).Name+"/Collections";
    private readonly CascadeJsonSerialization Serialization;

    private readonly ConcurrentDictionary<string, FastFileCacheRecord> cache = new ConcurrentDictionary<string, FastFileCacheRecord>();
        
    public CascadeDataLayer? Cascade { get; set; }

    /// <summary>
    /// Initializes a FastFileClassCache instance
    /// </summary>
    /// <param name="fileDir">The directory path where model and collection files are stored.</param>
    /// <param name="serialization">Optional Cascade JSON serializer - created internally if not provided</param>
    public FastFileClassCache(string fileDir, CascadeJsonSerialization? serialization = null) {
      _fileDir = fileDir;
      Directory.CreateDirectory(GetModelFilePath());
      Directory.CreateDirectory(GetCollectionFilePath());
      this.Serialization = serialization ?? new CascadeJsonSerialization();
    }

    /// <summary>
    /// Retrieves the storage path of all models for the class, or one with the specified id
    /// </summary>
    /// <param name="id">Optional object id to specify a specific model file.</param>
    /// <returns>the relative path</returns>
    protected string GetModelPath(object? id = null) { 
      return id==null ? _modelsDirectory : Path.Combine(_modelsDirectory, id.ToString() + ".json");
    }

    /// <summary>
    /// Retrieves the storage path for all collections, or one with the given key
    /// </summary>
    /// <param name="key">Optional key</param>
    /// <returns>The relative path</returns>
    protected string GetCollectionPath(string? key = null) {
      return key==null ? _collectionsDirectory : Path.Combine(_collectionsDirectory, key.ToString() + ".json");
    }

    /// <summary>
    /// Transforms a given relative path into a full file path using the base directory.
    /// </summary>
    /// <param name="path">Relative path to be combined with the base directory.</param>
    /// <returns>The complete file path.</returns>
    protected string ToFilePath(string path) {
      return Path.Combine(_fileDir, path);
    }

    /// <summary>
    /// Retrieves the complete file path for a model, optionally including the specified ID.
    /// </summary>
    /// <param name="id">Optional object ID to specify a specific model file.</param>
    /// <returns>The full file path for a model file.</returns>
    public string GetModelFilePath(object? id = null) {
      return ToFilePath(GetModelPath(id)); 
    }
        
    /// <summary>
    /// Retrieves the complete file path for a collection, optionally including the specified key.
    /// </summary>
    /// <param name="key">Optional key to specify a specific collection file.</param>
    /// <returns>The full file path for a collection file.</returns>
    protected string GetCollectionFilePath(string? key = null) {
      return ToFilePath(GetCollectionPath(key));
    }

    /// <summary>
    /// Computes a hash from the given string using FNVHash algorithm.
    /// </summary>
    /// <param name="s">Input string for which hash is to be computed.</param>
    /// <returns>A 32-bit unsigned integer representing the computed hash.</returns>
    protected uint HashAnyString(string? s) {
      var input = s==String.Empty ? "[[[empty]]]" : s;
      var result = (uint)FNVHash.GetHash(input, 32);
      return result;
    }

    /// <summary>
    /// Stores the given string in cache and writes it to a file while ensuring
    /// consistency with file operations in case of an update.
    /// </summary>
    /// <param name="path">Relative path of where to store the content.</param>
    /// <param name="timeMs">Timestamp indicating when the content was received.</param>
    /// <param name="content">The content string to store.</param>
    protected void StoreString(string path, long timeMs, string content) {
      var existing = cache.TryGetValue(path, out var cacheRecord);
      var contentChanged = false;
      var timeChanged = false;
      var hash = HashAnyString(content);
      var storeContent = content.Length < MAX_STORED_LENGTH;
      
      if (existing) {
        contentChanged = hash != cacheRecord!.Hash;
        timeChanged = timeMs > (cacheRecord!.TimeMs);
      } else {
        contentChanged = true;
        timeChanged = true;
      }

      if (contentChanged || timeChanged)
        cache[path] = new FastFileCacheRecord(path, timeMs, hash, storeContent, content);

      var filePath = ToFilePath(path);
      CascadeUtils.EnsureFileOperationSync(() => {
        if (contentChanged)
          CascadeUtils.WriteStringToFile(filePath, content);
        if (timeChanged)
          File.SetLastWriteTimeUtc(filePath, CascadeUtils.fromUnixMilliseconds(timeMs));
      }, EnsureFileOperationMaxAttempts, EnsureFileOperationSleepMs);
    }

    /// <summary>
    /// Asynchronously serializes an object to a specified path with a given timestamp.
    /// </summary>
    /// <param name="aPath">The destination path for storing the serialized object.</param>
    /// <param name="aObject">The object to be serialized.</param>
    /// <param name="timeMs">The timestamp to associate with the serialized content.</param>
    protected async Task SerializeToPathAsync(string aPath, object aObject, long timeMs) {
      var wrapper = new Dictionary<string, object?> { { ValueKey, aObject } };
      var content = Serialization.Serialize(wrapper);
      StoreString(aPath, timeMs, content);
    }
        
    /// <summary>
    /// Asynchronously deserializes content from a specified path into an object of type T.
    /// </summary>
    /// <typeparam name="T">The model type into which the content should be deserialized.</typeparam>
    /// <param name="aPath">Path from where the content is loaded for deserialization.</param>
    /// <returns>An instance of type T, or null if deserialization fails.</returns>
    protected async Task<T?> DeserializeFromPathAsync<T>(string aPath) {
      var content = FetchString(aPath);
      return DeserializeCacheString<T>(content);
    }

    /// <summary>
    /// Deserializes a cached string into an object of type T.
    /// </summary>
    /// <typeparam name="T">The model type into which the string is deserialized.</typeparam>
    /// <param name="content">The string content to be deserialized.</param>
    /// <returns>An instance of type T restored from the content, or default if content is null or empty.</returns>
    public T? DeserializeCacheString<T>(string? content) {
      if (String.IsNullOrWhiteSpace(content))
        return default(T);

      var wrapper = Serialization.DeserializeType<IDictionary<string, object?>>(content)!;
      var value = Serialization.DeserializeType<T>((JsonElement)wrapper[ValueKey]);
      return value;
    }

    /// <summary>
    /// Retrieves a complete FastFileCacheRecord, loading content from the file if necessary, 
    /// and keeps the cache updated.
    /// </summary>
    /// <param name="path">The path for which the cache record is to be fetched.</param>
    /// <returns>A FastFileCacheRecord including content if loaded or already cached, or null if not found.</returns>
    public FastFileCacheRecord? FetchFullRecord(string path) {
      var existing = cache.TryGetValue(path, out var cacheRecord);
      if (existing && cacheRecord!.HasContent)
        return cacheRecord;

      var filePath = ToFilePath(path);
      var content = CascadeUtils.EnsureFileOperationSync(() => CascadeUtils.LoadFileAsString(filePath), EnsureFileOperationMaxAttempts, EnsureFileOperationSleepMs);

      if (content == null) {  
        if (existing)
          cache.TryRemove(path, out var rec);

        return null;
      }

      FastFileCacheRecord result;
      if (!existing) {
        var storeContent = content.Length < MAX_STORED_LENGTH;
        var hash = HashAnyString(content);
        var timeMs = CascadeUtils.EnsureFileOperationSync(() => CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)), EnsureFileOperationMaxAttempts, EnsureFileOperationSleepMs);
        cacheRecord = new FastFileCacheRecord(path, timeMs, hash, storeContent, content);
        cache[path] = cacheRecord;
      }

      if (cacheRecord!.HasContent)
        result = cacheRecord;
      else
        result = new FastFileCacheRecord(path, cacheRecord!.TimeMs, cacheRecord!.Hash, true, content);

      return result;    
    }
        
    /// <summary>
    /// Fetches the cached string content from a specified path.
    /// </summary>
    /// <param name="path">The path from which the content is retrieved.</param>
    /// <returns>The cached string content or null if not available.</returns>
    public string? FetchString(string path) {
      return FetchFullRecord(path)?.Content;
    }
        
    /// <summary>
    /// Asynchronously fetches an object using a specified RequestOp, handling the operation depending on the verb.
    /// </summary>
    /// <param name="requestOp">The operation request defining the type, verb, and key/id for the fetch operation.</param>
    /// <returns>An OpResponse containing the result or indicating none found.</returns>
    public Task<OpResponse> Fetch(RequestOp requestOp) {
      if (requestOp.Type != typeof(Model))
        throw new Exception("requestOp.Type != typeof(Model)");

      bool exists;
      long arrivedAtMs;

      switch (requestOp.Verb) {
        case RequestVerb.Get:
          if (!CascadeTypeUtils.ValueCompatibleWithType(requestOp.Id, typeof(IdType)))
            throw new Exception($"The type for id ({requestOp.Id.GetType().Name}) must be compatible with the class IdType {typeof(IdType).Name}");

          var id = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), requestOp.Id);
          if (id == null)
            throw new Exception("Unable to get right value for Id");

          string modelPath = GetModelPath(id);
          var crModel = FetchFullRecord(modelPath);

          if (crModel != null) {
            var loaded = DeserializeCacheString<Model>(crModel.Content);
            return Task.FromResult(new OpResponse(
              requestOp,
              Cascade?.NowMs ?? 0,
              exists: true,
              arrivedAtMs: crModel.TimeMs, result: loaded));
          } else {
            return Task.FromResult(OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name));
          }

        case RequestVerb.Query:
        case RequestVerb.GetCollection:
          string collectionPath = GetCollectionPath(requestOp.Key!);
          var crCollection = FetchFullRecord(collectionPath);

          if (crCollection != null) {
            var loaded = DeserializeCacheString<IEnumerable<IdType>>(crCollection.Content);
            return Task.FromResult(new OpResponse(
              requestOp,
              Cascade!.NowMs,
              exists: true,
              arrivedAtMs: crCollection.TimeMs, result: loaded));
          } else {
            return Task.FromResult(OpResponse.None(requestOp, Cascade!.NowMs, this.GetType().Name));
          }

        default:
          throw new NotImplementedException($"Unsupported {requestOp.Verb}");
      }
    }

    /// <summary>
    /// Stores a given model object identified by a specific id with a timestamp.
    /// </summary>
    /// <param name="id">The identifier for the model to be stored.</param>
    /// <param name="model">The model object to be serialized and stored.</param>
    /// <param name="arrivedAt">The timestamp in milliseconds when the model arrived.</param>
    public async Task Store(object id, object model, long arrivedAt) {
      var idTyped = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), id);
      if (idTyped == null)
        throw new Exception("Bad id");

      string modelPath = GetModelPath(idTyped)!;
      await SerializeToPathAsync(modelPath, model, arrivedAt);
    }

    /// <summary>
    /// Stores a collection of ids identified by a specific key with a timestamp.
    /// </summary>
    /// <param name="key">The key that identifies the collection to be stored.</param>
    /// <param name="ids">The collection of identifiers to store.</param>
    /// <param name="arrivedAt">The timestamp in milliseconds when the collection was established.</param>
    public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
      string collectionPath = GetCollectionPath(key);
      await SerializeToPathAsync(collectionPath, ids, arrivedAt);
    }

    /// <summary>
    /// Removes a model identified by a specific id from both the cache and the file system
    /// </summary>
    /// <param name="id">The identifier for the model to remove.</param>
    public async Task Remove(object id) {
      string modelPath = GetModelPath(id);
      cache.TryRemove(modelPath, out var removed);
      CascadeUtils.EnsureFileOperationSync(() => {
        File.Delete(ToFilePath(modelPath));
      });
    }

    /// <summary>
    /// Clears all cached entries, optionally preserving specific entries based on held status 
    /// or last-modified dates which can be older than a defined point-in-time.
    /// </summary>
    /// <param name="exceptHeld">If true, preserves held models and collections.</param>
    /// <param name="olderThan">Optional DateTime specifying the threshold for removal based on last-modified time.</param>
    public async Task ClearAll(bool exceptHeld, DateTime? olderThan = null) {
      if (exceptHeld || olderThan != null) {

        // Handle model files
        foreach (var file in Directory.GetFiles(GetModelFilePath())) {
          if (olderThan != null) {
            var fileTime = File.GetLastWriteTimeUtc(file);
            if (fileTime.IsGreaterOrEqual(olderThan.Value))
              continue;
          }

          var id = Path.GetFileNameWithoutExtension(file);
          if (exceptHeld) {
            if (Cascade!.IsHeld<Model>(id))
              continue;
          }

          Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} id {id}");
          CascadeUtils.EnsureFileOperationSync(() => {
            File.Delete(file);
          });

          var cachePath = CascadeUtils.GetRelativePath(_fileDir, file);
          cache.TryRemove(cachePath, out var removed);
        }

        // Handle collection files
        foreach (var file in Directory.GetFiles(GetCollectionFilePath())) {
          if (olderThan != null) {
            var fileTime = File.GetLastWriteTimeUtc(file);
            if (fileTime.IsGreaterOrEqual(olderThan.Value))
              continue;
          }

          var collectionName = Path.GetFileNameWithoutExtension(file);
          if (exceptHeld) {
            if (Cascade!.IsCollectionHeld<Model>(collectionName))
              continue;
          }

          Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} collection {collectionName}");
          CascadeUtils.EnsureFileOperationSync(() => {
            File.Delete(file);
          });

          var cachePath = CascadeUtils.GetRelativePath(_fileDir, file);
          cache.TryRemove(cachePath, out var removed);
        }
      } else {
        // Delete all files in the models directory
        var modelFilePath = GetModelFilePath();
        var files = Directory.GetFiles(modelFilePath);
        foreach (var file in files) {
          Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} id {Path.GetFileNameWithoutExtension(file)}");
          CascadeUtils.EnsureFileOperationSync(() => {
            File.Delete(file);
          });
        }
        
        // Delete all files in the collections directory
        foreach (var file in Directory.GetFiles(GetCollectionFilePath())) {
          Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} collection {Path.GetFileNameWithoutExtension(file)}");
          CascadeUtils.EnsureFileOperationSync(() => {
            File.Delete(file);
          });
        }
        cache.Clear();
      }
    }
  }
}

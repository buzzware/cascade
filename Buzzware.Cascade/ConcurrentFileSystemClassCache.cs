using System.Collections;
using System.Collections.Concurrent;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {
    
    /// <summary>
    /// A file system-based cache for model instances and collections of ids that wraps its file
    /// reads and writes in retrying operations so concurrent access to the same files does not fail.
    /// Implements IModelClassCache using JSON serialization.
    /// </summary>
    /// <typeparam name="Model">The model class stored by this cache</typeparam>
    /// <typeparam name="IdType">The type of the model id</typeparam>
    public class ConcurrentFileSystemClassCache<Model, IdType> : IModelClassCache
        where Model : class {
        private const string ValueKey = "Value";
        private readonly string _fileDir;
        private readonly string _modelsDirectory = typeof(Model).Name+"/Models";
        private readonly string _collectionsDirectory = typeof(Model).Name+"/Collections";
        private readonly CascadeJsonSerialization Serialization;
        private ConcurrentDictionary<string,bool> writingFlags = new ConcurrentDictionary<string,bool>();

        /// <summary>
        /// Reference to the CascadeDataLayer that uses this cache
        /// </summary>
        public CascadeDataLayer? Cascade { get; set; }

        /// <summary>
        /// ConcurrentFileSystemClassCache Constructor
        /// Creates the model and collection directories and initializes the JSON serialization mechanism.
        /// </summary>
        /// <param name="fileDir">The base directory for storing model and collection files.</param>
        /// <param name="serialization">Optional JSON serialization object; uses default if not specified.</param>
        public ConcurrentFileSystemClassCache(string fileDir, CascadeJsonSerialization? serialization = null) {
            _fileDir = fileDir;
            Directory.CreateDirectory(GetModelFilePath());
            Directory.CreateDirectory(GetCollectionFilePath());
            this.Serialization = serialization ?? new CascadeJsonSerialization();
        }

        /// <summary>
        /// Performs any setup required by the cache. This implementation requires none.
        /// </summary>
        public async Task Setup() {
        }
        
        /// <summary>
        /// Gets the file path for storing a specific model instance, or the models directory when id is null.
        /// </summary>
        /// <param name="id">Optional identifier for which the model path is generated, null if retrieving the directory path.</param>
        /// <returns>The full path for model storage.</returns>
        private string GetModelFilePath(object? id = null) { 
          return id==null ? Path.Combine(_fileDir, _modelsDirectory) : Path.Combine(_fileDir, _modelsDirectory, id.ToString() + ".json");
        }

        /// <summary>
        /// Gets the file path for storing a specific collection, or the collections directory when key is null.
        /// </summary>
        /// <param name="key">Optional collection key for which the path is generated, null if retrieving the directory path.</param>
        /// <returns>The full path for collection storage.</returns>
        private string GetCollectionFilePath(string? key = null) {
          return key==null ? Path.Combine(_fileDir, _collectionsDirectory) : Path.Combine(_fileDir, _collectionsDirectory, key.ToString() + ".json");
        }


        /// <summary>
        /// Serializes an object to the given file path within a retrying file operation,
        /// and sets the file's last write time from the given timestamp.
        /// </summary>
        /// <param name="aPath">The target file path for serialized output.</param>
        /// <param name="aObject">The object to serialize.</param>
        /// <param name="timeMs">The timestamp in milliseconds to set as the file's last write time.</param>
        protected Task SerializeToPathAsync(string aPath, object aObject, long timeMs) {
            return Task.Run(() => {
                return CascadeUtils.EnsureFileOperation(async () => {
                    var wrapper = new Dictionary<string, object?> { { ValueKey, aObject } };
                    var content = Serialization.Serialize(wrapper);
                    if (!Directory.Exists(Path.GetDirectoryName(aPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(aPath)!);

                    using (var stream = new FileStream(aPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
                        using (var writer = new StreamWriter(stream)) {
                            await writer.WriteAsync(content).ConfigureAwait(false);
                            File.SetLastWriteTimeUtc(aPath, CascadeUtils.fromUnixMilliseconds(timeMs));
                        }
                    }
                });
            });
        }

        

        /// <summary>
        /// Deserializes an object from the given file path within a retrying file operation.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="aPath">The path of the file from which to deserialize the object.</param>
        /// <returns>The deserialized object of type T, or default when the file content is missing or empty.</returns>
        protected async Task<T?> DeserializeFromPathAsync<T>(string aPath) {
            return await Task.Run(() => {
                return CascadeUtils.EnsureFileOperationSync(() => {
                    var content = CascadeUtils.LoadFileAsString(aPath);
                    if (String.IsNullOrWhiteSpace(content))
                        return default(T);
                    var wrapper = Serialization.DeserializeType<IDictionary<string, object?>>(content)!;
                    var value = Serialization.DeserializeType<T>((JsonElement)wrapper[ValueKey]);
                    return value;
                });
            });
        }
                    
            
            
            
        //     return await Task.Run(async () => {
        //         var attempts = 0;
        //         do {
        //             try {
        //                 attempts++;
        //             } catch (IOException e) {
        //                 Log.Debug("Failed reading attempt {Attempts} {Path}", attempts, aPath);
        //                 if (attempts >= MAX_WRITE_ATTEMPTS)
        //                     throw;
        //             }
        //         } while (true) ;
        //     });
        // }

        /// <summary>
        /// Fetches a model by id (Get) or a collection of ids by key (Query/GetCollection) from the file system.
        /// Freshness requirements are not checked - any existing file is returned.
        /// </summary>
        /// <param name="requestOp">The operation that specifies what to fetch.</param>
        /// <returns>An OpResponse containing the fetched value, or a none response when the file does not exist.</returns>
        public async Task<OpResponse> Fetch(RequestOp requestOp) {
            if (requestOp.Type != typeof(Model))
                throw new Exception("requestOp.Type != typeof(Model)");
            bool exists;
            long arrivedAtMs;
            switch (requestOp.Verb) {
                case RequestVerb.Get:
                    if (!CascadeTypeUtils.ValueCompatibleWithType(requestOp.Id,typeof(IdType)))
                        throw new Exception($"The type for id ({requestOp.Id.GetType().Name}) must be compatible with the class IdType {typeof(IdType).Name}");
                    var id = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), requestOp.Id);
                    if (id == null)
                        throw new Exception("Unable to get right value for Id");

                    string modelFilePath = GetModelFilePath(id);
                    exists = File.Exists(modelFilePath);
                    arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(modelFilePath)) : -1;
                    if (exists) {
                        var loaded = await DeserializeFromPathAsync<Model>(modelFilePath);
                        return new OpResponse(
                            requestOp,
                            Cascade?.NowMs ?? 0,
                            exists: true,
                            arrivedAtMs: arrivedAtMs, result: loaded);
                    } else {
                        return OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name);
                    }
                case RequestVerb.Query:
                case RequestVerb.GetCollection:
                    string collectionFilePath = GetCollectionFilePath(requestOp.Key!);
                    exists = File.Exists(collectionFilePath);
                    arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(collectionFilePath)) : -1;
                    if (exists) {
                        var loaded = await DeserializeFromPathAsync<IEnumerable<IdType>>(collectionFilePath);
                        return new OpResponse(
                            requestOp,
                            Cascade!.NowMs,
                            exists: true,
                            arrivedAtMs: arrivedAtMs, result: loaded);
                    } else {
                        return OpResponse.None(requestOp, Cascade!.NowMs, this.GetType().Name);
                    }
                default:
                    throw new NotImplementedException($"Unsupported {requestOp.Verb}");
            }
        }


        // public async Task<Model?> Fetch<Model>(object id, int freshnessSeconds = 0) where Model : class {
        //     var response = await Fetch(RequestOp.GetOp<Model>(id, Buzzware.Cascade!.NowMs, freshnessSeconds: freshnessSeconds));
        //     return response.Result as Model;
        // }


        /// <summary>
        /// Stores a model instance to the file system based on its id. File exceptions are logged and swallowed.
        /// </summary>
        /// <param name="id">Identifier of the model instance.</param>
        /// <param name="model">The model object to store.</param>
        /// <param name="arrivedAt">Timestamp of when the model arrived, used for file timestamping.</param>
        public async Task Store(object id, object model, long arrivedAt) {
            var idTyped = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), id);
            if (idTyped == null)
                throw new Exception("Bad id");
            try {
                string modelFilePath = GetModelFilePath(idTyped)!;
                await SerializeToPathAsync(modelFilePath, model, arrivedAt);
            } catch (Exception e) {
                Log.Debug(e.Message);   // sharing violation exception sometimes happens here
            }
        }

        /// <summary>
        /// Stores each of the given models using its cascade id.
        /// </summary>
        /// <param name="results">The model objects to store.</param>
        /// <param name="arrivedAt">Timestamp of when the models arrived, used for file timestamping.</param>
        public async Task StoreAll(IReadOnlyList<object> results, long arrivedAt) {
            foreach (var result in results)
                await Store(CascadeTypeUtils.GetCascadeId(result), result, arrivedAt);
        }
        
        /// <summary>
        /// Stores a collection of model ids to the file system under a specific key.
        /// </summary>
        /// <param name="key">The key under which the collection is stored.</param>
        /// <param name="ids">The collection of identifiers to store.</param>
        /// <param name="arrivedAt">Timestamp of when the collection arrived, used for file timestamping.</param>
        public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
            string collectionFilePath = GetCollectionFilePath(key);
            await SerializeToPathAsync(collectionFilePath, ids, arrivedAt);
        }

        /// <summary>
        /// Removes a model instance's file from the file system based on its identifier, if it exists.
        /// </summary>
        /// <param name="id">The identifier of the model instance to remove.</param>
        public async Task Remove(object id) {
            string modelFilePath = GetModelFilePath(id);
            if (File.Exists(modelFilePath)) {
                File.Delete(modelFilePath);
            }
        }
        
        // public async Task ClearAll(bool exceptHeld) {
        //     // Delete all files in the models directory
        //     foreach (var file in Directory.GetFiles(GetModelFilePath())) {
        //         File.Delete(file);
        //     }
        //
        //     // Delete all files in the collections directory
        //     foreach (var file in Directory.GetFiles(GetCollectionFilePath())) {
        //         File.Delete(file);
        //     }
        // }
        
        /// <summary>
        /// Clears all stored models and collections from the file system, optionally preserving
        /// held items and files modified on or after a given time.
        /// </summary>
        /// <param name="exceptHeld">If true, retains models and collections held by the Cascade layer.</param>
        /// <param name="olderThan">Optional DateTime; only files last modified before this time are deleted.</param>
        public async Task ClearAll(bool exceptHeld, DateTime? olderThan = null) {
            if (exceptHeld || olderThan!=null) {
                // models
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
                    Log.Debug($"ConcurrentFileSystemClassCache Clear {typeof(Model).FullName} id {id}");
                    File.Delete(file);
                }

                // collections
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
                    Log.Debug($"ConcurrentFileSystemClassCache Clear {typeof(Model).FullName} collection {collectionName}");
                    File.Delete(file);
                }
            } else {
                // Delete all files in the models directory
                foreach (var file in Directory.GetFiles(GetModelFilePath())) {
                    Log.Debug($"ConcurrentFileSystemClassCache Clear {typeof(Model).FullName} id {Path.GetFileNameWithoutExtension(file)}");
                    File.Delete(file);
                }
                
                // Delete all files in the collections directory
                foreach (var file in Directory.GetFiles(GetCollectionFilePath())) {
                    Log.Debug($"ConcurrentFileSystemClassCache Clear {typeof(Model).FullName} collection {Path.GetFileNameWithoutExtension(file)}");
                    File.Delete(file);
                }
            }
        }
    }
}

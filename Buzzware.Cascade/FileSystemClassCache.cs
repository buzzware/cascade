using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {

    /// <summary>
    /// A simple file system-based cache for storing and retrieving instances of a model class with specified ID type.
    /// Implements IModelClassCache to fetch, store, and remove model instances and collections using JSON serialization.
    /// </summary>
    public class FileSystemClassCache<Model, IdType> : IModelClassCache
        where Model : class {
        
        private const string ValueKey = "Value";
        private readonly string _fileDir;
        private readonly string _modelsDirectory = typeof(Model).Name+"/Models";
        private readonly string _collectionsDirectory = typeof(Model).Name+"/Collections";
        private readonly CascadeJsonSerialization Serialization;

        /// <summary>
        /// Reference to cascade data layer 
        /// </summary>
        public CascadeDataLayer? Cascade { get; set; }

        /// <summary>
        /// FileSystemClassCache Constructor
        /// Initializes directories for models and collections, and initializes the JSON serialization mechanism.
        /// </summary>
        /// <param name="fileDir">The base directory for storing model and collection files.</param>
        /// <param name="serialization">Optional JSON serialization object; uses default if not specified.</param>
        public FileSystemClassCache(string fileDir, CascadeJsonSerialization? serialization = null) {
            _fileDir = fileDir;
            Directory.CreateDirectory(GetModelFilePath());
            Directory.CreateDirectory(GetCollectionFilePath());
            this.Serialization = serialization ?? new CascadeJsonSerialization();
        }

        /// <summary>
        /// Gets the file path or folder for storing a specific model class or instance
        /// </summary>
        /// <param name="id">Optional identifier for which the model path is generated, null if retrieving the directory path.</param>
        /// <returns>The full path for model storage.</returns>
        private string GetModelFilePath(object? id = null) { 
          return id==null ? Path.Combine(_fileDir, _modelsDirectory) : Path.Combine(_fileDir, _modelsDirectory, id.ToString() + ".json");
        }

        /// <summary>
        /// Gets the file path for storing a specific collection or record
        /// </summary>
        /// <param name="key">Optional collection key for which the path is generated, null if retrieving the directory path.</param>
        /// <returns>The full path for collection storage.</returns>
        private string GetCollectionFilePath(string? key = null) {
          return key==null ? Path.Combine(_fileDir, _collectionsDirectory) : Path.Combine(_fileDir, _collectionsDirectory, key.ToString() + ".json");
        }

        /// <summary>
        /// Serializes an object to a specified file path asynchronously and sets the file's last write time.
        /// </summary>
        /// <param name="aPath">The target file path for serialized output.</param>
        /// <param name="aObject">The object to serialize.</param>
        /// <param name="timeMs">The timestamp in milliseconds to set as the file's last write time.</param>
        protected async Task SerializeToPathAsync(string aPath, object aObject, long timeMs) {
            await Task.Run(async () => {
                // Create a wrapper dictionary to embed object values
                var wrapper = new Dictionary<string, object?> { { ValueKey, aObject } };
                var content = Serialization.Serialize(wrapper);

                // Ensure directory exists before writing
                if (!Directory.Exists(Path.GetDirectoryName(aPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(aPath)!);

                // Open a file stream and write serialized content
                using (var stream = new FileStream(aPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    using (var writer = new StreamWriter(stream)) {
                        await writer.WriteAsync(content).ConfigureAwait(false);
                    }

                // Set the file's last write time using the provided timestamp
                File.SetLastWriteTimeUtc(aPath, CascadeUtils.fromUnixMilliseconds(timeMs));
            });
        }

        /// <summary>
        /// Deserializes and returns an object from the given file path.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="aPath">The path of the file from which to deserialize the object.</param>
        /// <returns>The deserialized object of type T, or null if deserialization fails.</returns>
        protected async Task<T?> DeserializeFromPathAsync<T>(string aPath) {
            return await Task.Run(() => {
                // Load file content as string
                var content = CascadeUtils.LoadFileAsString(aPath);

                // Deserialize the content to a dictionary
                var wrapper = Serialization.DeserializeType<IDictionary<string, object?>>(content)!;

                // Extract the actual value from the dictionary and deserialize it to the requested type
                var value = Serialization.DeserializeType<T>((JsonElement)wrapper[ValueKey]);
                return value;
            });
        }

        /// <summary>
        /// Fetches a model or collection from the file system based on the specified request operation.
        /// </summary>
        /// <param name="requestOp">The operation that specifies what to fetch.</param>
        /// <returns>Operation response containing the fetched object or a failed status if not found.</returns>
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

                    // Construct file path and check for file existence
                    string modelFilePath = GetModelFilePath(id);
                    exists = File.Exists(modelFilePath);
                    arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(modelFilePath)) : -1;
                    if (
                        exists && 
                        requestOp.FreshnessSeconds>=0 && 
                        (requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-arrivedAtMs) <= requestOp.FreshnessSeconds*1000))
                    ) {
                        // If the file exists and is fresh, deserialize and return it
                        var loaded = await DeserializeFromPathAsync<Model>(modelFilePath);
                        return new OpResponse(
                            requestOp,
                            Cascade?.NowMs ?? 0,
                            connected: true,
                            exists: true,
                            result: loaded,
                            arrivedAtMs: arrivedAtMs
                        );
                    } else {
                        return OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name);
                    }
                case RequestVerb.Query:
                case RequestVerb.GetCollection:
                    string collectionFilePath = GetCollectionFilePath(requestOp.Key!);
                    exists = File.Exists(collectionFilePath);
                    arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(collectionFilePath)) : -1;
                    
                    if (
                        exists && 
                        requestOp.FreshnessSeconds>=0 && 
                        (requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-arrivedAtMs) <= requestOp.FreshnessSeconds*1000))
                    ) {
                        // If the collection file exists and is fresh, deserialize and return it
                        var loaded = await DeserializeFromPathAsync<IEnumerable<IdType>>(collectionFilePath);
                        return new OpResponse(
                            requestOp,
                            Cascade!.NowMs,
                            connected: true,
                            exists: true,
                            result: loaded,
                            arrivedAtMs: arrivedAtMs
                        );
                    } else {
                        return OpResponse.None(requestOp, Cascade!.NowMs, this.GetType().Name);
                    }
                default:
                    throw new NotImplementedException($"Unsupported {requestOp.Verb}");
            }
        }

        /// <summary>
        /// Stores a model instance asynchronously to the file system based on its id.
        /// </summary>
        /// <param name="id">Identifier of the model instance.</param>
        /// <param name="model">The model object to store.</param>
        /// <param name="arrivedAt">Timestamp of when the model arrived, used for file timestamping.</param>
        public async Task Store(object id, object model, long arrivedAt) {
            var idTyped = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), id);
            if (idTyped == null)
                throw new Exception("Bad id");
            try {
                // Get the file path and serialize model to this path
                string modelFilePath = GetModelFilePath(idTyped)!;
                await SerializeToPathAsync(modelFilePath, model, arrivedAt);
            } catch (Exception e) {
                Log.Debug(e.Message);   // sharing violation exception sometimes happens here
            }
        }

        /// <summary>
        /// Stores a collection of model ids to the file system under a specific key.
        /// </summary>
        /// <param name="key">The key under which the collection is stored.</param>
        /// <param name="ids">The collection of identifiers to store.</param>
        /// <param name="arrivedAt">Timestamp of when the collection was stored, used for file timestamping.</param>
        public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
            // Serialize the collection of IDs to file path corresponding to the key
            string collectionFilePath = GetCollectionFilePath(key);
            await SerializeToPathAsync(collectionFilePath, ids, arrivedAt);
        }

        /// <summary>
        /// Removes a model instance from the file system based on its identifier.
        /// </summary>
        /// <param name="id">The identifier of the model instance to remove.</param>
        public async Task Remove(object id) {
            // Get the model file path and delete it if exists
            string modelFilePath = GetModelFilePath(id);
            if (File.Exists(modelFilePath)) {
                File.Delete(modelFilePath);
            }
        }
        
        /// <summary>
        /// Clears all stored models and collections from the file system. Optionally exempts held or recently modified files.
        /// </summary>
        /// <param name="exceptHeld">If true, retains files marked as held in cache.</param>
        /// <param name="olderThan">Optional DateTime to retain files modified after a certain date.</param>
        public async Task ClearAll(bool exceptHeld, DateTime? olderThan = null) {
            if (exceptHeld || olderThan!=null) {
                // Conditional model file deletion based on hold status or modification date
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
                    Log.Debug($"FileSystemClassCache Clear {typeof(Model).FullName} id {id}");
                    File.Delete(file);
                }

                // Conditional collection file deletion based on hold status or modification date
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
                    Log.Debug($"FileSystemClassCache Clear {typeof(Model).FullName} collection {collectionName}");
                    File.Delete(file);
                }
            } else {
                // Delete all files in the models directory
                foreach (var file in Directory.GetFiles(GetModelFilePath())) {
                    Log.Debug($"FileSystemClassCache Clear {typeof(Model).FullName} id {Path.GetFileNameWithoutExtension(file)}");
                    File.Delete(file);
                }
                
                // Delete all files in the collections directory
                foreach (var file in Directory.GetFiles(GetCollectionFilePath())) {
                    Log.Debug($"FileSystemClassCache Clear {typeof(Model).FullName} collection {Path.GetFileNameWithoutExtension(file)}");
                    File.Delete(file);
                }
            }
        }
    }
}

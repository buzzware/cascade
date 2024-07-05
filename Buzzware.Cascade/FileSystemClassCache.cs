using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {


    public class FileSystemClassCache<Model, IdType> : IModelClassCache
        where Model : class {
        private const string ValueKey = "Value";
        private readonly string _fileDir;
        private readonly string _modelsDirectory = typeof(Model).Name+"/Models";
        private readonly string _collectionsDirectory = typeof(Model).Name+"/Collections";
        private readonly CascadeJsonSerialization Serialization;

        public CascadeDataLayer? Cascade { get; set; }

        public FileSystemClassCache(string fileDir, CascadeJsonSerialization? serialization = null) {
            _fileDir = fileDir;
            Directory.CreateDirectory(GetModelFilePath());
            Directory.CreateDirectory(GetCollectionFilePath());
            this.Serialization = serialization ?? new CascadeJsonSerialization();
        }

        private string GetModelFilePath(object? id = null) { 
          return id==null ? Path.Combine(_fileDir, _modelsDirectory) : Path.Combine(_fileDir, _modelsDirectory, id.ToString() + ".json");
        }

        private string GetCollectionFilePath(string? key = null) {
          return key==null ? Path.Combine(_fileDir, _collectionsDirectory) : Path.Combine(_fileDir, _collectionsDirectory, key.ToString() + ".json");
        }

        protected async Task SerializeToPathAsync(string aPath, object aObject, long timeMs) {
            await Task.Run(async () => {
                var wrapper = new Dictionary<string, object?> { { ValueKey, aObject } };
                var content = Serialization.Serialize(wrapper);
                if (!Directory.Exists(Path.GetDirectoryName(aPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(aPath)!);

                using (var stream = new FileStream(aPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    using (var writer = new StreamWriter(stream)) {
                        await writer.WriteAsync(content).ConfigureAwait(false);
                    }

                File.SetLastWriteTimeUtc(aPath, CascadeUtils.fromUnixMilliseconds(timeMs));
            });
        }

        protected async Task<T?> DeserializeFromPathAsync<T>(string aPath) {
            return await Task.Run(() => {
                var content = CascadeUtils.LoadFileAsString(aPath);
                var wrapper = Serialization.DeserializeType<IDictionary<string, object>>(content)!;
                var value = Serialization.DeserializeType<T>((JsonElement)wrapper[ValueKey]);
                return value;
            });
        }

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
                    if (
                        exists && 
                        requestOp.FreshnessSeconds>=0 && 
                        (requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-arrivedAtMs) <= requestOp.FreshnessSeconds*1000))
                    ) {
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


        // public async Task<Model?> Fetch<Model>(object id, int freshnessSeconds = 0) where Model : class {
        //     var response = await Fetch(RequestOp.GetOp<Model>(id, Buzzware.Cascade!.NowMs, freshnessSeconds: freshnessSeconds));
        //     return response.Result as Model;
        // }
        
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

        public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
            string collectionFilePath = GetCollectionFilePath(key);
            await SerializeToPathAsync(collectionFilePath, ids, arrivedAt);
        }

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
                    Log.Debug($"FileSystemClassCache Clear {typeof(Model).FullName} id {id}");
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

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

namespace Cascade {


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
                var wrapper = ImmutableDictionary<string, object>.Empty.Add(ValueKey, aObject);
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
            return await Task.Run(async () => {
                var content = await CascadeUtils.LoadFileAsString(aPath).ConfigureAwait(false);
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
                    }
                    else {
                        return OpResponse.None(requestOp, Cascade!.NowMs, this.GetType().Name);
                    }
                default:
                    throw new NotImplementedException($"Unsupported {requestOp.Verb}");
            }
        }


        // public async Task<Model?> Fetch<Model>(object id, int freshnessSeconds = 0) where Model : class {
        //     var response = await Fetch(RequestOp.GetOp<Model>(id, Cascade!.NowMs, freshnessSeconds: freshnessSeconds));
        //     return response.Result as Model;
        // }
        
        public async Task Store(object id, object model, long arrivedAt) {
            var idTyped = (IdType?)CascadeTypeUtils.ConvertTo(typeof(IdType), id);
            if (idTyped == null)
                throw new Exception("Bad id");
            
            string modelFilePath = GetModelFilePath(idTyped)!;
            await SerializeToPathAsync(modelFilePath, model, arrivedAt);
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
        
        public async Task ClearAll(bool exceptHeld = true) {
            if (exceptHeld) {
                // models
                foreach (var file in Directory.GetFiles(GetModelFilePath())) {
                    if (!Cascade.IsHeld<Model>(Path.GetFileNameWithoutExtension(file)))
                        File.Delete(file);
                }

                // collections
                foreach (var file in Directory.GetFiles(GetCollectionFilePath())) {
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                    if (!Cascade.IsCollectionHeld<Model>(fileNameWithoutExtension))
                        File.Delete(file);
                }
                
                // var heldModelIds = (await Cascade.ListHeldIds<Model>()).ToArray();
                // var idType = CascadeTypeUtils.GetCascadeIdType(typeof(Model));
                // var idsToRemove = Directory.GetFiles(GetModelFilePath()).Where(id_string => {
                //         //var id = CascadeTypeUtils.GetCascadeId(kv.Value.Item1);
                //         //var id = kv.Key;
                //         var id = CascadeTypeUtils.ConvertTo(idType, id_string)!;
                //         var contains = heldModelIds.Contains(id);
                //         return !contains;
                //     })
                //     .ToArray();
                // foreach (var id in idsToRemove)
                //     File.Delete(id);
                //
                // // collections
                // var heldCollectionNames = (await Cascade.ListHeldCollections(typeof(Model))).ToArray();
                // var namesToRemove = collections.Where(kv => !heldCollectionNames.Contains(kv.Key))
                //     .Select(kv => kv.Key)
                //     .ToArray();
                // foreach (var name in namesToRemove)
                //     collections.TryRemove(name, out var v);
            } else {
                // Delete all files in the models directory
                foreach (var file in Directory.GetFiles(GetModelFilePath())) {
                    File.Delete(file);
                }
                
                // Delete all files in the collections directory
                foreach (var file in Directory.GetFiles(GetCollectionFilePath())) {
                    File.Delete(file);
                }
            }
        }
    }
}

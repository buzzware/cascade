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
	
	public class FastFileClassCache<Model, IdType> : IModelClassCache where Model : class {

		protected const int MAX_STORED_LENGTH = 255;
		private const int EnsureFileOperationMaxAttempts = 10;
		private const int EnsureFileOperationSleepMs = 50;
		
		public record FastFileCacheRecord {
			public string Path { get; }
			public long TimeMs { get; set; }
			public uint Hash { get; init; }
			public bool HasContent { get; init; }
			public string? Content { get; init; }
			
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

		// private readonly ConcurrentDictionary<string, ReaderWriterLockSlim> locks = new ConcurrentDictionary<string, ReaderWriterLockSlim>();
		private readonly ConcurrentDictionary<string, FastFileCacheRecord> cache = new ConcurrentDictionary<string, FastFileCacheRecord>();
        
		public CascadeDataLayer? Cascade { get; set; }
        
		public FastFileClassCache(string fileDir, CascadeJsonSerialization? serialization = null) {
			_fileDir = fileDir;
			Directory.CreateDirectory(GetModelFilePath());
			Directory.CreateDirectory(GetCollectionFilePath());
			this.Serialization = serialization ?? new CascadeJsonSerialization();
		}

		protected string GetModelPath(object? id = null) { 
			return id==null ? _modelsDirectory : Path.Combine(_modelsDirectory, id.ToString() + ".json");
		}

		protected string GetCollectionPath(string? key = null) {
			return key==null ? _collectionsDirectory : Path.Combine(_collectionsDirectory, key.ToString() + ".json");
		}

		protected string ToFilePath(string path) {
			return Path.Combine(_fileDir, path);
		}

		public string GetModelFilePath(object? id = null) {
			return ToFilePath(GetModelPath(id)); 
		}
        
		protected string GetCollectionFilePath(string? key = null) {
			return ToFilePath(GetCollectionPath(key));
		}

		protected uint HashAnyString(string? s) {
			var input = s==String.Empty ? "[[[empty]]]" : s;
			var result = (uint)FNVHash.GetHash(input, 32);
			return result;
		}
        
		// protected LockHandle GetReadLock(string name) {
		//     var rwLock = locks.GetOrAdd(name, _ => new ReaderWriterLockSlim());
		//     return new LockHandle(rwLock, false);
		// }
		//
		// protected LockHandle GetWriteLock(string name) {
		//     var rwLock = locks.GetOrAdd(name, _ => new ReaderWriterLockSlim());
		//     return new LockHandle(rwLock, true);
		// }
        

		protected void StoreString(string path, long timeMs, string content) {
			var existing = cache.TryGetValue(path,out var cacheRecord);
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
					CascadeUtils.WriteStringToFile(filePath,content);
				if (timeChanged)
					File.SetLastWriteTimeUtc(filePath, CascadeUtils.fromUnixMilliseconds(timeMs));
			}, EnsureFileOperationMaxAttempts, EnsureFileOperationSleepMs);
		}

		protected async Task SerializeToPathAsync(string aPath, object aObject, long timeMs) {
			var wrapper = new Dictionary<string, object?> { { ValueKey, aObject } };
			var content = Serialization.Serialize(wrapper);
            
			StoreString(aPath, timeMs, content);
		}
        
		protected async Task<T?> DeserializeFromPathAsync<T>(string aPath) {
			var content = FetchString(aPath);
			return DeserializeCacheString<T>(content);
		}

		public T? DeserializeCacheString<T>(string? content) {
			if (String.IsNullOrWhiteSpace(content))
				return default(T);
			var wrapper = Serialization.DeserializeType<IDictionary<string, object>>(content)!;
			var value = Serialization.DeserializeType<T>((JsonElement)wrapper[ValueKey]);
			return value;
		}

		// Get a full FastFileCacheRecord (including content). If it has to load from a file will cache with or without content.  
		public FastFileCacheRecord? FetchFullRecord(string path) {
			var existing = cache.TryGetValue(path,out var cacheRecord);
			if (existing && cacheRecord!.HasContent)
				return cacheRecord;

			var filePath = ToFilePath(path);
			var content = CascadeUtils.EnsureFileOperationSync(() => CascadeUtils.LoadFileAsString(filePath),EnsureFileOperationMaxAttempts, EnsureFileOperationSleepMs);
			if (content == null) {  // should be in cache if not in file system
				if (existing)
					cache.TryRemove(path, out var rec);
				return null;
			}
			FastFileCacheRecord result;
			if (!existing) {
				var storeContent = content.Length < MAX_STORED_LENGTH;
				var hash = HashAnyString(content);
				var timeMs = CascadeUtils.EnsureFileOperationSync(() => CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(filePath)),EnsureFileOperationMaxAttempts, EnsureFileOperationSleepMs);
				cacheRecord = new FastFileCacheRecord(path, timeMs, hash, storeContent, content);
				cache[path] = cacheRecord;
			}

			if (cacheRecord!.HasContent)
				result = cacheRecord;
			else
				result = new FastFileCacheRecord(path, cacheRecord!.TimeMs, cacheRecord!.Hash, true, content);
			return result;    
		}
        
		public string? FetchString(string path) {
			return FetchFullRecord(path)?.Content;
		}
        
		public Task<OpResponse> Fetch(RequestOp requestOp) {
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

					string modelPath = GetModelPath(id);
					var crModel = FetchFullRecord(modelPath);
					if (crModel!=null) {
						var loaded = DeserializeCacheString<Model>(crModel.Content);
						return Task.FromResult(new OpResponse(
							requestOp,
							Cascade?.NowMs ?? 0,
							connected: true,
							exists: true,
							result: loaded,
							arrivedAtMs: crModel.TimeMs
						));
					} else {
						return Task.FromResult(OpResponse.None(requestOp, Cascade.NowMs, this.GetType().Name));
					}
				case RequestVerb.Query:
				case RequestVerb.GetCollection:
					string collectionPath = GetCollectionPath(requestOp.Key!);
					var crCollection = FetchFullRecord(collectionPath);
					if (crCollection!=null) {
						var loaded = DeserializeCacheString<IEnumerable<IdType>>(crCollection.Content);
						return Task.FromResult(new OpResponse(
							requestOp,
							Cascade!.NowMs,
							connected: true,
							exists: true,
							result: loaded,
							arrivedAtMs: crCollection.TimeMs
						));
					} else {
						return Task.FromResult(OpResponse.None(requestOp, Cascade!.NowMs, this.GetType().Name));
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
			string modelPath = GetModelPath(idTyped)!;
			await SerializeToPathAsync(modelPath, model, arrivedAt);
		}
        
		public async Task StoreCollection(string key, IEnumerable ids, long arrivedAt) {
			string collectionPath = GetCollectionPath(key);
			await SerializeToPathAsync(collectionPath, ids, arrivedAt);
		}

		public async Task Remove(object id) {
			string modelPath = GetModelPath(id);
			cache.TryRemove(modelPath, out var removed);
			CascadeUtils.EnsureFileOperationSync(() => {
				File.Delete(ToFilePath(modelPath));
			});
		}
        
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
					Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} id {id}");
					CascadeUtils.EnsureFileOperationSync(() => {
						File.Delete(file);
					});
					var cachePath = CascadeUtils.GetRelativePath(_fileDir, file);
					cache.TryRemove(cachePath, out var removed);
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
					Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} collection {collectionName}");
					CascadeUtils.EnsureFileOperationSync(() => {
						File.Delete(file);
					});
					var cachePath = CascadeUtils.GetRelativePath(_fileDir, file);
					cache.TryRemove(cachePath, out var removed);
				}
			} else {
				cache.Clear();
				// Delete all files in the models directory
				foreach (var file in Directory.GetFiles(GetModelFilePath())) {
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
			}
		}
	}
}

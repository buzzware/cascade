using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// </summary>
	public partial class CascadeDataLayer {

		#region Meta
		// The "meta" feature offers key/value persistent storage 

		public string MetaResolvePath(string path) {
			if (path != null && path.Contains(".."))
				throw new ArgumentException("Path cannot contain ..");
			path = path!.TrimStart(new[] { '/', '\\' });
			path = Path.Combine(Config.MetaPath, path);
			return path;
		}
		
		// Sets the key to a value
		public void MetaSet(
			string path,	// forward-slash relative path to a document (the key)
			string value	// a string or null (the value)
		) {
			path = MetaResolvePath(path);
			var folder = Path.GetDirectoryName(path)!;
			CascadeUtils.EnsureFileOperationSync(() => {
				if (!Directory.Exists(folder) && value!=null)
					Directory.CreateDirectory(folder);
				if (value == null) {
					if (File.Exists(path)) {
						Log.Debug($"MetaSet file {path}");
						File.Delete(path);
					}
				} else {
					File.WriteAllText(path, value);	
				}
			});
		}

		// Gets the value of a key
		public string? MetaGet(
			string path
		) {
			path = MetaResolvePath(path);
			return CascadeUtils.EnsureFileOperationSync(() => {
				if (!File.Exists(path))
					return null;
				return File.ReadAllText(path);
			});
		}

		public bool MetaExists(
			string path
		) {
			path = MetaResolvePath(path);
			return File.Exists(path);
		}

		public IEnumerable<string> MetaList(string path, bool recursive = false, bool sort = true) {
			path = MetaResolvePath(path);
			if (!Directory.Exists(path))
				return ImmutableArray<string>.Empty;
			if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
				path += Path.DirectorySeparatorChar;
			
			var items = Directory.GetFiles(path,"*.*",recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			var result = items.Select(p => p.Substring(path.Length)).ToImmutableArray();
			if (sort)
				result = result.Sort();
			return result;
		}
		
		public void MetaClearPath(string path, DateTime? olderThan=null, bool recursive = false) {
			if (String.IsNullOrWhiteSpace(path))
				throw new ArgumentException("path cannot be empty");
			var folderPath = MetaResolvePath(path);
			CascadeUtils.EnsureFileOperationSync(() => {
				if (Directory.Exists(folderPath)) {
					if (olderThan == null && recursive) {
						Log.Debug($"MetaClearPath folder {folderPath}");
						Directory.Delete(folderPath, true);
					}
					else {
						var files = Directory.GetFiles(folderPath, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
						foreach (var file in files) {
							var fileTime = File.GetLastWriteTimeUtc(file);
							if (olderThan == null) {
								Log.Debug($"MetaClearPath file {file}");
								File.Delete(file);
							}
							else {
								if (fileTime <= olderThan.Value) {
									Log.Debug($"MetaClearPath file {file}");
									File.Delete(file);
								}
							}
						}
					}
				}
			});
		}

		public void MetaClearAll() {
			var path = MetaResolvePath("");
			CascadeUtils.EnsureFileOperationSync(() => {
				if (Directory.Exists(path)) {
					Log.Debug($"MetaClearAll folder {path}");
					Directory.Delete(path, true);
				}
			});
		}

		#endregion
		
		#region Holding

		public const string BLOB_PATH_ALT_SEPARATOR = "_%_";
		
		public static string EncodeBlobPath(string path) {
			return path.Replace("/", BLOB_PATH_ALT_SEPARATOR);
		}

		public static string DecodeBlobPath(string path) {
			return path.Replace(BLOB_PATH_ALT_SEPARATOR, "/");
		}

		public static string HoldModelPath(string typeFolder, object? id = null) {
			if (id == null) {
				return Path.Combine(CascadeConstants.HOLD, "Model", typeFolder);
			} else {
				var idString = id.ToString();
				if (typeFolder == CascadeConstants.BLOB)
					idString = EncodeBlobPath(idString);
				return Path.Combine(CascadeConstants.HOLD, "Model", typeFolder, idString);
			}
		}

		private string HoldModelPath(Type type) {
			var folderName = CascadeTypeUtils.IsBlobType(type) ? CascadeConstants.BLOB : type.FullName!;
			return HoldModelPath(folderName); 
		}
		
		private string HoldModelPath(Type type,object id) {
			if (id is null or "" or 0)
				throw new ArgumentException("Id Cannot be null or empty string or 0");
			var folderName = CascadeTypeUtils.IsBlobType(type) ? CascadeConstants.BLOB : type.FullName!;
			return HoldModelPath(folderName,id);
		}
		
		public void Hold<Model>(object id) {
			Hold(typeof(Model), id);
		}

		public void Hold(object model) {
			Hold(model.GetType(), CascadeTypeUtils.GetCascadeId(model));
		}
		
		public void Hold(Type modelType, object id) {
			Log.Debug($"CascadeDataLayer Hold {modelType.FullName} id {id}");
			var path = HoldModelPath(modelType,id);
			MetaSet(path, String.Empty);
		}

		public bool IsHeld<Model>(object id) {
			return MetaExists(HoldModelPath(typeof(Model), id));
		}

		public void Unhold<Model>(object id) {
			Log.Debug($"CascadeDataLayer Unhold {nameof(Model)} id {id}");
			MetaSet(HoldModelPath(typeof(Model),id),null);
		}
		
		public void HoldBlob(string path) {
			Log.Debug($"CascadeDataLayer HoldBlob {CascadeConstants.BLOB} path {path}");
			var metaPath = HoldModelPath(CascadeConstants.BLOB,path);
			MetaSet(metaPath, String.Empty);
		}

		public void UnholdBlob(string path) {
			Log.Debug($"CascadeDataLayer UnholdBlob {CascadeConstants.BLOB} path {path}");
			MetaSet(HoldModelPath(CascadeConstants.BLOB,path),null);
		}
		
		public bool IsHeldBlob(string path) {
			return MetaExists(HoldModelPath(CascadeConstants.BLOB, path));
		}
		
		public IEnumerable<object> ListHeldIds<Model>() {
			return ListHeldIds(typeof(Model)); // var modelPath = HoldModelPath<Model>();
		}
		
		public IEnumerable<object> ListHeldIds(Type type) {
			if (CascadeTypeUtils.IsBlobType(type))
				return ListHeldBlobPaths();
			
			var path = HoldModelPath(type);
			var idType = CascadeTypeUtils.GetCascadeIdType(type);

			var items = MetaList(path);
			
			return items
				.Select<string, object?>(name =>
				{
					if (idType == typeof(string))
						return name;
					else
						return CascadeTypeUtils.ConvertTo(idType, name)!;
				})
				.ToImmutableArray()
				.Sort();
		}

		public IEnumerable<object> ListHeldBlobPaths() {
			var path = HoldModelPath(CascadeConstants.BLOB);
			var items = MetaList(path);
			return items
				.Select<string, object?>(name => DecodeBlobPath(name))
				.ToImmutableArray()
				.Sort();
		}
		
		private string HoldCollectionPath(Type modelType) {
			return Path.Combine(CascadeConstants.HOLD, "Collection", modelType.FullName);
		}
		
		private string HoldCollectionPath(Type modelType,string key) {
			if (key is null or "")
				throw new ArgumentException("name Cannot be null or empty string");
			return Path.Combine(CascadeConstants.HOLD, "Collection", modelType.FullName, key);
		}
		
		public void HoldCollection<Model>(string name) {
			HoldCollection(typeof(Model),name);
		}

		public void HoldCollection(Type modelType, string name) {
			Log.Debug($"CascadeDataLayer HoldCollection {modelType.FullName} collection {name}");
			var path = HoldCollectionPath(modelType,name);
			// if (MetaExists(path))
			// 	return;
			MetaSet(path, String.Empty);
		}
		
		public void UnholdCollection<Model>(string name) {
			Log.Debug($"CascadeDataLayer UnholdCollection {nameof(Model)} collection {name}");
			var path = HoldCollectionPath(typeof(Model),name);
			if (MetaExists(path))
				MetaSet(path, null);
		}
		
		public bool IsCollectionHeld<Model>(string name) {
			return MetaExists(HoldCollectionPath(typeof(Model),name));
		}
		
		public IEnumerable<object> ListHeldCollections(Type modelType) {
			return MetaList(HoldCollectionPath(modelType));
		}

		public void UnholdAll(Type modelType, DateTime? olderThan=null) {
			if (!CascadeTypeUtils.IsBlobType(modelType))
				MetaClearPath(HoldCollectionPath(modelType),olderThan);
			MetaClearPath(HoldModelPath(modelType),olderThan);
		}
		
		public void UnholdAll(DateTime? olderThan=null) {
			MetaClearPath(CascadeConstants.HOLD, olderThan, true);
		}
		
		public void UnholdAll() {
			MetaClearAll();
		}
		
		#endregion

	}
}

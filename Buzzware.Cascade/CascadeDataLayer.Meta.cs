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
	/// Methods to manipulate and query metadata and holding
	/// </summary>
	public partial class CascadeDataLayer {

		#region Meta
		// The "meta" feature offers key/value persistent storage 

		/// <summary>
		/// Resolves and returns the absolute path for the given relative meta path.
		/// Ensures that the path does not contain any directory traversal sequences ('..').
		/// </summary>
		/// <param name="path">The relative path that needs resolution.</param>
		/// <returns>The resolved absolute path.</returns>
		public string MetaResolvePath(string path) {
			if (path != null && path.Contains(".."))
				throw new ArgumentException("Path cannot contain ..");
			path = path!.TrimStart(new[] { '/', '\\' });
			path = Path.Combine(Config.MetaPath, path);
			return path;
		}
		
		/// <summary>
		/// Sets a value in the metadata store for the specified key path. 
		/// If the value is null, the key is removed from the store.
		/// </summary>
		/// <param name="path">The relative path (key) in the metadata store.</param>
		/// <param name="value">The value to associate with the key. Null to remove the key.</param>
		public void MetaSet(
			string path,	// forward-slash relative path to a document (the key)
			string? value	// a string or null (the value)
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

		/// <summary>
		/// Retrieves the value associated with the specified key path in the metadata store.
		/// Returns null if the key does not exist.
		/// </summary>
		/// <param name="path">The relative path (key) to retrieve the value for.</param>
		/// <returns>The value associated with the key, or null if the key does not exist.</returns>
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

		/// <summary>
		/// Checks whether a key exists in the metadata store for the given path.
		/// </summary>
		/// <param name="path">The relative key path to check for existence.</param>
		/// <returns>True if the key exists, false otherwise.</returns>
		public bool MetaExists(
			string path
		) {
			path = MetaResolvePath(path);
			return File.Exists(path);
		}

		/// <summary>
		/// Lists all keys under the specified path in the metadata store.
		/// Options to list recursively and sort the results.
		/// </summary>
		/// <param name="path">The relative path to list keys from.</param>
		/// <param name="recursive">Whether to list recursively through all subdirectories.</param>
		/// <param name="sort">Whether to sort the resulting list of keys.</param>
		/// <returns>An enumerable of all keys under the specified path.</returns>
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
		
		/// <summary>
		/// Clears files from the specified metadata path based on optional filter criteria.
		/// Can remove files older than a specified date and can operate recursively.
		/// </summary>
		/// <param name="path">The path to clear the files or folders.</param>
		/// <param name="olderThan">The optional DateTime to check if files are older to be deleted.</param>
		/// <param name="recursive">Whether to delete files recursively.</param>
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

		/// <summary>
		/// Clears all metadata files and folders in the configured root meta path.
		/// </summary>
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

		// Separator used in encoded blob paths
		public const string BLOB_PATH_ALT_SEPARATOR = "_%_";
		
		/// <summary>
		/// Encodes a blob path by replacing slashes with the alternative separator.
		/// </summary>
		/// <param name="path">The blob path to encode.</param>
		/// <returns>The encoded blob path.</returns>
		public static string EncodeBlobPath(string path) {
			return path.Replace("/", BLOB_PATH_ALT_SEPARATOR);
		}

		/// <summary>
		/// Decodes an encoded blob path by reverting the alternative separator back to slashes.
		/// </summary>
		/// <param name="path">The encoded blob path to decode.</param>
		/// <returns>The decoded blob path.</returns>
		public static string DecodeBlobPath(string path) {
			return path.Replace(BLOB_PATH_ALT_SEPARATOR, "/");
		}

		/// <summary>
		/// Constructs and returns a path for holding a model of the specified type in the hold directory.
		/// </summary>
		/// <param name="typeFolder">The folder type associated with the model.</param>
		/// <param name="id">Optional ID of the model, used to construct sub-paths.</param>
		/// <returns>The generated hold model path.</returns>
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

		/// <summary>
		/// Constructs and returns a path for a specific model type to be held in the hold directory.
		/// </summary>
		/// <param name="type">The Type of the model to hold.</param>
		/// <returns>The generated hold model path.</returns>
		private string HoldModelPath(Type type) {
			var folderName = CascadeTypeUtils.IsBlobType(type) ? CascadeConstants.BLOB : type.FullName!;
			return HoldModelPath(folderName); 
		}
		
		/// <summary>
		/// Constructs and returns the hold model path for a given type and ID.
		/// Ensures the ID is valid before generating the path.
		/// </summary>
		/// <param name="type">The Type of the model to hold.</param>
		/// <param name="id">The identifier for the model. Must not be null, an empty string, or zero.</param>
		/// <returns>The hold model path.</returns>
		/// <exception cref="ArgumentException">Thrown if ID is not valid.</exception>
		private string HoldModelPath(Type type,object id) {
			if (id is null or "" or 0)
				throw new ArgumentException("Id Cannot be null or empty string or 0");
			var folderName = CascadeTypeUtils.IsBlobType(type) ? CascadeConstants.BLOB : type.FullName!;
			return HoldModelPath(folderName,id);
		}
		
		/// <summary>
		/// Holds a model of the specified type and identifier by creating a corresponding metadata entry.
		/// </summary>
		/// <typeparam name="Model">The model type to hold.</typeparam>
		/// <param name="id">The identifier of the model to hold.</param>
		public void Hold<Model>(object id) {
			Hold(typeof(Model), id);
		}

		/// <summary>
		/// Holds an object model by its type and identifier value.
		/// </summary>
		/// <param name="model">The model instance to hold.</param>
		public void Hold(object model) {
			Hold(model.GetType(), CascadeTypeUtils.GetCascadeId(model));
		}
		
		/// <summary>
		/// Holds a model of a specified type and identifier by creating a metadata entry.
		/// </summary>
		/// <param name="modelType">The Type of the model to hold.</param>
		/// <param name="id">The identifier of the model to hold.</param>
		public void Hold(Type modelType, object id) {
			Log.Debug($"CascadeDataLayer Hold {modelType.FullName} id {id}");
			var path = HoldModelPath(modelType,id);
			MetaSet(path, String.Empty);
		}

		/// <summary>
		/// Checks if a model of the specified type and identifier is currently held.
		/// </summary>
		/// <typeparam name="Model">The model type to check.</typeparam>
		/// <param name="id">The identifier of the model to check.</param>
		/// <returns>True if the model is held, otherwise False.</returns>
		public bool IsHeld<Model>(object id) {
			return MetaExists(HoldModelPath(typeof(Model), id));
		}

		/// <summary>
		/// Unholds a model of the specified type and identifier by removing the metadata entry.
		/// </summary>
		/// <typeparam name="Model">The model type to unhold.</typeparam>
		/// <param name="id">The identifier of the model to unhold.</param>
		public void Unhold<Model>(object id) {
			Log.Debug($"CascadeDataLayer Unhold {nameof(Model)} id {id}");
			MetaSet(HoldModelPath(typeof(Model),id),null);
		}
		
		/// <summary>
		/// Holds a blob path by creating a corresponding metadata entry.
		/// </summary>
		/// <param name="path">The blob path to hold.</param>
		public void HoldBlob(string path) {
			Log.Debug($"CascadeDataLayer HoldBlob {CascadeConstants.BLOB} path {path}");
			var metaPath = HoldModelPath(CascadeConstants.BLOB,path);
			MetaSet(metaPath, String.Empty);
		}

		/// <summary>
		/// Unholds a blob path by removing the corresponding metadata entry.
		/// </summary>
		/// <param name="path">The blob path to unhold.</param>
		public void UnholdBlob(string path) {
			Log.Debug($"CascadeDataLayer UnholdBlob {CascadeConstants.BLOB} path {path}");
			MetaSet(HoldModelPath(CascadeConstants.BLOB,path),null);
		}
		
		/// <summary>
		/// Checks if a blob path is currently held in the metadata.
		/// </summary>
		/// <param name="path">The blob path to check.</param>
		/// <returns>True if the blob path is held, otherwise False.</returns>
		public bool IsHeldBlob(string path) {
			return MetaExists(HoldModelPath(CascadeConstants.BLOB, path));
		}
		
		/// <summary>
		/// Lists all identifiers of a specified model type that are currently held.
		/// </summary>
		/// <typeparam name="Model">The model type whose held identifiers are to be listed.</typeparam>
		/// <returns>An enumerable of all held identifiers for the specified model type.</returns>
		public IEnumerable<object> ListHeldIds<Model>() {
			return ListHeldIds(typeof(Model)); // var modelPath = HoldModelPath<Model>();
		}
		
		/// <summary>
		/// Lists all identifiers of the specified type currently held in metadata.
		/// Differentiates between blob and non-blob types.
		/// </summary>
		/// <param name="type">The Type to list held identifiers for.</param>
		/// <returns>An enumerable of held identifiers.</returns>
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

		/// <summary>
		/// Lists all blob paths currently held in metadata.
		/// </summary>
		/// <returns>An enumerable of held blob paths.</returns>
		public IEnumerable<object> ListHeldBlobPaths() {
			var path = HoldModelPath(CascadeConstants.BLOB);
			var items = MetaList(path);
			return items
				.Select<string, object?>(name => DecodeBlobPath(name))
				.ToImmutableArray()
				.Sort();
		}

		/// <summary>
		/// Constructs and returns a path for a collection of models of a specified type in the hold directory.
		/// </summary>
		/// <param name="modelType">The Type of the model collection to hold.</param>
		/// <returns>The hold collection path.</returns>
		private string HoldCollectionPath(Type modelType) {
			return Path.Combine(CascadeConstants.HOLD, "Collection", modelType.FullName);
		}

		/// <summary>
		/// Constructs and returns a path for a specific collection in the hold directory.
		/// Ensures the collection name is valid before generating the path.
		/// </summary>
		/// <param name="modelType">The Type of the model collection to hold.</param>
		/// <param name="key">The collection key name. Must not be null or empty.</param>
		/// <returns>The hold collection path.</returns>
		/// <exception cref="ArgumentException">Thrown if the key is invalid.</exception>
		private string HoldCollectionPath(Type modelType,string key) {
			if (key is null or "")
				throw new ArgumentException("name Cannot be null or empty string");
			return Path.Combine(CascadeConstants.HOLD, "Collection", modelType.FullName, key);
		}

		/// <summary>
		/// Holds a named collection for a model type by creating a corresponding metadata entry.
		/// </summary>
		/// <typeparam name="Model">The model type for the collection to hold.</typeparam>
		/// <param name="name">The name of the collection to hold.</param>
		public void HoldCollection<Model>(string name) {
			HoldCollection(typeof(Model),name);
		}

		/// <summary>
		/// Holds a named collection for a specified model type by creating a metadata entry.
		/// </summary>
		/// <param name="modelType">The Type of the model collection to hold.</param>
		/// <param name="name">The name of the collection to hold.</param>
		public void HoldCollection(Type modelType, string name) {
			Log.Debug($"CascadeDataLayer HoldCollection {modelType.FullName} collection {name}");
			var path = HoldCollectionPath(modelType,name);
			MetaSet(path, String.Empty);
		}
		
		/// <summary>
		/// Unholds a named collection for a model type by removing the corresponding metadata entry.
		/// </summary>
		/// <typeparam name="Model">The model type of the collection to unhold.</typeparam>
		/// <param name="name">The name of the collection to unhold.</param>
		public void UnholdCollection<Model>(string name) {
			Log.Debug($"CascadeDataLayer UnholdCollection {nameof(Model)} collection {name}");
			var path = HoldCollectionPath(typeof(Model),name);
			if (MetaExists(path))
				MetaSet(path, null);
		}
		
		/// <summary>
		/// Checks if a named collection for a model type is currently held.
		/// </summary>
		/// <typeparam name="Model">The model type of the collection to check.</typeparam>
		/// <param name="name">The name of the collection to check.</param>
		/// <returns>True if the collection is held, false otherwise.</returns>
		public bool IsCollectionHeld<Model>(string name) {
			return MetaExists(HoldCollectionPath(typeof(Model),name));
		}
		
		/// <summary>
		/// Lists all named collections of a specified model type that are currently held.
		/// </summary>
		/// <param name="modelType">The Type of the model collections to list.</param>
		/// <returns>An enumerable of all names of held collections.</returns>
		public IEnumerable<object> ListHeldCollections(Type modelType) {
			return MetaList(HoldCollectionPath(modelType));
		}

		/// <summary>
		/// Unholds all instances and collections of the specified model type. 
		/// Optionally filter out entries older than a specified date.
		/// </summary>
		/// <param name="modelType">The Type of the model to clear holds for.</param>
		/// <param name="olderThan">Optional DateTime to filter which holds should be cleared.</param>
		public void UnholdAll(Type modelType, DateTime? olderThan=null) {
			if (!CascadeTypeUtils.IsBlobType(modelType))
				MetaClearPath(HoldCollectionPath(modelType),olderThan);
			MetaClearPath(HoldModelPath(modelType),olderThan);
		}

		/// <summary>
		/// Unholds all models and collections, clearing all associated metadata.
		/// Can target only metadata entries older than a specified date.
		/// </summary>
		/// <param name="olderThan">Optional DateTime to filter which holds should be cleared.</param>
		public void UnholdAll(DateTime? olderThan=null) {
			MetaClearPath(CascadeConstants.HOLD, olderThan, true);
		}

		/// <summary>
		/// Unholds all associations in both model and collection types, clearing all metadata.
		/// </summary>
		public void UnholdAll() {
			MetaClearAll();
		}
		#endregion
	}
}

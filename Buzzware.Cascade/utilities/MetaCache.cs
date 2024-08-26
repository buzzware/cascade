using System;
using System.Collections.Generic;
using System.IO;

namespace Buzzware.Cascade {

  /// <summary>
  /// The MetaCache class provides caching functionality for metadata associated with CascadeDataLayer. 
  /// It enables loading, storage, retrieval, and clearing of metadata in a dictionary structure, 
  /// maintaining a local cache and synchronizing changes with the CascadeDataLayer.
  /// As metadata is implemented using files, this cache can be used for fast UI updates by avoiding file access.
  /// </summary>
	public class MetaCache {
		
    // The path where metadata files are stored.
		private readonly string metaPath;
    
    // The CascadeDataLayer instance used for interacting with the data layer.
		private readonly CascadeDataLayer cascade;

    // An in-memory dictionary used to store metadata keys and values for quick access.
		private Dictionary<string,string> store;

    /// <summary>
    /// MetaCache Constructor
    /// Initializes a new MetaCache with a specified data layer and metadata path.
    /// </summary>
    /// <param name="cascade">The CascadeDataLayer instance used for metadata operations.</param>
    /// <param name="metaPath">The path to the directory where metadata files are stored.</param>
		public MetaCache(
			CascadeDataLayer cascade,
			string metaPath
		) {
			this.cascade = cascade;
			this.metaPath = metaPath;
			this.store = new Dictionary<string, string>();
		}

    /// <summary>
    /// Loads metadata from the metaPath directory into the local cache.
    /// Clears the existing cache and fetches fresh metadata from the data layer.
    /// </summary>
		public void Load() {
			store.Clear();
			var files = cascade.MetaList(this.metaPath);

      // Iterate over each file and add its metadata to the local cache.
			foreach (var file in files) {
				store[file] = cascade.MetaGet(Path.Combine(metaPath, file));
			}
		}
		
    /// <summary>
    /// Sets metadata for a specific path in both the local cache and the data layer.
    /// </summary>
    /// <param name="path">The relative path for the metadata.</param>
    /// <param name="value">The metadata value to be set. If null, the metadata is removed.</param>
		public void MetaSet(
			string path,
			string value
		) {
			if (value == null)
				store.Remove(path);
			else
				store[path] = value;

      // Update the metadata value in the data layer.
			cascade.MetaSet(Path.Combine(metaPath, path), value);
		}

    /// <summary>
    /// Sets the existence of metadata for a specific path in both the local cache and the data layer.
    /// </summary>
    /// <param name="path">The relative path for the metadata.</param>
    /// <param name="value">A boolean indicating whether the metadata should exist (true) or be removed (false).</param>
		public void MetaSetExists(string path, bool value) {
			if (value)
				store[path] = String.Empty;
			else
				store.Remove(path);

      // Update the metadata existence in the data layer.
			cascade.MetaSet(Path.Combine(metaPath, path), value ? String.Empty : null);
		}
		
    /// <summary>
    /// Retrieves the metadata value associated with a specific path from the local cache.
    /// </summary>
    /// <param name="path">The relative path for which the metadata value is requested.</param>
    /// <param name="value">Unused parameter in the method, could be intended for future use.</param>
    /// <returns>The metadata value associated with the specified path.</returns>
		public string MetaGet(
			string path,
			string value
		) {
			return store[path];
		}

    /// <summary>
    /// Checks for the existence of metadata for a specific path in the local cache.
    /// </summary>
    /// <param name="path">The relative path for which the existence check is performed.</param>
    /// <returns>True if the metadata exists, otherwise false.</returns>
		public bool MetaGetExists(string path) {
			return store.ContainsKey(path);
		}
		
    /// <summary>
    /// Clears metadata from the local cache and optionally removes files older than a specified date from the data layer.
    /// </summary>
    /// <param name="olderThan">An optional parameter to specify a date. Files older than this date will be removed.</param>
		public void MetaClear(DateTime? olderThan = null) {
			store.Clear();

      // Clear metadata files from the data layer based on the provided date.
			cascade.MetaClearPath(this.metaPath, recursive: true, olderThan: olderThan);

      // Reload the local cache if a date was provided for clearing.
			if (olderThan != null)
				Load();
		}

    /// <summary>
    /// Provides a list of all metadata keys present in the local cache.
    /// </summary>
    /// <returns>An enumerable containing all metadata keys stored in the local cache.</returns>
		public IEnumerable<string> MetaList() {
			return store.Keys;
		}
	}
}

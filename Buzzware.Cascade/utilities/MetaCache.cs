using System;
using System.Collections.Generic;
using System.IO;

namespace Buzzware.Cascade {
	
	public class MetaCache {
		
		private readonly string metaPath;
		private readonly CascadeDataLayer cascade;
		private Dictionary<string,string> store;

		public MetaCache(
			CascadeDataLayer cascade,
			string metaPath
		) {
			this.cascade = cascade;
			this.metaPath = metaPath;
			this.store = new Dictionary<string, string>();
		}

		public void Load() {
			store.Clear();
			var files = cascade.MetaList(this.metaPath);
			foreach (var file in files) {
				store[file] = cascade.MetaGet(Path.Combine(metaPath,file));
			}
		}
		
		public void MetaSet(
			string path,
			string value
		) {
			if (value == null)
				store.Remove(path);
			else
				store[path] = value;
			cascade.MetaSet(Path.Combine(metaPath,path),value);
		}

		public void MetaSetExists(string path, bool value) {
			if (value)
				store[path] = String.Empty;
			else
				store.Remove(path);
			cascade.MetaSet(Path.Combine(metaPath,path),value ? String.Empty : null);
		}
		
		public string MetaGet(
			string path,
			string value
		) {
			return store[path];
		}

		public bool MetaGetExists(string path) {
			return store.ContainsKey(path);
		}
		
		public void MetaClear(DateTime? olderThan = null) {
			store.Clear();
			cascade.MetaClearPath(this.metaPath, recursive:true, olderThan: olderThan);
			if (olderThan != null)
				Load();
		}

		public IEnumerable<string> MetaList() {
			return store.Keys;
		}
	}
}

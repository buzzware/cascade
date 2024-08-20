using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// Methods to serialize, deserialize, store, and manage pending operations for offline (ConnectionOnline == false)
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Gets the count of pending changes when the connection is offline.
		/// </summary>
		public int PendingCount
		{
			get
			{
				// Get Count from the ChangesPendingList but only when Disconnected (Offline)
				if (!ConnectionOnline)
				{
					return GetChangesPendingList().Count();
				}
				return 0;
			}
		}
		
		/// <summary>
		/// Finds a numeric filename that doesn't already exist in the specified folder.
		/// This method increments from the provided number until an available filename is found.
		/// </summary>
		/// <param name="folder">The folder where the file should be</param>
		/// <param name="number">The starting number for the filename</param>
		/// <param name="format">The numeric format to use for the filename</param>
		/// <param name="suffix">The suffix to append to the filename</param>
		/// <returns>The full path to the available file</returns>
		private string FindNumericFileDoesntExist(string folder, long number, string format, string suffix) {
			string filePath;
			var i = 0;
			do {
				filePath = Path.Combine(folder, (number+i).ToString(format) + suffix);
				i++;
			} while (File.Exists(filePath));
			return filePath;
		}

		/// <summary>
		/// Serializes a RequestOp object into a JsonNode while also extracting and returning any external content.
		/// </summary>
		/// <param name="op">The RequestOp object to serialize</param>
		/// <param name="externalContent">Dictionary containing the external content if any (key: filename suffix, value: content)</param>
		/// <returns>A JsonNode representing the serialized RequestOp</returns>
		public JsonNode SerializeRequestOp(
			RequestOp op, 
			out IReadOnlyDictionary<string, byte[]> externalContent	// filename suffix, content
		) {
			var dic = new Dictionary<string, object?>();
			dic[nameof(op.Verb)] = op.Verb.ToString();
			dic[nameof(op.Type)] = op.Type.FullName;
			dic[nameof(op.Id)] = op.Id;
			dic[nameof(op.TimeMs)] = op.TimeMs;

			var externalFiles = new Dictionary<string, string>();		// property, filename suffix
			var externalContentWorking = new Dictionary<string, byte[]>();
			
			var value = op.Value;
			switch (value) {
				case byte[] bytes:
					externalFiles[nameof(op.Value)] = nameof(op.Value);
					externalContentWorking[nameof(op.Value)] = bytes;
					value = null;
					break;
				default:
				case null:
					// do nothing
					break;
			}
			dic[nameof(op.Value)] = serialization.SerializeToNode(value);
			
			if (op.Criteria!=null)
				dic[nameof(op.Criteria)] = serialization.SerializeToNode(op.Criteria);
			if (op.Extra!=null)
				dic[nameof(op.Extra)] = serialization.SerializeToNode(op.Extra);
			
			var node = serialization.SerializeToNode(dic);
			externalContent = externalContentWorking;
			
			return node;
		}

		/// <summary>
		/// Deserializes a JSON string into a RequestOp object while extracting any external links.
		/// </summary>
		/// <param name="s">The serialized JSON string representation of the RequestOp</param>
		/// <param name="externals">Dictionary of external file links found during deserialization</param>
		/// <returns>The deserialized RequestOp object</returns>
		public RequestOp DeserializeRequestOp(string? s, out IReadOnlyDictionary<string, string> externals) {
			Log.Debug("DeserializeRequestOp: "+s);
			var el = serialization.DeserializeElement(s);
			Enum.TryParse<RequestVerb>(el.GetProperty(nameof(RequestOp.Verb)).GetString(), out var verb);
			var typeName = el.GetProperty(nameof(RequestOp.Type)).GetString();
			Type type; // Type.GetType(typeName,true);
			if (verb == RequestVerb.BlobGet || verb == RequestVerb.BlobPut || verb == RequestVerb.BlobDestroy)
				type = CascadeTypeUtils.BlobType;
			else
				type = Origin.LookupModelType(typeName);

			if (el.HasProperty("externals")) {
				var dic = serialization.DeserializeDictionaryOfNormalTypes(el.GetProperty("externals")); 
				externals = dic.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value?.ToString() ?? String.Empty
				);;
			} else
				externals = ImmutableDictionary<string, string>.Empty; 
			
			//var externals = serialization.DeserializeType<Dictionary<string,string>>(()) "externals"
			
			object? id = null;
			var idProperty = el.GetProperty(nameof(RequestOp.Id));
			if (verb == RequestVerb.Get || verb == RequestVerb.Update || verb == RequestVerb.Replace || verb == RequestVerb.Destroy || verb == RequestVerb.Create) {
				var idType = CascadeTypeUtils.GetCascadeIdType(type);
				if (idProperty.ValueKind == JsonValueKind.Number)
					id = CascadeTypeUtils.ConvertTo(idType, idProperty.GetInt64());
				else if (idProperty.ValueKind == JsonValueKind.String)
					id = CascadeTypeUtils.ConvertTo(idType, idProperty.GetString());
				else
					throw new TypeAccessException("Failed to interpret id value in correct type");
			} else if (verb == RequestVerb.BlobGet || verb == RequestVerb.BlobPut || verb == RequestVerb.BlobDestroy) {
				id = idProperty.GetString();	// path
			}
			
			object? value = null,criteria = null;
			if (verb == RequestVerb.Update) {
				value = serialization.DeserializeDictionaryOfNormalTypes(el.GetProperty(nameof(RequestOp.Value)));
			} else if (verb == RequestVerb.Execute) {
				value = el.GetProperty(nameof(RequestOp.Value)).ToString();
				criteria = serialization.DeserializeDictionaryOfNormalTypes(el.GetProperty(nameof(RequestOp.Criteria)));
			} else if (type.IsSubclassOf(typeof(SuperModel))) {
				value = serialization.DeserializeType(type, el.GetProperty(nameof(RequestOp.Value)));
			}
			return new RequestOp(
				el.GetProperty(nameof(RequestOp.TimeMs)).GetInt64(),
				type,
				verb,
				id,
				value: value,
				populate: null,
				freshnessSeconds: null,
				populateFreshnessSeconds: null,
				criteria: criteria,
				key: null,
				extra: null
			);
		}
		
		/// <summary>
		/// Adds a new pending RequestOp change to the system and saves it in a designated path.
		/// </summary>
		/// <param name="op">The RequestOp representing the change</param>
		/// <returns>string representing the file path where the operation was saved</returns>
		public async Task<string> AddPendingChange(RequestOp op) {
			var typeStr = op.Type.Name;
			var folder = Config.PendingChangesPath; //Path.Combine(Config.PendingChangesPath, typeStr);
			if (!Directory.Exists(folder))
				Directory.CreateDirectory(folder);

			var content = SerializeRequestOp(op, out var externalContent);
			var filePath = FindNumericFileDoesntExist(folder, op.TimeMs, "D15", ".json");

			var externalsNode = new JsonObject();
			foreach (var external in externalContent) {
				externalsNode[external.Key] = ExternalBinaryPathFromPendingChangePath(Path.GetFileName(filePath), external.Key);
			}
			content["externals"] = externalsNode;
			
			await CascadeUtils.EnsureFileOperation(async () => {
				await CascadeUtils.WriteTextFile(filePath, content.ToJsonString());
				foreach (var kvp in externalContent) {
					await CascadeUtils.WriteBinaryFile(ExternalBinaryPathFromPendingChangePath(filePath, kvp.Key), kvp.Value);
				}
			});
			return filePath!;
		}

		/// <summary>
		/// Converts a pending change file path to its corresponding binary file path.
		/// </summary>
		/// <param name="filePath">The path of the original file</param>
		/// <param name="externalPath">The name of the external segment to append</param>
		/// <returns>The reconstructed path to the binary file</returns>
		public string ExternalBinaryPathFromPendingChangePath(string filePath, string externalPath) {
			return Path.ChangeExtension(filePath, "__" + externalPath + ".bin").Replace(".__","__");
		}
		
		/// <summary>
		/// Retrieves a list of pending change filenames from the configured directory.
		/// </summary>
		/// <returns>An enumerable of pending change filenames</returns>
		public IEnumerable<string> GetChangesPendingList() {
			if (!Directory.Exists(Config.PendingChangesPath))
				return new string[] {};
			var items = Directory.GetFiles(Config.PendingChangesPath);
			return items.Select(Path.GetFileName).Where(f => !f.Contains("__") && f.EndsWith(".json")).ToImmutableArray().Sort();
		}
		
		/// <summary>
		/// Removes a pending change file based on its name.
		/// </summary>
		/// <param name="filename">The name of the file to remove</param>
		private async Task RemoveChangePendingFile(string filename) {
			var filepath = Path.Combine(Config.PendingChangesPath, filename);
			CascadeUtils.EnsureFileOperationSync(() => {
				if (File.Exists(filepath))
					File.Delete(filepath);
			});
		}
		
		/// <summary>
		/// Retrieves and reconstructs a list of pending RequestOps with associated external file links.
		/// </summary>
		/// <returns>A list of tuples containing the filename, RequestOp, and associated externals</returns>
		public async Task<List<Tuple<string, RequestOp, IReadOnlyDictionary<string, string>?>>> GetChangesPending() {
			var changes = new List<Tuple<string, RequestOp, IReadOnlyDictionary<string, string>?>>();
			var list = GetChangesPendingList();
			foreach (var filename in list) {
				var content = CascadeUtils.LoadFileAsString(Path.Combine(Config.PendingChangesPath, filename));
				var requestOp = DeserializeRequestOp(content, out var externals);
				foreach (var external in externals) {
					var exfile = Path.Combine(Config.PendingChangesPath,external.Value);
					var blob = await CascadeUtils.ReadBinaryFile(exfile);
					var propertyName = external.Key;
					if (propertyName.Contains("."))
						throw new StandardException("sub properties not implemented");
					var property = typeof(RequestOp).GetField(propertyName);	// typically Value which is a field
					if (property==null)
						throw new StandardException($"property {propertyName} unknown");
					property.SetValue(requestOp,blob);
				}
				changes.Add(new Tuple<string, RequestOp,  IReadOnlyDictionary<string,string>?>(filename,requestOp,externals));
			}
			return changes;
		}
		
		/// <summary>
		/// Checks if there are any pending changes stored in the system.
		/// </summary>
		/// <returns>bool indicating the existence of pending changes</returns>
		public bool HasChangesPending() {
			return GetChangesPendingList().Any();
		}
		
		/// <summary>
		/// Clears all pending changes by deleting their stored files.
		/// </summary>
		public async Task ClearChangesPending() {
			CascadeUtils.EnsureFileOperationSync(() => {
				if (Directory.Exists(Config.PendingChangesPath))
					Directory.Delete(Config.PendingChangesPath, true);
			});
			RaisePropertyChanged(nameof(PendingCount));
		}
		
		/// <summary>
		/// Uploads all pending changes, processes them, and removes the changes once uploaded.
		/// </summary>
		/// <param name="progressMessage">Optional action to report progress messages</param>
		/// <param name="progressCount">Optional action to report remaining count of pending changes</param>
		public async Task UploadChangesPending(Action<string>? progressMessage = null,Action<int>? progressCount = null) {
			progressMessage?.Invoke("Load Changes");
			var changes = (await GetChangesPending()).ToImmutableArray();
			progressCount?.Invoke(changes.Length);
			for (var index = 0; index < changes.Length; index++) {
				var change = changes[index];
				progressMessage?.Invoke($"Uploading Changes");
				await InnerProcess(change.Item2, true);
				await RemoveChangePending(change.Item1,change.Item3?.Values);
				progressCount?.Invoke(changes.Length - index - 1);
			}
			// Update Home Screen Pending Count after Uploading Changes
			RaisePropertyChanged(nameof(PendingCount));
			progressMessage?.Invoke("Changes Uploaded.");
		}

		/// <summary>
		/// Removes a main pending change file along with its associated external files.
		/// </summary>
		/// <param name="main">The main file to remove</param>
		/// <param name="externals">An enumeration of associated external files to remove</param>
		public async Task RemoveChangePending(string main, IEnumerable<string>? externals) {
			await RemoveChangePendingFile(main);
			if (externals != null) {
				foreach (var f in externals) {
					await RemoveChangePendingFile(f);
				}	
			}
		}
	}
}

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
	/// </summary>
	public partial class CascadeDataLayer {

		// Showing Pending Counter on the Home Screen
		// To trigger a PropertyChanged event on this or any other property, use RaisePropertyChanged
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
		
		private string FindNumericFileDoesntExist(string folder, long number, string format, string suffix) {
			string filePath;
			var i = 0;
			do {
				filePath = Path.Combine(folder, (number+i).ToString(format) + suffix);
				i++;
			} while (File.Exists(filePath));
			return filePath;
		}

		public JsonNode SerializeRequestOp(
			RequestOp op, 
			out IReadOnlyDictionary<string, byte[]> externalContent	// filename suffix, content
		) {
			// if (op.Verb == RequestVerb.BlobPut)
			// 	throw new NotImplementedException("Serialisation of Blob values not yet supported");
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
			// if (verb == RequestVerb.BlobPut)
			// 	throw new NotImplementedException("Deserialisation of Blob values not yet supported");
			
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
		
		public async Task<string> AddPendingChange(RequestOp op) {
			var typeStr = op.Type.Name;
			var folder = Config.PendingChangesPath; //Path.Combine(Config.PendingChangesPath, typeStr);
			if (!Directory.Exists(folder))
				Directory.CreateDirectory(folder);
			//var filePath = FindNumericFileDoesntExist(folder, op.TimeMs, "D15", $"__{typeStr}__{op.IdAsString}.json");
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

		public string ExternalBinaryPathFromPendingChangePath(string filePath, string externalPath) {
			return Path.ChangeExtension(filePath, "__" + externalPath + ".bin").Replace(".__","__");
		}
		
		public IEnumerable<string> GetChangesPendingList() {
			if (!Directory.Exists(Config.PendingChangesPath))
				return new string[] {};
			var items = Directory.GetFiles(Config.PendingChangesPath);
			return items.Select(Path.GetFileName).Where(f => !f.Contains("__") && f.EndsWith(".json")).ToImmutableArray().Sort();
		}
		
		private async Task RemoveChangePendingFile(string filename) {
			var filepath = Path.Combine(Config.PendingChangesPath, filename);
			CascadeUtils.EnsureFileOperationSync(() => {
				if (File.Exists(filepath))
					File.Delete(filepath);
			});
		}
		
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
		
		public bool HasChangesPending() {
			return GetChangesPendingList().Any();
		}
		
		public async Task ClearChangesPending() {
			CascadeUtils.EnsureFileOperationSync(() => {
				if (Directory.Exists(Config.PendingChangesPath))
					Directory.Delete(Config.PendingChangesPath, true);
			});
			RaisePropertyChanged(nameof(PendingCount));
		}
		
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

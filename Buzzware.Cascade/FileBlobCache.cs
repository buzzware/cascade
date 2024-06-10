using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade {
	public class FileBlobCache : IBlobCache {
		private readonly string _tempDir;

		public CascadeDataLayer Cascade { get; set; }

		private string FullBlobPath { get; }

		private readonly string _blobDirectory = "Blob";		
		
		public FileBlobCache(string tempDir) {
			_tempDir = tempDir;
			FullBlobPath = ToFilePath(_blobDirectory);
			Directory.CreateDirectory(FullBlobPath);
		}

		protected string ToFilePath(string path) {
			return Path.Combine(_tempDir, path);
		}

		protected string GetBlobPath(string path) { 
			return Path.Combine(_blobDirectory, path);
		}
		
		public string GetModelFilePath(string path) {
			return ToFilePath(GetBlobPath(path)); 
		}
		
		public async Task ClearAll(bool exceptHeld, DateTime? olderThan) {
			if (exceptHeld || olderThan!=null) {
				// models
				foreach (var file in Directory.GetFiles(FullBlobPath,"*",SearchOption.AllDirectories)) {
					if (olderThan != null) {
						var fileTime = File.GetLastWriteTimeUtc(file);
						if (fileTime.IsGreaterOrEqual(olderThan.Value))
							continue;
					}
					var path = CascadeUtils.GetRelativePath(FullBlobPath, file);
					if (exceptHeld) {
						if (Cascade!.IsHeldBlob(path))
							continue;
					}
					//Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} id {id}");
					CascadeUtils.EnsureFileOperationSync(() => {
						File.Delete(file);
					});
					// var cachePath = CascadeUtils.GetRelativePath(_fileDir, file);
					// cache.TryRemove(cachePath, out var removed);
				}
			} else {
				// cache.Clear();
				// Delete all files in the models directory
				foreach (var file in Directory.GetFiles(FullBlobPath)) {
					//Log.Debug($"FastFileClassCache Clear {typeof(Model).FullName} id {Path.GetFileNameWithoutExtension(file)}");
					CascadeUtils.EnsureFileOperationSync(() => {
						File.Delete(file);
					});
				}
			}
		}

		public async Task<OpResponse> Fetch(RequestOp requestOp) {
			if (requestOp.Verb != RequestVerb.BlobGet)
				throw new Exception("requestOp.Verb != Blob");
			bool exists;
			long arrivedAtMs;
	    
			var path = requestOp.Id as string;
			if (path == null)
				throw new Exception("Id must be a string");

			string blobFilePath = GetModelFilePath(path);
			exists = File.Exists(blobFilePath);
			arrivedAtMs = exists ? CascadeUtils.toUnixMilliseconds(File.GetLastWriteTimeUtc(blobFilePath)) : -1;
			if (
				exists && 
				requestOp.FreshnessSeconds>=0 && 
				(requestOp.FreshnessSeconds==CascadeDataLayer.FRESHNESS_ANY || ((Cascade.NowMs-arrivedAtMs) <= requestOp.FreshnessSeconds*1000))
			) {
				var loaded = await LoadBlob(blobFilePath);
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
		}
		
		public async Task Store(OpResponse opResponse) {
			var path = opResponse.RequestOp.Id as string;
			long arrivedAt = opResponse.ArrivedAtMs ?? Cascade.NowMs;
			
			if (path == null)
				throw new Exception("Bad path");
			try {
				string modelFilePath = GetModelFilePath(path)!;
				if (opResponse.ResultIsEmpty()) {
					File.Delete(modelFilePath);
				} else {
					if (!(opResponse.Result is IReadOnlyList<byte>))
						throw new ArgumentException("Result must be null or IReadOnlyList<byte>");
					await StoreBlob(modelFilePath, (IReadOnlyList<byte>) opResponse.Result, arrivedAt);
				}
			} catch (Exception e) {
				Log.Debug(e.Message);   // sharing violation exception sometimes happens here
			}
		}

		private async Task<IReadOnlyList<byte>?> LoadBlob(string path) {
			if (!File.Exists(path))
				return null;

			byte[] result;
			using (FileStream stream = File.OpenRead(path)) {
				result = new byte[stream.Length];
				await stream.ReadAsync(result, 0, (int)stream.Length);
			}
			return result.ToImmutableArray();			
		}
		
		private async Task StoreBlob(string path, IReadOnlyList<byte> blob, long arrivedAt) {
			await Task.Run(async () => {
				if (!Directory.Exists(Path.GetDirectoryName(path)))
					Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				
				using FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 256*1024, true);
				await fileStream.WriteAsync(blob.ToArray(), 0, blob.Count).ConfigureAwait(false);
				
				File.SetLastWriteTimeUtc(path, CascadeUtils.fromUnixMilliseconds(arrivedAt));
			});
		}
	}
}

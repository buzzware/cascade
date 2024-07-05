using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade {
	public static class CascadeUtils {
		public static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		
		// "Where Collections" are collections whose key begins with "WHERE__" and is defined using this function.
		// Their key fully describes what they are - a collection of the given type where the given property has the given value.
		// In future, the framework could parse and evaluate these names as a query eg. to refresh them
		// Other collections should not begin with "WHERE__", and property names or values should not contain more than one consecutive underscore
		// This function is primarily intended for collections created for associations, especially HasMany.
		public static string WhereCollectionKey(string typeName,string property,string value) {
			return $"WHERE__{typeName}__{property}__{value}";
		}

		public static Int64 toUnixMilliseconds(DateTime aDateTime) {
			return (Int64)(aDateTime.ToUniversalTime() - epoch).TotalMilliseconds;
		}

		public static Int64 toUnixMilliseconds(int year, int month = 1, int day = 1, int hour = 0, int min = 0, int sec = 0) {
			return toUnixMilliseconds(new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc));
		}
		
		public static DateTime fromUnixMilliseconds(Int64 aTimems) {
			return epoch.AddMilliseconds(aTimems);
		}
		
		public static async Task<byte[]> ReadBinaryFile(string filePath, int bufferSize = 8192) {
			byte[] buffer = new byte[bufferSize];
			using (MemoryStream ms = new MemoryStream())
			{
				using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: buffer.Length, useAsync: true))
				{
					int read;
					while ((read = await file.ReadAsync(buffer, 0, buffer.Length)) > 0)
					{
						ms.Write(buffer, 0, read);
					}
				}
				return ms.ToArray();
			}
		}
		
		public static async Task WriteBinaryFile(string path, byte[] content, int bufferSize = 8192) {
			using FileStream destinationStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: bufferSize, useAsync: true);
			await destinationStream.WriteAsync(content, 0, content.Length);
		}

		public static async Task WriteTextFile(string filePath, string? content) {
			using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
				using (var writer = new StreamWriter(stream)) {
					await writer.WriteAsync(content); //.ConfigureAwait(false);
				}
			}
		}
		
		public static async Task<string?> LoadFileAsStringAsync(string aPath) {
			if (!File.Exists(aPath))
				return null;
			string? content;
			using (var stream = new FileStream(aPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) {
				using (var reader = new StreamReader(stream)) {
					content = await reader.ReadToEndAsync();
				}
			}
			return content;
		}
		
		public static string? LoadFileAsString(string path) {
			if (!File.Exists(path))
				return null;
			string? content;
			using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				using (var reader = new StreamReader(stream)) {
					content = reader.ReadToEnd();
				}
			}
			return content;
		}
		
		public static async Task WriteStringToFileAsync(string path, string content) {
			using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
				using (var writer = new StreamWriter(stream)) {
					await writer.WriteAsync(content);
				}
			}
		}
		
		public static void WriteStringToFile(string path, string content) {
			using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) {
				using (var writer = new StreamWriter(stream)) {
					writer.Write(content);
				}
			}
		}
		
		// public async Task Populate(ICascadeModel model, string property) {
		// 	var modelType = model.GetType();
		// 	var propertyInfo = modelType.GetProperty(property);
		//
		// 	if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute),true).FirstOrDefault() is HasManyAttribute hasMany) {
		// 		await processHasMany(model, modelType, propertyInfo!, hasMany);
		// 	} else if (propertyInfo?.GetCustomAttributes(typeof(BelongsToAttribute),true).FirstOrDefault() is BelongsToAttribute belongsTo) {
		// 		await processBelongsTo(model, modelType, propertyInfo!, belongsTo);
		// 	}
		// }


		// public static string[] SplitKey(string aKey) {
		// 	return aKey.Split(new string[] {"__"},StringSplitOptions.RemoveEmptyEntries);
		// }
		//
		// public static string ExtractResource(string aKey) {
		// 	if (aKey==null)
		// 		return null;
		// 	var parts = SplitKey(aKey);
		// 	return parts.Length == 0 ? null : parts[0];
		// }
		//
		// public static string ExtractId(string aKey) {
		// 	if (aKey==null)
		// 		return null;
		// 	var parts = SplitKey(aKey);
		// 	return parts.Length < 2 ? null : parts[1];
		// }
		//
		// public static long ExtractLongId(string aKey) {
		// 	if (aKey==null)
		// 		return 0;
		// 	var id = ExtractId(aKey);
		// 	return id == null ? 0 : LongId(id);
		// }
		//
		// public static long LongId(string aResourceId) {
		// 	EnsureIsResourceId(aResourceId);
		// 	return Convert.ToInt64(aResourceId);
		// }
		//
		// public static string EnsureIsResourceId(string aResourceId) {
		// 	if (!IsResourceId(aResourceId))
		// 		throw new Exception("aResourceId is not a valid resource id");
		// 	return aResourceId;
		// }
		//
		// public static long EnsureIsResourceId(long aResourceId) {
		// 	if (aResourceId==0)
		// 		throw new Exception("aResourceId is not a valid resource id");
		// 	return aResourceId;
		// }
		//
		// public static bool IsResourceId(string aResourceId) {
		// 	return !(aResourceId == null || aResourceId == "0" || aResourceId == "");
		// }		
		//
		// public static bool IsResourceId(int aResourceId) {
		// 	return !(aResourceId == 0);
		// }
		//
		// public static bool IsResourceId(long aResourceId) {
		// 	return !(aResourceId == 0);
		// }
		//
		//
		// public static string JoinKey(string resource, string id) {
		// 	if (resource==null)
		// 		throw new ArgumentException("A key needs a resource");
		// 	if (id == null)
		// 		return resource;
		// 	return resource + "__" + id;
		// }
		private const int MAX_WRITE_ATTEMPTS = 5;

		public static T EnsureFileOperationSync<T>(
			Func<T> func,
			int maxAttempts = 5,
			int sleepMs = 10
		) {
			var attempts = 0;
			var random = new Random();
			do {
				try {
					attempts++;
					var result = func();
					if (attempts>1)
						Log.Debug("EnsureFileOperation succeeded attempt {Attempts}", attempts);
					return result;
				} catch (Exception e) {
					Log.Debug("EnsureFileOperation failed attempt {Attempts}", attempts);
					if (!IsFileSharingException(e) || attempts >= maxAttempts)
						throw;
				}
				if (sleepMs > 0) {
					var randomSleepMs = random.Next(1, sleepMs);
					Thread.Sleep(randomSleepMs);
				}
			} while (true) ;
		}

		public static void EnsureFileOperationSync(
			Action func,
			int maxAttempts = 5,
			int sleepMs = 10
		) {
			EnsureFileOperationSync<object?>(
				() => {
					func();
					return null;
				}, 
				maxAttempts,
				sleepMs
			);
		}
		
		
		public static async Task<T> EnsureFileOperation<T>(
			Func<Task<T>> func,
			int maxAttempts = 5,
			int sleepMs = 10
		) {
			var attempts = 0;
			do {
				try {
					attempts++;
					var result = await func();
					if (attempts>1)
						Log.Debug("EnsureFileOperation succeeded attempt {Attempts}", attempts);
					return result;
				} catch (IOException e) {
					Log.Debug("EnsureFileOperation failed attempt {Attempts}", attempts);
					if (attempts >= maxAttempts)
						throw;
				}
				if (sleepMs>0)
					await Task.Delay(sleepMs);
			} while (true) ;
		}

		public static Task EnsureFileOperation(
			Func<Task> func,
			int maxAttempts = 5,
			int sleepMs = 10
		) {
			return EnsureFileOperation<object?>(
				async () => {
					await func();
					return null;
				}, 
				maxAttempts,
				sleepMs
			);
		}
		
		public static bool IsFileSharingException(Exception exception) {
			var ioException = exception as IOException;
			if (ioException == null)
				return false;
			
			int errorCode = (int)(ioException.HResult & 0x0000FFFF);
			// https://learn.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-
			var isFileIssue = errorCode == 0x20 || errorCode == 0x21 || errorCode == 0x21;
			return isFileIssue;
		}

		public static bool IsNetworkIOException(Exception exception) {
			var ioException = exception as IOException;
			if (ioException == null)
				return false;
			
			int errorCode = (int)(ioException.HResult & 0x0000FFFF);
			// https://learn.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-
			var isFileIssue = (errorCode == 0x20) || (errorCode == 0x21) || (errorCode == 0x21);
			return !isFileIssue;
		}

		public static bool IsNonStandardNetworkFailureException(Exception exception) {
			return IsNetworkIOException(exception) ||
			       exception is System.Net.Sockets.SocketException ||
			       exception is System.Net.WebException;
		}

		public static bool IsNetworkFailureException(Exception exception) {
			return exception is NoNetworkException || IsNonStandardNetworkFailureException(exception);
		}
		
		public static string GetRelativePath(string basePath, string absolutePath) {
			string[] baseDirs = basePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string[] absDirs = absolutePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			// Find the common root
			int length = Math.Min(baseDirs.Length, absDirs.Length);
			int lastCommonRoot = -1;

			for (int i = 0; i < length; i++)
			{
				if (string.Equals(baseDirs[i], absDirs[i], StringComparison.OrdinalIgnoreCase))
				{
					lastCommonRoot = i;
				}
				else
				{
					break;
				}
			}

			// If no common root, paths do not have a relative path
			if (lastCommonRoot == -1)
			{
				throw new ArgumentException("Paths do not have a common base");
			}

			// Add relative folders in from basePath to the common root
			var relativePath = new System.Text.StringBuilder();
			for (int i = lastCommonRoot + 1; i < baseDirs.Length; i++)
			{
				if (baseDirs[i].Length > 0)
				{
					relativePath.Append("..");
					relativePath.Append(Path.DirectorySeparatorChar);
				}
			}

			// Add to the relative path the remaining absDirs folders
			for (int i = lastCommonRoot + 1; i < absDirs.Length; i++)
			{
				relativePath.Append(absDirs[i]);
				if (i < absDirs.Length - 1)
				{
					relativePath.Append(Path.DirectorySeparatorChar);
				}
			}

			return relativePath.ToString();
		}		
		
		public static string? UpToFolderNamed(string path, string folderName) {
			var parts = path.Split('/');
			var folderIndex = Array.LastIndexOf(parts, folderName);
			if (folderIndex < 0)
				return null;
			var result = string.Join("/", parts.Take(folderIndex + 1));
			return result;
		}

		public static string? AboveFolderNamed(string path, string folderName) {
			var path2 = UpToFolderNamed(path, folderName);
			if (path2 == null)
				return null;
			return Path.GetDirectoryName(path2);
		}
	}
}

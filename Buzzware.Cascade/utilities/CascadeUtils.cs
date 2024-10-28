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

  /// <summary>
  /// A utility class providing general helper methods for the Cascade library.
  /// </summary>
	public static class CascadeUtils {
  
    /// <summary>
    /// The Unix epoch reference time, set to January 1, 1970, at 00:00:00 UTC.
    /// </summary>
		public static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Current Unix time in milliseconds.
    /// </summary>
		public static long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Generates a key for 'Where Collections', which are collections where 
    /// a given property of a given type has a specific value.
    /// The key format is "WHERE__{typeName}__{property}__{value}". These collections
    /// are particularly useful for associations like HasMany.
    /// </summary>
    /// <param name="typeName">The name of the type for the collection.</param>
    /// <param name="property">The property name being queried.</param>
    /// <param name="value">The value that property is being matched against.</param>
    /// <returns>A key representing a specific query condition for a collection.</returns>
		public static string WhereCollectionKey(string typeName,string property,string value) {
			return $"WHERE__{typeName}__{property}__{value}";
		}

    /// <summary>
    /// Converts a DateTime object to Unix time in milliseconds.
    /// </summary>
    /// <param name="aDateTime">The DateTime object to convert.</param>
    /// <returns>Unix time representation of the provided DateTime in milliseconds.</returns>
		public static Int64 toUnixMilliseconds(DateTime aDateTime) {
			return (Int64)(aDateTime.ToUniversalTime() - epoch).TotalMilliseconds;
		}

    /// <summary>
    /// Converts a specified date and time to Unix time in milliseconds.
    /// The time is considered to be in UTC.
    /// </summary>
    /// <param name="year">Year of the date to convert.</param>
    /// <param name="month">Month of the date to convert (1-12).</param>
    /// <param name="day">Day of the month to convert (1-31).</param>
    /// <param name="hour">Hour of the day to convert (0-23).</param>
    /// <param name="min">Minute of the hour to convert (0-59).</param>
    /// <param name="sec">Second of the minute to convert (0-59).</param>
    /// <returns>Unix time representation of the specified date and time in milliseconds.</returns>
		public static Int64 toUnixMilliseconds(int year, int month = 1, int day = 1, int hour = 0, int min = 0, int sec = 0) {
			return toUnixMilliseconds(new DateTime(year, month, day, hour, min, sec, DateTimeKind.Utc));
		}
		
    /// <summary>
    /// Converts Unix time in milliseconds back to a DateTime object.
    /// The resulting DateTime is in UTC.
    /// </summary>
    /// <param name="aTimems">Unix time in milliseconds.</param>
    /// <returns>A DateTime object representing the given Unix time.</returns>
		public static DateTime fromUnixMilliseconds(Int64 aTimems) {
			return epoch.AddMilliseconds(aTimems);
		}
		
    /// <summary>
    /// Reads the contents of a binary file asynchronously.
    /// </summary>
    /// <param name="filePath">Path of the file to read.</param>
    /// <param name="bufferSize">Size of the buffer to use for reading bytes, default is 8192.</param>
    /// <returns>The byte array containing the contents of the file.</returns>
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
		
    /// <summary>
    /// Writes a byte array to a binary file asynchronously.
    /// </summary>
    /// <param name="path">The file path where the data should be written.</param>
    /// <param name="content">The byte array to write to the file.</param>
    /// <param name="bufferSize">Size of the buffer for writing, default is 8192.</param>
		public static async Task WriteBinaryFile(string path, byte[] content, int bufferSize = 8192) {
			using FileStream destinationStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: bufferSize, useAsync: true);
			await destinationStream.WriteAsync(content, 0, content.Length);
		}

    /// <summary>
    /// Writes a string content to a text file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the file where the content will be written.</param>
    /// <param name="content">String content to be written to the file.</param>
		public static async Task WriteTextFile(string filePath, string? content) {
			using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
				using (var writer = new StreamWriter(stream)) {
					await writer.WriteAsync(content); //.ConfigureAwait(false);
				}
			}
		}
		
    /// <summary>
    /// Loads the content of a file as a string asynchronously.
    /// If the file does not exist, returns null.
    /// </summary>
    /// <param name="aPath">The path to the file to read.</param>
    /// <returns>The content of the file as a string, or null if the file does not exist.</returns>
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
		
    /// <summary>
    /// Synchronously loads the content of a file as a string.
    /// If the file does not exist, returns null.
    /// </summary>
    /// <param name="path">The path to the file to read.</param>
    /// <returns>The content of the file as a string, or null if the file does not exist.</returns>
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
		
    /// <summary>
    /// Writes a string content to a file asynchronously.
    /// </summary>
    /// <param name="path">Path to the file where the content will be written.</param>
    /// <param name="content">String content to write to the file.</param>
		public static async Task WriteStringToFileAsync(string path, string content) {
			using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
				using (var writer = new StreamWriter(stream)) {
					await writer.WriteAsync(content);
				}
			}
		}
		
    /// <summary>
    /// Writes a string content to a file synchronously.
    /// </summary>
    /// <param name="path">Path to the file where the content will be written.</param>
    /// <param name="content">String content to write to the file.</param>
		public static void WriteStringToFile(string path, string content) {
			using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) {
				using (var writer = new StreamWriter(stream)) {
					writer.Write(content);
				}
			}
		}
		
		private const int MAX_WRITE_ATTEMPTS = 5;

    /// <summary>
    /// Tries to perform a file operation with a specified number of attempts
    /// and optional sleep time between attempts. Used to handle transient 
    /// file sharing issues.
    /// </summary>
    /// <typeparam name="T">The type returned by the file operation.</typeparam>
    /// <param name="func">The file operation func to attempt.</param>
    /// <param name="maxAttempts">Maximum number of attempts before throwing an exception.</param>
    /// <param name="sleepMs">Time in milliseconds to sleep between attempts.</param>
    /// <returns>The result of the file operation if successful.</returns>
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

    /// <summary>
    /// Tries to perform a file operation with a specified number of attempts
    /// and optional sleep time between attempts. Used to handle transient 
    /// file sharing issues.
    /// </summary>
    /// <param name="func">The file operation action to attempt.</param>
    /// <param name="maxAttempts">Maximum number of attempts before throwing an exception.</param>
    /// <param name="sleepMs">Time in milliseconds to sleep between attempts.</param>
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
		
    /// <summary>
    /// Asynchronously attempts a file operation multiple times, waiting between attempts.
    /// Used to handle transient sharing issues with I/O operations.
    /// </summary>
    /// <typeparam name="T">The type returned by the async operation.</typeparam>
    /// <param name="func">The async function to attempt.</param>
    /// <param name="maxAttempts">Maximum number of attempts before failing.</param>
    /// <param name="sleepMs">Milliseconds to wait between attempts.</param>
    /// <returns>The result of a successful operation.</returns>
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

    /// <summary>
    /// Asynchronously attempts a void file operation multiple times, waiting between attempts.
    /// Used to handle transient sharing issues with I/O operations.
    /// </summary>
    /// <param name="func">The async operation to attempt.</param>
    /// <param name="maxAttempts">Maximum number of attempts before failing.</param>
    /// <param name="sleepMs">Milliseconds to wait between attempts.</param>
    /// <returns>An awaitable Task.</returns>
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
		
    /// <summary>
    /// Checks if an exception is related to file sharing or locking issues.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if it's a file sharing exception; otherwise, false.</returns>
		public static bool IsFileSharingException(Exception exception) {
			var ioException = exception as IOException;
			if (ioException == null)
				return false;
			
			int errorCode = (int)(ioException.HResult & 0x0000FFFF);
			// https://learn.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-
			var isFileIssue = errorCode == 0x20 || errorCode == 0x21 || errorCode == 0x21;
			return isFileIssue;
		}

    /// <summary>
    /// Checks if an exception is related to network issues excluding typical file sharing problems.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>True if the exception is a network IO exception, but not due to file sharing.</returns>
		public static bool IsNetworkIOException(Exception exception) {
			var ioException = exception as IOException;
			if (ioException == null)
				return false;
			
			int errorCode = (int)(ioException.HResult & 0x0000FFFF);
			// https://learn.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-
			var isFileIssue = (errorCode == 0x20) || (errorCode == 0x21) || (errorCode == 0x21);
			return !isFileIssue;
		}

    /// <summary>
    /// Determines if an exception is due to a network failure not contained
    /// in the standard IOException causes, including Sockets and WebException.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is due to non-standard network failures.</returns>
		public static bool IsNonStandardNetworkFailureException(Exception exception) {
			return IsNetworkIOException(exception) ||
			       exception is System.Net.Sockets.SocketException ||
			       exception is System.Net.WebException;
		}

    /// <summary>
    /// Determines if an exception is due to a network failure, including 
    /// standard, non-standard and specific exceptions like NoNetworkException.
    /// </summary>
    /// <param name="exception">The exception to evaluate.</param>
    /// <returns>True if the exception is network related.</returns>
		public static bool IsNetworkFailureException(Exception exception) {
			return exception is NoNetworkException || IsNonStandardNetworkFailureException(exception);
		}

    /// <summary>
    /// Combines a list of path segments into a single path string using the specified separator.
    /// </summary>
    /// <param name="pathSegments">List of individual path segments to combine.</param>
    /// <param name="separator">Character separator to use for combining paths.</param>
    /// <returns>The combined path as a string.</returns>
		public static string CombinePaths(List<string> pathSegments, char separator = '/')
		{
			string combinedPath = Path.Combine(pathSegments.ToArray());
			combinedPath = combinedPath.Replace(Path.DirectorySeparatorChar, separator);
			return combinedPath;
		}
		
    /// <summary>
    /// Calculates the relative path from a base path to an absolute path.
    /// Throws an exception if the paths do not share a common root.
    /// </summary>
    /// <param name="basePath">The base path to calculate the relative path from.</param>
    /// <param name="absolutePath">The absolute path to which the relative path is calculated.</param>
    /// <returns>The relative path from the base path to the absolute path.</returns>
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

    /// <summary>
    /// Returns the path up to and including the named folder within a given path.
    /// If the folder is not found, returns null.
    /// </summary>
    /// <param name="path">The full path in which to search for the folder.</param>
    /// <param name="folderName">The name of the folder to find.</param>
    /// <returns>The path up to and including the specified folder, or null if not found.</returns>
		public static string? UpToFolderNamed(string path, string folderName) {
			var parts = path.Split('/');
			var folderIndex = Array.LastIndexOf(parts, folderName);
			if (folderIndex < 0)
				return null;
			var result = string.Join("/", parts.Take(folderIndex + 1));
			return result;
		}

    /// <summary>
    /// Returns the path above a named folder within a given path.
    /// If the folder is not found, returns null.
    /// </summary>
    /// <param name="path">The full path in which to search for the folder.</param>
    /// <param name="folderName">The name of the folder to find.</param>
    /// <returns>The path above the specified folder, or null if not found.</returns>
		public static string? AboveFolderNamed(string path, string folderName) {
			var path2 = UpToFolderNamed(path, folderName);
			if (path2 == null)
				return null;
			return Path.GetDirectoryName(path2);
		}

    // run a series of tasks with the given limit of parallel threads 
		public static async Task<Out[]> ProcessParallel<In,Out>(IEnumerable<In> items, int maxDegreeOfParallelism, Func<In, Task<Out>> process) {
			var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
			var tasks = new List<Task<Out>>();
			
			foreach (var item in items) {
				await semaphore.WaitAsync();

				tasks.Add(Task.Run(async () => {
					try {
						return await process(item);
					}
					finally {
						semaphore.Release();
					}
				}));
			}
			var results = await Task.WhenAll(tasks);
			return results;
		}
	}
}

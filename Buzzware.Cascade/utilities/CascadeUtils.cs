using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
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

		public static byte[] BytesFromStream(Stream stream) {
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			var beforePosition = stream.CanSeek ? stream.Position : -1;
			
			// Fast path 1: MemoryStream - use built-in methods
			if (stream is MemoryStream memoryStream) {
				// If we're at the start, ToArray() is optimal
				if (memoryStream.Position == 0) {
					var array = memoryStream.ToArray();
					if (beforePosition >= 0)
						memoryStream.Position = beforePosition;
					return array;
				}

				// If not at start, read remaining bytes from current position
				long remaining = memoryStream.Length - memoryStream.Position;
				if (remaining == 0) {
					if (beforePosition >= 0)
						memoryStream.Position = beforePosition;
					return Array.Empty<byte>();
				}

				byte[] buffer = new byte[remaining];
				int bytesRead = memoryStream.Read(buffer, 0, buffer.Length);

				// Handle case where Read didn't return all bytes (shouldn't happen with MemoryStream)
				if (bytesRead != buffer.Length) {
					var array = buffer.AsSpan(0, bytesRead).ToArray();
					if (beforePosition >= 0)
						memoryStream.Position = beforePosition;
					return array;
				}
				if (beforePosition >= 0)
					memoryStream.Position = beforePosition;
				return buffer;
			}

			// Fast path 2: FileStream or other seekable streams with known length
			if (stream.CanSeek && stream.Length >= 0) {
				long remaining = stream.Length - stream.Position;
				if (remaining == 0)
					return Array.Empty<byte>();

				if (remaining > int.MaxValue)
					throw new IOException($"Stream is too large ({remaining} bytes). Maximum supported size is {int.MaxValue} bytes.");

				byte[] buffer = new byte[remaining];

#if NET7_0_OR_GREATER
        // .NET 7+ has ReadExactly which is more efficient
        stream.ReadExactly(buffer, 0, buffer.Length);
#else
				// Read in loop to ensure we get all bytes
				int totalRead = 0;
				while (totalRead < buffer.Length) {
					int bytesRead = stream.Read(buffer, totalRead, buffer.Length - totalRead);
					if (bytesRead == 0)
						throw new EndOfStreamException($"Stream ended after reading {totalRead} of {buffer.Length} bytes.");

					totalRead += bytesRead;
				}
#endif

				return buffer;
			}

			// Slow path: Non-seekable stream or unknown length
			// Copy to MemoryStream then extract bytes
			using (var ms = new MemoryStream()) {
				stream.CopyTo(ms);
				return ms.ToArray();
			}
		}    
    
		public static async Task<byte[]> BytesFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			// MemoryStream - synchronous is fine, it's in-memory
			if (stream is MemoryStream memoryStream)
			{
				if (memoryStream.Position == 0)
					return memoryStream.ToArray();
        
				long remaining = memoryStream.Length - memoryStream.Position;
				if (remaining == 0)
					return Array.Empty<byte>();
        
				byte[] buffer = new byte[remaining];
				await memoryStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
				return buffer;
			}

			// Seekable streams
			if (stream.CanSeek && stream.Length >= 0)
			{
				long remaining = stream.Length - stream.Position;
				if (remaining == 0)
					return Array.Empty<byte>();
        
				if (remaining > int.MaxValue)
					throw new IOException($"Stream is too large ({remaining} bytes).");
        
				byte[] buffer = new byte[remaining];
        
#if NET7_0_OR_GREATER
        await stream.ReadExactlyAsync(buffer, 0, buffer.Length, cancellationToken);
#else
				int totalRead = 0;
				while (totalRead < buffer.Length)
				{
					int bytesRead = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, cancellationToken);
					if (bytesRead == 0)
						throw new EndOfStreamException();
            
					totalRead += bytesRead;
				}
#endif
        
				return buffer;
			}

			// Non-seekable stream
			using (var ms = new MemoryStream())
			{
				await stream.CopyToAsync(ms, 81920, cancellationToken); // 80KB buffer
				return ms.ToArray();
			}
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
    
    
		public static async Task<Stream> StreamToFileAndNewStream(Stream sourceStream, string filePath) {
			try {
				// 1. Create the file and copy the source data into it
				using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
					await sourceStream.CopyToAsync(fileStream);
				}
				// 2. Return a new stream for reading. 
				// FileShare.Read allows others to read, but not write/delete while this stream is open.
				return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			} catch (Exception) {
				// 3. Robust Cleanup: If anything failed above, delete the partial/locked file
				if (File.Exists(filePath)) {
					try { 
						File.Delete(filePath); 
					} catch { 
						// We swallow exceptions during deletion to ensure the 
						// original I/O exception is what the caller sees.
					}
				}
				// Re-throw so the exception escapes the function as requested
				throw;
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
		
    /// <summary>
    /// Escapes a string so that it is safe for use both within a URL and as a
    /// unix or windows filename. Unsafe characters are URL-style percent-encoded
    /// (%XX of their UTF-8 bytes, uppercase hex).
    ///
    /// A character is considered safe only if it is alphanumeric, '-' or '.'.
    /// Every other character is escaped, which covers all characters considered
    /// unsafe in a URL or on the unix or windows filesystem.
    ///
    /// Underscores ('_') are kept unescaped except when they are leading, trailing
    /// or consecutive, in which case they are also escaped (as %5F).
    /// </summary>
    /// <param name="value">The string to escape.</param>
    /// <returns>The escaped string.</returns>
    public static string SafeKeyEncode(string value) {
      if (value == null)
        throw new ArgumentNullException(nameof(value));

      var sb = new System.Text.StringBuilder(value.Length * 2);
      for (int i = 0; i < value.Length; i++) {
        char c = value[i];

        bool isSafe = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '-' || c == '.';
        if (isSafe) {
          sb.Append(c);
          continue;
        }

        if (c == '_') {
          bool leading = i == 0;
          bool trailing = i == value.Length - 1;
          bool consecutive = (i > 0 && value[i - 1] == '_') ||
                             (i < value.Length - 1 && value[i + 1] == '_');
          if (!leading && !trailing && !consecutive) {
            sb.Append('_');
            continue;
          }
          // otherwise fall through and escape the underscore
        }

        // Percent-encode the UTF-8 bytes of this character (handling surrogate pairs).
        string s;
        if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) {
          s = value.Substring(i, 2);
          i++;
        } else {
          s = c.ToString();
        }
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(s)) {
          sb.Append('%');
          sb.Append(b.ToString("X2"));
        }
      }
      return sb.ToString();
    }

    /// <summary>
    /// Decodes a string previously encoded with <see cref="SafeKeyEncode"/>,
    /// reversing the URL-style percent-encoding. Each "%XX" sequence is converted
    /// back to its byte value, and the resulting bytes are decoded as UTF-8.
    /// Characters that were not encoded are passed through unchanged.
    /// </summary>
    /// <param name="value">The string to decode.</param>
    /// <returns>The decoded string.</returns>
    public static string SafeKeyDecode(string value) {
      if (value == null)
        throw new ArgumentNullException(nameof(value));

      var bytes = new List<byte>(value.Length);
      for (int i = 0; i < value.Length; i++) {
        char c = value[i];
        if (c == '%' && i + 2 < value.Length &&
            IsHexDigit(value[i + 1]) && IsHexDigit(value[i + 2])) {
          bytes.Add((byte)((HexValue(value[i + 1]) << 4) | HexValue(value[i + 2])));
          i += 2;
        } else {
          // Pass unencoded characters through as their UTF-8 bytes.
          foreach (var b in System.Text.Encoding.UTF8.GetBytes(c.ToString()))
            bytes.Add(b);
        }
      }
      return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static bool IsHexDigit(char c) {
      return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }

    private static int HexValue(char c) {
      if (c >= '0' && c <= '9') return c - '0';
      if (c >= 'A' && c <= 'F') return c - 'A' + 10;
      return c - 'a' + 10;
    }

		public static async Task ProcessParallel<In>(IReadOnlyList<In> items, int maxDegreeOfParallelism, Func<In, Task> process) {
			var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
			var tasks = new List<Task>();

			foreach (var item in items) {
				await semaphore.WaitAsync();

				tasks.Add(Task.Run(async () => {
					try {
						await process(item);
					}
					finally {
						semaphore.Release();
					}
				}));
			}
			await Task.WhenAll(tasks);
		}

    /// <summary>
    /// Like ProcessParallel, but with a per item timeout and fail fast behaviour - the first
    /// failure stops new items from starting, cancels in-flight items via their token, and its
    /// exception is rethrown to the caller once the in-flight items have finished.
    /// The per item timeout is hard: a timed out item has its token cancelled, surfaces as
    /// TimeoutException and is abandoned, so a process that never observes its token cannot
    /// hang the loop. If outerToken is cancelled, OperationCanceledException is thrown in
    /// preference to any concurrent item failure (matching Parallel.ForEachAsync semantics).
    /// This code effectively implements Parallel.ForEachAsync which isn't available to this dotnetstandard 2.0 library
    /// </summary>
    /// <typeparam name="In">The type of the items to process.</typeparam>
    /// <param name="items">The items to process.</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of items processed concurrently.</param>
    /// <param name="process">The operation to run for each item. It should observe the given token.</param>
    /// <param name="perItemTimeout">Optional hard timeout applied to each item individually.</param>
    /// <param name="outerToken">Optional token cancelling the whole operation.</param>
		public static async Task ProcessParallelFailFast<In>(
			IReadOnlyList<In> items,
			int maxDegreeOfParallelism,
			Func<In, CancellationToken, Task> process,
			TimeSpan perItemTimeout = default,
			CancellationToken outerToken = default
		) {
			if (items == null)
				throw new ArgumentNullException(nameof(items));
			if (process == null)
				throw new ArgumentNullException(nameof(process));
			if (maxDegreeOfParallelism < 1)
				throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
			outerToken.ThrowIfCancellationRequested();
			if (items.Count == 0)
				return;

			Exception? firstException = null;			// first failure of any kind, in order of occurrence
			Exception? firstRealException = null;	// first failure that is not a cancellation ripple
			var recordLock = new object();
			var failFastCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
			var throttle = new SemaphoreSlim(maxDegreeOfParallelism);
			try {
				var ct = failFastCts.Token;		// outerToken + fail fast

				// Record before cancelling, so the root cause is always recorded ahead of the
				// OperationCanceledExceptions that cancellation then ripples through other items.
				void RecordFailure(Exception e) {
					lock (recordLock) {
						if (firstException == null)
							firstException = e;
						if (firstRealException == null && !(e is OperationCanceledException))
							firstRealException = e;
					}
					try {
						failFastCts.Cancel();
					} catch (AggregateException) {
						// a throwing cancellation callback must not obscure the recorded failure
					}
				}

				// Runs one item. Never throws - failures are recorded so that WhenAll below cannot
				// throw an arbitrarily ordered exception; the throttle is always released exactly once.
				async Task RunItem(In item) {
					try {
						var itemCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
						var disposeItemCts = true;
						try {
							var itemTask = process(item, itemCts.Token);
							if (perItemTimeout > TimeSpan.Zero) {
								var timerCts = new CancellationTokenSource();
								try {
									var timer = Task.Delay(perItemTimeout, timerCts.Token);
									var completed = await Task.WhenAny(itemTask, timer).ConfigureAwait(false);
									if (completed == timer && !itemTask.IsCompleted) {
										itemCts.Cancel();				// signal the abandoned process to stop
										disposeItemCts = false;	// it may still be using the token; the link is freed with failFastCts
										ObserveAbandoned(itemTask);
										throw new TimeoutException($"Operation exceeded {perItemTimeout.TotalSeconds:0.#}s and was abandoned.");
									}
									timerCts.Cancel();
								} finally {
									timerCts.Dispose();
								}
							}
							await itemTask.ConfigureAwait(false);
						} finally {
							if (disposeItemCts)
								itemCts.Dispose();
						}
					} catch (Exception e) {
						RecordFailure(e);
					} finally {
						throttle.Release();
					}
				}

				var tasks = new List<Task>(items.Count);
				foreach (var item in items) {
					try {
						await throttle.WaitAsync(ct).ConfigureAwait(false);
					} catch (OperationCanceledException) {
						break;	// fail fast - stop starting new items; started ones are awaited below
					}
					if (ct.IsCancellationRequested) {	// check again now that a slot is held
						throttle.Release();
						break;
					}
					tasks.Add(Task.Run(() => RunItem(item)));
				}

				// Item tasks record failures rather than throwing, so this returns once every started
				// item has completed or been abandoned by its timeout - it cannot hang or throw.
				await Task.WhenAll(tasks).ConfigureAwait(false);

				// external cancellation takes precedence over item failures
				outerToken.ThrowIfCancellationRequested();

				var failure = firstRealException ?? firstException;
				if (failure != null)
					ExceptionDispatchInfo.Capture(failure).Throw();
			} finally {
				failFastCts.Dispose();
				throttle.Dispose();
			}
		}

    /// <summary>
    /// Observes the eventual fault of an abandoned task so it cannot surface as an
    /// UnobservedTaskException, logging it for diagnostics.
    /// </summary>
		private static void ObserveAbandoned(Task task) {
			task.ContinueWith(
				t => Log.Debug(t.Exception, "ProcessParallelFailFast: abandoned item later faulted"),
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);
		}

		public static bool HasArrivedAtExpired(long nowMs, long fallbackSeconds, long arrivedAtMs) {
			if (fallbackSeconds==RequestOp.FALLBACK_NEVER)	// always expired
				return true;
			if (fallbackSeconds == RequestOp.FALLBACK_ANY)	// never expires
				return false;
			return arrivedAtMs < (nowMs-fallbackSeconds*1000L);
		}

		public static FreshnessState CalcFreshnessState(long nowMs, long? arrivedAtMs, long freshnessSeconds, long fallbackSeconds) {
			if (!arrivedAtMs.HasValue)
				return FreshnessState.Absent;
			var freshAfterMs = nowMs - freshnessSeconds * 1000L;
			var withinFreshness = freshnessSeconds == RequestOp.FRESHNESS_ANY || (freshnessSeconds > RequestOp.FRESHNESS_FRESHEST && arrivedAtMs >= freshAfterMs); 
			if (withinFreshness)
				return FreshnessState.Fresh;
			var fallbackAfterMs = nowMs - fallbackSeconds * 1000L;
			var withinFallback = fallbackSeconds == RequestOp.FALLBACK_ANY || (fallbackSeconds > RequestOp.FALLBACK_NEVER && arrivedAtMs >= fallbackAfterMs);
			return withinFallback ? FreshnessState.Stale : FreshnessState.Expired;
		}

		public static FreshnessState? FreshnessStateMax(FreshnessState? state1, FreshnessState? state2) {
			if (state1 == null || state2 == null) {
				if (state1 == null)
					return state2;
				else
					return state1;
			} else {
				if (state1==state2)
					return state1;
				else if (state1>state2)
					return state1;
				else
					return state2;
			}
		}
		
		public static FreshnessState FreshnessStateMax(FreshnessState state1, FreshnessState state2) {
			if (state1==state2)
				return state1;
			else if (state1>state2)
				return state1;
			else
				return state2;
		}

		public static string HashDictionaryStable(IDictionary<string, object> mainListCriteria) {
			var ordered =  new SortedDictionary<string, object>(mainListCriteria, StringComparer.Ordinal);  
			var str = JsonSerializer.Serialize(ordered);
			return StringHexHash(str);
		}

		public static string StringHexHash(string str)
		{
			var hash = (long)FNVHash.GetHash(str, 64);
			var result = hash.ToString("X");
			return result;
		}
	}
}

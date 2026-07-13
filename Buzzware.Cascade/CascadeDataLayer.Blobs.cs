using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade {

	/// <summary>
	/// Functionality for retrieving, storing, and deleting binary blobs identified by a unique path.
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Retrieve a binary blob identified by the given path with optional caching and freshness parameters.
		/// </summary>
		/// <param name="path">Identifier for the blob</param>
		/// <param name="freshnessSeconds">Desired freshness duration in seconds for the cache</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness duration if primary freshness cannot be achieved</param>
		/// <param name="hold">Indicates if the blob and its associations should be held in cache</param>
		/// <param name="sequenceBeganMs">The request timestamp in milliseconds since epoch, for caching optimization</param>
		/// <returns>The blob data as a byte array or null if not found</returns>
		public async Task<byte[]?> BlobGet(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			//return (byte[]?)(await this.BlobGetResponse(path, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs)).Result;
			var response = await this.BlobGetResponse(path, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
			if (response.Result==null || response.Result is byte[])
				return response.Result as byte[];
			if (response.Result is Stream stream) {
				using (stream) {
					using var ms = new MemoryStream();
					await stream.CopyToAsync(ms);
					return ms.ToArray();
				}
			}
			throw new UnknownException("Invalid Result type: " + response.Result.GetType().FullName);					
		}

		/// <summary>
		/// Retrieve a binary blob identified by the given path as a Stream, with optional caching and freshness parameters.
		/// </summary>
		/// <param name="path">Identifier for the blob</param>
		/// <param name="freshnessSeconds">Desired freshness duration in seconds for the cache</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness duration if primary freshness cannot be achieved</param>
		/// <param name="hold">Indicates if the blob and its associations should be held in cache</param>
		/// <param name="sequenceBeganMs">The request timestamp in milliseconds since epoch, for caching optimization</param>
		/// <returns>The blob data as a Stream or null if not found</returns>
		public async Task<Stream?> BlobGetStream(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			var response = await this.BlobGetResponse(path, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
			if (response.Result is byte[] bytes)
				return new MemoryStream(bytes);
			return response.Result as Stream;
		}
		
		/// <summary>
		/// Retrieves a response containing a binary blob from the data layer based on the specified path.
		/// </summary>
		/// <param name="path">Path of the blob to be retrieved</param>
		/// <param name="freshnessSeconds">Desired freshness of the blob in seconds</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness in case the primary cannot be achieved</param>
		/// <param name="hold">Flag to hold the blob in cache for offline availability</param>
		/// <param name="sequenceBeganMs">Request timestamp in milliseconds since epoch</param>
		/// <returns>A task representing the operation, with OpResponse as the result</returns>
		public Task<OpResponse> BlobGetResponse(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			// Create a request operation for retrieving a binary blob
			var req = RequestOp.BlobGetOp(
				path,
				sequenceBeganMs ?? NowMs,
				Config.GetBlobFreshnessSeconds(freshnessSeconds),
				fallbackFreshnessSeconds ?? Config.BlobFallbackFreshnessSeconds,
				hold
			);

			// Process the request and return its response
			return ProcessRequest(req);
		}
		
		/// <summary>
		/// Retrieve a binary blob identified by the given path as an absolute file path, with optional caching and freshness parameters.
		/// </summary>
		/// <param name="path">Identifier for the blob</param>
		/// <param name="freshnessSeconds">Desired freshness duration in seconds for the cache</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness duration if primary freshness cannot be achieved</param>
		/// <param name="hold">Indicates if the blob and its associations should be held in cache</param>
		/// <param name="sequenceBeganMs">The request timestamp in milliseconds since epoch, for caching optimization</param>
		/// <returns>An absolute file path to the blob file, or null if not found</returns>
		public async Task<string?> BlobGetFilePath(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			OpResponse opResponse = await this.BlobGetFilePathResponse(path, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
			return opResponse.Result as string;
		}

		/// <summary>
		/// Download a binary blob identified by the given path to a local cache. Does not return the data. Use BlobGet/BlobGetStream to get the data.
		/// </summary>
		/// <param name="path">Identifier for the blob</param>
		/// <param name="freshnessSeconds">Desired freshness duration in seconds for the cache</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness duration if primary freshness cannot be achieved</param>
		/// <param name="hold">Indicates if the blob and its associations should be held in cache</param>
		/// <param name="sequenceBeganMs">The request timestamp in milliseconds since epoch, for caching optimization</param>
		public async Task BlobDownload(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			// currently syntactic sugar for BlobGetFilePath but one day might work even when there is no cache that can provide an absolute path
			await BlobGetFilePath(path, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
		}
		
		/// <summary>
		/// Test whether blob identified by the given path as an absolute file path exists, with optional caching and freshness parameters.
		/// For now this uses BlobGetFilePath which means it will download the file if it exists, but in future it should be optimised to only detect existence on the origin
		/// </summary>
		/// <param name="path">Identifier for the blob</param>
		/// <param name="freshnessSeconds">Desired freshness duration in seconds for the cache</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness duration if primary freshness cannot be achieved</param>
		/// <param name="hold">Indicates if the blob and its associations should be held in cache</param>
		/// <param name="sequenceBeganMs">The request timestamp in milliseconds since epoch, for caching optimization</param>
		/// <returns>An absolute file path to the blob file, or null if not found</returns>
		public async Task<bool> BlobExists(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			return await BlobGetFilePath(
				path, 
				freshnessSeconds, 
				fallbackFreshnessSeconds, 
				hold, 
				sequenceBeganMs
			) != null;
		}
		
		/// <summary>
		/// Retrieves a response containing a binary blob from the data layer based on the specified path.
		/// </summary>
		/// <param name="path">Path of the blob to be retrieved</param>
		/// <param name="freshnessSeconds">Desired freshness of the blob in seconds</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness in case the primary cannot be achieved</param>
		/// <param name="hold">Flag to hold the blob in cache for offline availability</param>
		/// <param name="sequenceBeganMs">Request timestamp in milliseconds since epoch</param>
		/// <returns>A task representing the operation, with OpResponse as the result</returns>
		public Task<OpResponse> BlobGetFilePathResponse(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			// Create a request operation for retrieving a binary blob
			var req = RequestOp.BlobGetFilePathOp(
				path,
				sequenceBeganMs ?? NowMs,
				Config.GetBlobFreshnessSeconds(freshnessSeconds),
				fallbackFreshnessSeconds ?? Config.BlobFallbackFreshnessSeconds,
				hold
			);

			// Process the request and return its response
			return ProcessRequest(req);
		}

		/// <summary>
		/// Store a binary blob at the specified path.
		/// </summary>
		/// <param name="path">Path where the blob will be stored</param>
		/// <param name="data">The binary data to be stored</param>
		public async Task BlobPut(
			string path, 
			byte[] data
		) {
			// Obtain and process the response for putting a binary blob
			var response = await BlobPutResponse(path,data);
		}		

		/// <summary>
		/// Store a binary blob at the specified path, returning the OpResponse
		/// </summary>
		/// <param name="path">Path to store the blob data</param>
		/// <param name="data">Binary data to be stored</param>
		/// <returns>Operation response with details of the put operation</returns>
		public Task<OpResponse> BlobPutResponse(string path, byte[] data) {
			// Create a request operation for storing a binary blob
			var req = RequestOp.BlobPutOp(path, NowMs, data);
			
			// Process the request and return its response
			return ProcessRequest(req);
		}
		
		/// <summary>
		/// Store a binary blob stream at the specified path.
		/// One day this could be made more efficient for large blobs.
		/// </summary>
		/// <param name="path">Path where the blob will be stored</param>
		/// <param name="data">The binary data to be stored</param>
		public async Task BlobPut(
			string path, 
			Stream stream
		) {
			// Obtain and process the response for putting a binary blob
			var response = await BlobPutResponse(path,stream);
		}		
		
		/// <summary>
		/// Store a binary blob at the specified path, returning the OpResponse
		/// </summary>
		/// <param name="path">Path to store the blob stream</param>
		/// <param name="stream">Binary stream to be stored</param>
		/// <returns>Operation response with details of the put operation</returns>
		public Task<OpResponse> BlobPutResponse(string path, Stream stream) {
			// Create a request operation for storing a binary blob
			var req = RequestOp.BlobPutOp(path, NowMs, stream);
			
			// Process the request and return its response
			return ProcessRequest(req);
		}
		
		/// <summary>
		/// Removes a binary blob from the specified path, effectively deleting it.
		/// </summary>
		/// <param name="path">Path identifying the blob to be deleted</param>
		public async Task BlobDestroy(string path) {
			// Obtain the response and process the request for blob destruction
			var response = await BlobDestroyResponse(path);
		}
		
		/// <summary>
		/// Generate an operation response for destroying a binary blob at the specified path.
		/// </summary>
		/// <param name="path">Path of the blob to be destroyed</param>
		/// <returns>Operation response for the destroy operation</returns>
		public Task<OpResponse> BlobDestroyResponse(string path) {
			// Create a request operation for destroying a binary blob
			var req = RequestOp.BlobDestroyOp(path, NowMs);
			
			// Process the request and return its response
			return ProcessRequest(req);
		}

		/// <summary>
		/// Sets the property of the given model from a binary blob using the specified property's conversion attribute.
		/// </summary>
		/// <param name="model">The model instance where the property will be set</param>
		/// <param name="property">Name of the property to set</param>
		/// <param name="blob">Binary data to be converted and assigned to the property</param>
		/// <returns>The converted property value</returns>
		public async Task<object?> SetFromBlobProperty(SuperModel model, string property, byte[] blob) {
			// Retrieve the property information and convert the blob using its attribute
			CascadePropertyInfo propertyInfo = FastReflection.GetPropertyInfo(model.GetType(),property)!;
			var attribute = propertyInfo.KindAttribute as FromBlobAttribute;
			if (attribute==null)
				throw new ArgumentException("missing FromBlobAttribute");
			var propertyValue = attribute.ConvertToPropertyType(blob, propertyInfo.NotNullType);
			// Set the property of the model to the converted value
			await SetModelProperty(model, propertyInfo, propertyValue);
			return propertyValue;
		}
		
		/// <summary>
		/// Removes a binary blob of the specified path from the cache (if exists) without affecting the origin
		/// </summary>
		/// <param name="blobPath">Path identifying the blob to be cleared from the cache</param>
		public async Task BlobClear(string blobPath) {
			foreach (var layer in CacheLayers) {
				if (!layer.SupportsBlobs)
					continue;
				await layer.ClearBlob(blobPath);
			}
		}

		public async Task ClearBlobs(bool exceptHeld = true, DateTime? olderThan = null) {
			foreach (var layer in CacheLayers) {
				await layer.ClearBlobs(exceptHeld, olderThan);
			}
		}		
	}
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Get a binary blob identified by the given path
		/// </summary>
		/// <param name="path">id of blob</param>
		/// <param name="freshnessSeconds">freshness</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <returns>model of type M or null</returns>
		public async Task<byte[]?> BlobGet(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) {
			return (byte[]?)(await this.BlobGetResponse(path,freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs)).Result;
		}


		/// <summary>
		/// </summary>
		/// <param name="path">path of blob to get</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="fallbackFreshnessSeconds"></param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <param name="timeMs"></param>
		/// <returns>OpResponse</returns>
		public Task<OpResponse> BlobGetResponse(
			string path,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null,
			long? timeMs = null
		) {
			var req = RequestOp.BlobGetOp(
				path,
				timeMs ?? NowMs,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				fallbackFreshnessSeconds ?? Config.DefaultFallbackFreshnessSeconds,
				hold
			);
			return ProcessRequest(req);
		}
		
		public async Task BlobPut(
			string path, 
			byte[] data
		) {
			var response = await BlobPutResponse(path,data);
		}		
		
		public Task<OpResponse> BlobPutResponse(string path, byte[] data) {
			var req = RequestOp.BlobPutOp(path, NowMs, data);
			return ProcessRequest(req);
		}
		
		public async Task BlobDestroy(string path) {
			var response = await BlobDestroyResponse(path);
		}
		
		public Task<OpResponse> BlobDestroyResponse(string path) {
			var req = RequestOp.BlobDestroyOp(path, NowMs);
			return ProcessRequest(req);
		}

		public async Task<object?> SetFromBlobProperty(SuperModel model, string property, byte[] blob) {
			PropertyInfo propertyInfo = model.GetType().GetProperty(property)!;
			var attribute = propertyInfo.GetCustomAttribute<FromBlobAttribute>();
			var destinationPropertyType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
			var propertyValue = attribute.ConvertToPropertyType(blob, destinationPropertyType);
			await SetModelProperty(model, propertyInfo, propertyValue);
			return propertyValue;
		}
		
	}
}

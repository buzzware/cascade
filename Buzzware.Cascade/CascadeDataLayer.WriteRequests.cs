using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Create and return a model of the given type. An instance is used to pass in the values, and a newly created
		/// instance is returned from the origin.
		/// Note: the populate option will be removed from all write methods. Instead Create should be called followed by
		/// any call(s) to Populate() as required. 
		/// </summary>
		/// <param name="model"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>model of type M</returns>
		public async Task<M> Create<M>(M model, bool hold = false) {
			var response = await CreateResponse<M>(model,hold: hold);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}
		
		/// <summary>
		/// Create and return a model of the given type. An instance is used to pass in the values, and a newly created
		/// instance is returned from the origin.
		/// Note: the populate option will be removed from all write methods. Instead Create should be called followed by
		/// any call(s) to Populate() as required. 
		/// </summary>
		/// <param name="model"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>OpResponse with full detail of operation, including Result of type M</returns>
		public Task<OpResponse> CreateResponse<M>(M model, bool hold = false) {
			var req = RequestOp.CreateOp(
				model!,
				NowMs,
				hold: hold
			);
			return ProcessRequest(req);
		}

		public async Task<M> Replace<M>(M model) {
			var response = await ReplaceResponse<M>(model);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}

		public Task<OpResponse> ReplaceResponse<M>(M model) {
			var req = RequestOp.ReplaceOp(
				model!,
				NowMs
			);
			return ProcessRequest(req);
		}

		// may return null if the record no longer exists
		public async Task<M?> Update<M>(M model, IDictionary<string, object?> changes) where M : class {
			var response = await UpdateResponse<M>(model, changes);
			if (response.Result != null && response.Result is not M)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return response.Result as M;
		}
		
		public Task<OpResponse> UpdateResponse<M>(M model, IDictionary<string, object?> changes) {
			var req = RequestOp.UpdateOp(
				model!,
				changes,
				NowMs
			);
			return ProcessRequest(req);
		}
		
		public async Task Destroy<M>(M model) {
			var response = await DestroyResponse<M>(model);
		}

		public Task<OpResponse> DestroyResponse<M>(M model) {
			var req = RequestOp.DestroyOp<M>(
				model,
				NowMs
			);
			return ProcessRequest(req);
		}

		// ModelType : what model type are you executing the action on? Useful when implementing the action on the origin
		// ReturnType : the type you will be returning - eg. same as ModelType or IEnumerable<ModelType> or anything else
		public async Task<ReturnType> Execute<ModelType, ReturnType>(string action, IDictionary<string, object?> parameters) {
			var response = await ExecuteResponse<ModelType, ReturnType>(
				action,
				parameters
			);
			if (response.Result is not ReturnType result)
				throw new AssumptionException($"Should be of type {typeof(ReturnType).Name}");
			return (ReturnType)response.Result;
		}

		public Task<OpResponse> ExecuteResponse<ModelType, ReturnType>(string action, IDictionary<string, object?> parameters) {
			var req = RequestOp.ExecuteOp<ModelType, ReturnType>(
				action,
				parameters,
				NowMs
			);
			return ProcessRequest(req);
		}

	}
}

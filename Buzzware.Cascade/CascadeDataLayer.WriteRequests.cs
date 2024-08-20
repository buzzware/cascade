using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {
  
  /// <summary>
  /// Provides methods used by applications for creating, updating, replacing, and deleting models, as well as executing actions
  /// </summary>
  public partial class CascadeDataLayer {

    /// <summary>
    /// Create and return a model of the given type. An instance is used to pass in the values, and a newly created
    /// instance is returned from the origin.
    /// Note: the populate option will be removed from all write methods. Instead Create should be called followed by
    /// any call(s) to Populate() as required. 
    /// </summary>
    /// <typeparam name="M">The type of the model to be created.</typeparam>
    /// <param name="model">The model instance containing values to create a new entry from.</param>
    /// <param name="hold">Indicates whether to hold the operation.</param>
    /// <returns>Model of type M</returns>
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
    /// <typeparam name="M">The type of the model to be created.</typeparam>
    /// <param name="model">The model instance containing values to create a new entry from.</param>
    /// <param name="hold">Indicates whether to hold the operation.</param>
    /// <returns>OpResponse with full detail of operation, including Result of type M</returns>
    public Task<OpResponse> CreateResponse<M>(M model, bool hold = false) {
      var req = RequestOp.CreateOp(
        model!,
        NowMs,
        hold: hold
      );
      return ProcessRequest(req);
    }

    /// <summary>
    /// Replaces the given model with a new instance. 
    /// </summary>
    /// <typeparam name="M">The type of the model to be replaced.</typeparam>
    /// <param name="model">The model instance to replace.</param>
    /// <returns>Model of type M after replacement.</returns>
    public async Task<M> Replace<M>(M model) {
      var response = await ReplaceResponse<M>(model);
      if (response.Result is not M result)
        throw new AssumptionException($"Should be of type {typeof(M).Name}");
      return result;
    }

    /// <summary>
    /// Generates a response for replacing the given model with a new instance.
    /// </summary>
    /// <typeparam name="M">The type of the model to be replaced.</typeparam>
    /// <param name="model">The model instance to replace.</param>
    /// <returns>OpResponse with operation details, including Result of type M.</returns>
    public Task<OpResponse> ReplaceResponse<M>(M model) {
      var req = RequestOp.ReplaceOp(
        model!,
        NowMs
      );
      return ProcessRequest(req);
    }

    /// <summary>
    /// Updates the given model with specified changes. Returns null if the record no longer exists.
    /// </summary>
    /// <typeparam name="M">The type of the model to be updated.</typeparam>
    /// <param name="model">The model instance to update.</param>
    /// <param name="changes">Dictionary containing changes to apply to the model.</param>
    /// <returns>An updated model of type M or null if not found.</returns>
    public async Task<M?> Update<M>(M model, IDictionary<string, object?> changes) where M : class {
      var response = await UpdateResponse<M>(model, changes);
      if (response.Result != null && response.Result is not M)
        throw new AssumptionException($"Should be of type {typeof(M).Name}");
      return response.Result as M;
    }
    
    /// <summary>
    /// Generates a response for updating the given model with specified changes.
    /// </summary>
    /// <typeparam name="M">The type of the model to be updated.</typeparam>
    /// <param name="model">The model instance to update.</param>
    /// <param name="changes">Dictionary containing changes to apply to the model.</param>
    /// <returns>OpResponse with operation details, including Result of type M.</returns>
    public Task<OpResponse> UpdateResponse<M>(M model, IDictionary<string, object?> changes) {
      var req = RequestOp.UpdateOp(
        model!,
        changes,
        NowMs
      );
      return ProcessRequest(req);
    }
    
    /// <summary>
    /// Permanently removes the given model.
    /// </summary>
    /// <typeparam name="M">The type of the model to be destroyed.</typeparam>
    /// <param name="model">The model instance to destroy.</param>
    public async Task Destroy<M>(M model) {
      var response = await DestroyResponse<M>(model);
    }

    /// <summary>
    /// Generates a response for destroying the given model.
    /// </summary>
    /// <typeparam name="M">The type of the model to be destroyed.</typeparam>
    /// <param name="model">The model instance to destroy.</param>
    /// <returns>OpResponse with operation details.</returns>
    public Task<OpResponse> DestroyResponse<M>(M model) {
      var req = RequestOp.DestroyOp<M>(
        model,
        NowMs
      );
      return ProcessRequest(req);
    }

    /// <summary>
    /// Executes a specified action on a model type and returns a result of the specified return type.
    /// </summary>
    /// <typeparam name="ModelType">The type of model on which the action is executed.</typeparam>
    /// <typeparam name="ReturnType">The type that will be returned after execution, could be the same or different from ModelType.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="parameters">Parameters required for executing the action.</param>
    /// <returns>Result of type ReturnType after executing the action.</returns>
    public async Task<ReturnType> Execute<ModelType, ReturnType>(string action, IDictionary<string, object?> parameters) {
      var response = await ExecuteResponse<ModelType, ReturnType>(
        action,
        parameters
      );
      if (response.Result is not ReturnType result)
        throw new AssumptionException($"Should be of type {typeof(ReturnType).Name}");
      return (ReturnType)response.Result;
    }

    /// <summary>
    /// Execute a specified action on a model type - returns a OpResponse
    /// </summary>
    /// <typeparam name="ModelType">The type of model on which the action is executed.</typeparam>
    /// <typeparam name="ReturnType">The type that will be returned after execution.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <param name="parameters">Parameters required for executing the action.</param>
    /// <returns>OpResponse with operation details, including Result of type ReturnType.</returns>
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

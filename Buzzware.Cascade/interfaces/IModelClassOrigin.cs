using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Buzzware.Cascade {

  /// <summary>
  /// Interface defining the origin for a single class
  /// </summary>
	public interface IModelClassOrigin {

    /// <summary>
    /// Queries the underlying data store using provided criteria and retrieves a collection of matching model objects.
    /// </summary>
    /// <param name="criteria">The search criteria used to filter data</param>
    /// <returns>A collection of model objects that meet the specified criteria</returns>
		Task<IEnumerable> Query(object criteria);

    /// <summary>
    /// Retrieves a model object by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the model object</param>
    /// <returns>The model object if found, otherwise null</returns>
		Task<object?> Get(object id);

    /// <summary>
    /// Creates a new model object with the provided initial values.
    /// </summary>
    /// <param name="value">The initial values for creating the new model object</param>
    /// <returns>The newly created model object</returns>
		Task<object> Create(object value);

    /// <summary>
    /// Replaces an existing model object with a new version.
    /// </summary>
    /// <param name="value">The new model object to replace the existing one</param>
    /// <returns>The replaced model object</returns>
		Task<object> Replace(object value);

    /// <summary>
    /// Updates specific fields of an existing model object.
    /// </summary>
    /// <param name="id">The unique identifier of the model object to be updated</param>
    /// <param name="changes">Dictionary of field names with their corresponding new values</param>
    /// <param name="model">The current instance of the model object being updated</param>
    /// <returns>The updated model object</returns>
		Task<object> Update(object id, IDictionary<string, object?> changes, object? model);

    /// <summary>
    /// Destroys the specified model object, removing it from the underlying data store.
    /// </summary>
    /// <param name="model">The model object to be destroyed</param>
		Task Destroy(object model);

    /// <summary>
    /// Ensures that the caller is authenticated before proceeding with any operations.
    /// </summary>
		Task EnsureAuthenticated();

    /// <summary>
    /// Clears the current user's authentication status.
    /// </summary>
		Task ClearAuthentication();

    /// <summary>
    /// Executes a specified operation on the model class, taking into account whether the connection is online.
    /// </summary>
    /// <param name="request">The operation to be executed on the model</param>
    /// <param name="connectionOnline">A boolean indicating whether the connection is online</param>
    /// <returns>The result of the executed operation</returns>
		Task<object> Execute(RequestOp request, bool connectionOnline);

    /// <summary>
    /// Reference of the parent origin
    /// </summary>
		ICascadeOrigin Origin { get; set; }
	}
}

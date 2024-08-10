using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Buzzware.Cascade {

  /// <summary>
  /// Defines a Cascade Origin, with methods to process requests,
  /// manage authentication, and interact with model types in the Cascade framework.
  /// </summary>
	public interface ICascadeOrigin {

    /// <summary>
    /// Receives a request and returns a response eg. from a server
    /// </summary>
    /// <param name="request">The request operation to be processed.</param>
    /// <param name="connectionOnline">Indicates whether the connection is currently online.</param>
    /// <returns>A task returning an OpResponse indicating the outcome of the request.</returns>
		Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline);
		
    /// <summary>
    /// CascadeDataLayer reference
    /// </summary>
		CascadeDataLayer Cascade { get; set; }
		
    /// <summary>
    /// Current time in milliseconds since 1970
    /// </summary>
		long NowMs { get; }
		
    /// <summary>
    /// Ensures that the session is authenticated, potentially for a specific type.
    /// </summary>
    /// <param name="type">The type for which authentication is being ensured. Defaults to null.</param>
    /// <returns>A task representing the authentication process.</returns>
		Task EnsureAuthenticated(Type? type=null);
		
    /// <summary>
    /// Retrieves the model type based on the provided type name.
    /// </summary>
    /// <param name="typeName">The name of the type to be looked up.</param>
    /// <returns>The Type corresponding to the provided type name.</returns>
		Type LookupModelType(string typeName);
		
    /// <summary>
    /// Generates a new GUID as a string.
    /// </summary>
    /// <returns>A string representing a new GUID.</returns>
		string NewGuid();
		
    /// <summary>
    /// Lists all the model types registered
    /// </summary>
    /// <returns>An enumerable containing all registered model types.</returns>
		IEnumerable<Type> ListModelTypes();
	}
}

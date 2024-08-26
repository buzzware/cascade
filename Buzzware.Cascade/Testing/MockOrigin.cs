using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Buzzware.Cascade.Testing {
	
  /// <summary>
  /// Represents a mock implementation of the ICascadeOrigin interface for testing purposes.
  /// Handles operations related to request processing and time manipulation in a simulated environment.
  /// </summary>
  public class MockOrigin : ICascadeOrigin {
    
    /// <summary>
    /// A function delegate for handling requests in the mock origin environment.
    /// </summary>
    private Func<MockOrigin,RequestOp,Task<OpResponse>>? HandleRequest;

    /// <summary>
    /// MockOrigin Constructor
    /// Initializes a new instance of the MockOrigin class with optional parameters for current time and request handling.
    /// </summary>
    /// <param name="nowMs">Represents the current time in milliseconds in the mock environment. Default is 1000 ms.</param>
    /// <param name="handleRequest">A function delegate for handling requests, allowing customization of request processing.</param>
    public MockOrigin(long nowMs = 1000, Func<MockOrigin,RequestOp,Task<OpResponse>>? handleRequest = null) {
      NowMs = nowMs;
      HandleRequest = handleRequest;
    }

    /// <summary>
    /// Provides access to the Cascade data layer instance associated with this origin.
    /// </summary>
    public CascadeDataLayer Cascade { get; set; } 
    
    /// <summary>
    /// Represents the current time in milliseconds within the mock environment.
    /// </summary>
    public long NowMs { get; set; }

    /// <summary>
    /// Ensures that the current session is authenticated for accessing models of a specific type.
    /// </summary>
    /// <param name="type">The Type for which authentication is required.</param>
    public async Task EnsureAuthenticated(Type? type) {
    }

    /// <summary>
    /// Resolves and returns the corresponding Type object for a given type name in the mock environment.
    /// </summary>
    /// <param name="typeName">The name of the type to be resolved.</param>
    /// <returns>The Type object that corresponds to the provided type name.</returns>
    /// <exception cref="TypeLoadException">Thrown when the specified type name cannot be found.</exception>
    public virtual Type LookupModelType(string typeName) {
      if (typeName == typeof(Thing).FullName)
        return typeof(Thing);
      else if (typeName == typeof(Parent).FullName)
        return typeof(Parent);
      else if (typeName == typeof(Child).FullName)
        return typeof(Child);
      else if (typeName == "System.Byte[]")
        return typeof(System.Byte[]);
      else {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
          var type = assembly.GetType(typeName);
          if (type != null)
            return type;
        }
        throw new TypeLoadException($"Type {typeName} not found in origin");
      }
    }

    /// <summary>
    /// Generates a new globally unique identifier (GUID) in string format.
    /// </summary>
    /// <returns>A new GUID as a string.</returns>
    public string NewGuid() {
      return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Lists the model types available in the mock environment.
    /// </summary>
    /// <returns>An enumeration of Type objects representing the available model types.</returns>
    public IEnumerable<Type> ListModelTypes() {
      return new[] { typeof(Thing), typeof(Parent), typeof(Child) };
    }

    /// <summary>
    /// Advances the current time by a specified number of milliseconds in the mock environment.
    /// </summary>
    /// <param name="incMs">The number of milliseconds to increment the current time. Default is 1000 ms.</param>
    /// <returns>The new current time in milliseconds after the increment.</returns>
    public long IncNowMs(long incMs=1000) {
      return NowMs += incMs;
    }

    /// <summary>
    /// Processes a request operation in the mock environment and determines the response.
    /// Utilizes the provided HandleRequest delegate if available, otherwise throws an exception.
    /// </summary>
    /// <param name="request">The request operation to be processed.</param>
    /// <param name="connectionOnline">Indicates whether the connection is currently online.</param>
    /// <returns>OpResponse object detailing the result of the request processing.</returns>
    /// <exception cref="NotImplementedException">Thrown if no HandleRequest delegate is provided and the method is not overridden.</exception>
    public virtual Task<OpResponse> ProcessRequest(RequestOp request, bool connectionOnline) {
      if (HandleRequest != null)
        return HandleRequest(this,request);
      throw new NotImplementedException("Attach HandleRequest or override this");
    }
  }
}
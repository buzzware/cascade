using System;
using Buzzware.StandardExceptions;

/// <summary>
/// Represents an exception thrown when data is not available offline and a server connection is required to access it.
/// Extends the StandardException class with a default message and status.
/// </summary>
public class DataNotAvailableOffline : StandardException {
  
  /// <summary>
  /// Default error message indicating the server could not be reached and data is unavailable offline.
  /// </summary>
  public new const string DefaultMessage = "The server could not be reached and the data is not available without it.";

  /// <summary>
  /// Default HTTP status code representing the error condition.
  /// </summary>
  public new const int DefaultStatus = 410;

  /// <summary>
  /// DataNotAvailableOffline Constructor
  /// </summary>
  /// <param name="aMessage">Custom error message, defaults to predefined message if null.</param>
  /// <param name="aInnerException">The inner exception that caused this exception, if any.</param>
  /// <param name="aStatus">HTTP status code for the error, defaults to 410.</param>
  public DataNotAvailableOffline(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus)
    : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
  }
} 

/// <summary>
/// Represents an exception thrown when an operation is not available offline and a server connection is required to perform it.
/// Extends the StandardException class with a default message and status.
/// </summary>
public class OperationNotAvailableOffline : StandardException {

  /// <summary>
  /// Default error message indicating the server could not be reached and the operation is unavailable offline.
  /// </summary>
  public new const string DefaultMessage = "The server could not be reached and the operation is not available without it";

  /// <summary>
  /// Default HTTP status code representing the error condition.
  /// </summary>
  public new const int DefaultStatus = 410;

  /// <summary>
  /// OperationNotAvailableOffline Constructor
  /// </summary>
  /// <param name="aMessage">Custom error message, defaults to predefined message if null.</param>
  /// <param name="aInnerException">The inner exception that caused this exception, if any.</param>
  /// <param name="aStatus">HTTP status code for the error, defaults to 410.</param>
  public OperationNotAvailableOffline(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus)
    : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
  }
}

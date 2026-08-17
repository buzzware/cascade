using System;
using Buzzware.StandardExceptions;

/// <summary>
/// Exception thrown when :
/// 1. Cascade ConnectionOnline==false and
/// 2. the requested data doesn't exist locally
/// 
/// Retrying will not succeed unless ConnectionOnline becomes true
/// </summary>
public class DataNotAvailableOffline : StandardException {
  
  /// <summary>
  /// Default error message indicating the server could not be reached and data is unavailable offline.
  /// </summary>
  public new const string DefaultMessage = "The data is not available locally and the client is in offline mode.";

  /// <summary>
  /// Default HTTP status code representing the error condition. 410 Gone is chosen here because retrying won't help - the data is not available until ConnectionOnline==true
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
/// 
/// Retrying will not succeed unless ConnectionOnline becomes true
/// </summary>
public class OperationNotAvailableOffline : StandardException {

  /// <summary>
  /// Default error message indicating the operation is not available locally and the client is in offline mode.
  /// </summary>
  public new const string DefaultMessage = "The operation is not available locally and the client is in offline mode.";

  /// <summary>
  /// Default HTTP status code representing the error condition. 410 Gone is chosen here because retrying won't help - the operation is not available until ConnectionOnline==true
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

/// <summary>
/// An exception thrown when :
/// 1. Cascade ConnectionOnline==true and
/// 2. A request to the origin failed to connect and
/// 3. the requested data doesn't exist locally or doesn't meet the fallback freshness requirement
///
/// Retrying is likely to succeed
/// </summary>
public class OriginAccessFailure : NoNetworkException {
  
  /// <summary>
  /// Default error message indicating the network or server failed temporarily.
  /// </summary>
  public new const string DefaultMessage = "The origin could not be accessed due to a network or server failure.";

  /// <summary>
  /// 504 Gateway Timeout is chosen here because it is likely a temporary network failure
  /// </summary>
  public new const int DefaultStatus = 504;

  /// <summary>
  /// OriginAccessFailure Constructor
  /// </summary>
  /// <param name="aMessage">Custom error message, defaults to predefined message if null.</param>
  /// <param name="aInnerException">The inner exception that caused this exception, if any.</param>
  /// <param name="aStatus">HTTP status code for the error, defaults to 504.</param>
  public OriginAccessFailure(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus)
    : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
  }
} 

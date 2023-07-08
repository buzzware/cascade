using System;
using StandardExceptions;

public class OfflineException : NoNetworkException {
	// public NoNetworkException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage,aInnerException,aStatus) {
	// }
}


public class DataNotAvailableOffline : StandardException {
	public new const string DefaultMessage = "The server could not be reached and the data is not available without it.";
	public new const int DefaultStatus = 410;
	public DataNotAvailableOffline(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
	}
} 

public class OperationNotAvailableOffline : StandardException {
	public new const string DefaultMessage = "The server could not be reached and the operation is not available without it";
	public new const int DefaultStatus = 410;
	public OperationNotAvailableOffline(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
	}
} 

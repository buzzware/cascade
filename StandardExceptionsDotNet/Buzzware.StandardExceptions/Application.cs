using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Buzzware.StandardExceptions {
	public class UnsuccessfulException : UnprocessableEntityException {
		public new const string DefaultMessage = "The requested operation was not successful.";
		public UnsuccessfulException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage,aInnerException,aStatus) {
		}
	}
	
	public class ValidationUnsuccessfulException : UnsuccessfulException {
		public new const string DefaultMessage = "The requested operation was not successful due to validation errors.";
		public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
		public ValidationUnsuccessfulException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage,aInnerException,aStatus) {
		}
	}
			
	public class UnknownException : StandardException {
		public new const string DefaultMessage = "An unidentifiable error occurred.";
		public UnknownException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage,aInnerException,aStatus) {
		}
	}
	
	public class UserErrorException : StandardException {
		public new const string DefaultMessage = "The request was not processed due to a syntax error.";
		public UserErrorException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = 400) : base (aMessage,aInnerException,aStatus) {
		}
	}
	
	public class NoNetworkException : HttpException {
		public new const string DefaultMessage = "The server could not be reached or did not respond. You could be out of network range.";
		public const int DefaultStatus = 410;
		public NoNetworkException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage,aInnerException,aStatus) {
		}
	}

	public class AssumptionException : StandardException {
		public new const string DefaultMessage = "An internal assumption check was found to be false.";
		public AssumptionException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage,aInnerException,aStatus) {
		}
	}
	
	public class ReentrantException : StandardException {
		public new const string DefaultMessage = "Operation cannot be re-entered until the original call has returned";
		public ReentrantException (string aMessage=DefaultMessage, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage,aInnerException,aStatus) {
		}
	}
}

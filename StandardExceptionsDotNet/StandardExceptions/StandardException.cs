using System;

namespace StandardExceptions {
	public class StandardException : Exception {

		public const string DefaultMessage = "An error occurred that could not be identified";
		public const int DefaultStatus = 500;
		
		public int Status { get; private set; }

		public object Result { get; set; } = null;
		public object Error { get; set; } = null;

		public StandardException (string aMessage=DefaultMessage, Exception aInnerException = null, int aStatus = DefaultStatus) : base (aMessage,aInnerException) {
			Status = aStatus;
		}
	}
}

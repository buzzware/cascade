using System;
using System.Collections;
using System.Collections.Generic;

namespace StandardExceptions {
	public class StandardException : Exception {

		public const string DefaultMessage = "An error occurred that could not be identified";
		public const int DefaultStatus = 500;
		
		public int Status { get; private set; }

		public object Result { get; set; } = null;
		public object Error { get; set; } = null;

		public new IDictionary<string,object> Data { get; set; }
		public override string StackTrace => InnerException?.StackTrace;

		public StandardException (string aMessage=DefaultMessage, Exception aInnerException = null, int aStatus = DefaultStatus) : base (aMessage,aInnerException) {
			Status = aStatus;
		}
	}
}

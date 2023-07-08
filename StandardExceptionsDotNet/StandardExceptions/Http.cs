using System;

namespace StandardExceptions {

	public class HttpException : StandardException {		
		public new const string DefaultMessage = "A server or network or request error occurred that could not be identified";
		public new const int DefaultStatus = 500;
		public HttpException(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}	
		
	public class BadRequestException : HttpException {
		public new const string DefaultMessage = "The request was not processed due to a syntax error in the request.";
		public new const int DefaultStatus = 400;		
		public BadRequestException(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}	
	
	public class UnauthorizedException : HttpException {
		public new const string DefaultMessage = "The request was not processed because it lacked acceptable authentication.";
		public new const int DefaultStatus = 401;		
		public UnauthorizedException (string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}
	
	//PaymentRequired	402
	
	public class ForbiddenException : HttpException {
		public new const string DefaultMessage = "The server understood the request but refuses to authorize it.";
		public new const int DefaultStatus = 403;		
		public ForbiddenException (string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}
	
	public class NotFoundException : HttpException {
		public new const string DefaultMessage = "The server did not find what was requested.";
		public new const int DefaultStatus = 404;		
		public NotFoundException (string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}

// MethodNotAllowed	405
// NotAcceptable	406
// ProxyAuthenticationRequired	407
// RequestTimeout	408
// Conflict	409
// Gone	410
// LengthRequired	411
// PreconditionFailed	412
// RequestEntityTooLarge	413
// RequestURITooLong	414
// UnsupportedMediaType	415
// RequestedRangeNotSatisfiable	416
// ExpectationFailed	417
	
	public class UnprocessableEntityException : HttpException {
		public new const string DefaultMessage = "The server understands the request but was unable to process it.";
		public new const int DefaultStatus = 422;		
		public UnprocessableEntityException(string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}
	
// Locked	423
// FailedDependency	424
// UpgradeRequired	426
	
	public class InternalServerErrorException : HttpException {
		public new const string DefaultMessage = "The server encountered an unexpected condition that prevented it from fulfilling the request.";
		public new const int DefaultStatus = 500;		
		public InternalServerErrorException (string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}
	
// NotImplemented	501
// BadGateway	502
// ServiceUnavailable 503

	public class ServiceUnavailableException : HttpException {
		public new const string DefaultMessage = "The server is temporarily down or overloaded";
		public new const int DefaultStatus = 503;		
		public ServiceUnavailableException (string aMessage=null, Exception aInnerException=null, int aStatus = DefaultStatus) : base (aMessage ?? DefaultMessage,aInnerException,aStatus) {
		}
	}
	
// GatewayTimeout	504
// HTTPVersionNotSupported	505
// InsufficientStorage	507
// NotExtended	510
	
}

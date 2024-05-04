using System;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;

namespace Cascade {
	public interface ICascadePlatform {
		Task InvokeOnMainThread(Action action, Func<Exception,Exception>? exceptionHandler = null);
		Task InvokeOnMainThreadNow(Action action, Func<Exception, Exception>? exceptionHandler = null);
		bool IsMainThread();
	}
}

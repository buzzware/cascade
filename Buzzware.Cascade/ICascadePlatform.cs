using System;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade {
	public interface ICascadePlatform {
		Task InvokeOnMainThread(Action action, Func<Exception,Exception>? exceptionHandler = null);
		Task InvokeOnMainThreadNow(Action action, Func<Exception, Exception>? exceptionHandler = null);
		bool IsMainThread();
	}
}

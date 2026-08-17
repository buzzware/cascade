using System;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;

namespace Buzzware.Cascade {
	/// <summary>
	/// Abstracts platform-specific threading concerns, especially invoking actions on the main/UI thread.
	/// </summary>
	public interface ICascadePlatform {
		/// <summary>
		/// Invokes the given action on the main/UI thread, optionally transforming any exception thrown via exceptionHandler.
		/// </summary>
		/// <param name="action">The action to invoke on the main thread.</param>
		/// <param name="exceptionHandler">Optional function given any exception thrown by the action; the exception it returns is thrown instead.</param>
		Task InvokeOnMainThread(Action action, Func<Exception,Exception>? exceptionHandler = null);
		/// <summary>
		/// Invokes the given action immediately when already on the main thread, otherwise falls back to InvokeOnMainThread.
		/// </summary>
		/// <param name="action">The action to invoke.</param>
		/// <param name="exceptionHandler">Optional function given any exception thrown by the action; the exception it returns is thrown instead.</param>
		Task InvokeOnMainThreadNow(Action action, Func<Exception, Exception>? exceptionHandler = null);
		/// <summary>
		/// Determines whether the current thread is the main/UI thread.
		/// </summary>
		/// <returns>True if the current thread is the main thread, otherwise false.</returns>
		bool IsMainThread();
	}
}

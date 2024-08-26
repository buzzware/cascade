using System;
using System.Threading.Tasks;

namespace Buzzware.Cascade.Testing {

  /// <summary>
  /// MockCascadePlatform is an implementation of the ICascadePlatform interface used for testing purposes.
  /// </summary>
  public class MockCascadePlatform : ICascadePlatform {

    /// <summary>
    /// Invokes the specified action on the main thread. If an exception occurs, it can be handled
    /// by the optional exceptionHandler function. If the exceptionHandler returns a new exception, 
    /// it is thrown instead.
    /// </summary>
    /// <param name="action">The action to be invoked on the main thread.</param>
    /// <param name="exceptionHandler">Optional function to handle any exceptions that occur during the action.</param>
    public async Task InvokeOnMainThread(Action action, Func<Exception, Exception>? exceptionHandler = null) {
      try {
        action();
      } catch(Exception e) {
        if (exceptionHandler != null) {
          var newE = exceptionHandler(e);
          if (newE != null) {
            if (newE == e)
              throw;
            else
              throw newE;
          }
        } else {
          throw;
        }
      }
    }

    /// <summary>
    /// Attempts to invoke the specified action immediately if currently on the main thread.
    /// Otherwise, it falls back to invoking the action using InvokeOnMainThread method.
    /// If an exception occurs, it can be handled by the optional exceptionHandler function.
    /// </summary>
    /// <param name="action">The action to be invoked.</param>
    /// <param name="exceptionHandler">Optional function to handle any exceptions that occur during the action.</param>
    public async Task InvokeOnMainThreadNow(Action action, Func<Exception, Exception>? exceptionHandler = null) {
      if (IsMainThread())
        action();
      else
        InvokeOnMainThread(action, exceptionHandler);
    }

    /// <summary>
    /// Determines if the current execution context is the main thread. 
    /// This mock implementation always returns true, simulating a scenario where the main thread is always active.
    /// </summary>
    /// <returns>bool indicating if the current context is the main thread (always true in this mock).</returns>
    public bool IsMainThread() {
      return true;
    }
  }
}

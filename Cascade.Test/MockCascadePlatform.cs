using System;
using System.Threading.Tasks;
using StandardExceptions;

namespace Cascade.Test {
	public class MockCascadePlatform : ICascadePlatform {
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

		public async Task InvokeOnMainThreadNow(Action action, Func<Exception, Exception>? exceptionHandler = null) {
			if (IsMainThread())
				action();
			else
				InvokeOnMainThread(action, exceptionHandler);
		}

		public bool IsMainThread() {
			return true;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cascade.RnD {
	
	public class WorkerThreadLauncher {
		private readonly Action<CancellationToken> _action;
		private readonly int _numberOfThreads;
		private readonly int _timeoutMilliseconds;
		private readonly List<Task> _tasks = new();
		private readonly List<CancellationTokenSource> _cancellationTokenSources = new();
		private readonly List<Exception> _exceptions = new();

		public WorkerThreadLauncher(
			Action<CancellationToken> action, 
			int numberOfThreads, 
			int timeoutMilliseconds
		) {
			_action = action;
			_numberOfThreads = numberOfThreads;
			_timeoutMilliseconds = timeoutMilliseconds;
		}

		public void Start() {
			for (int i = 0; i < _numberOfThreads; i++) {
				var cts = new CancellationTokenSource();
				_cancellationTokenSources.Add(cts);
				_tasks.Add(Task.Run(() => {
					try {
						_action(cts.Token);
					}
					catch (Exception ex) {
						lock (_exceptions) {
							_exceptions.Add(ex);
						}
					}
				}));
			}
		}

		public async Task WaitCompleteAsync() {
			if (await Task.WhenAny(Task.WhenAll(_tasks), Task.Delay(_timeoutMilliseconds)) == Task.Delay(_timeoutMilliseconds)) {
				for (var i = 0; i < _tasks.Count; i++) {
					var t = _tasks[i];
					if (!t.IsCompleted)
						_cancellationTokenSources[i].Cancel();
				}
			}
			// Ensure all tasks are completed
			await Task.WhenAll(_tasks);
		}

		public IEnumerable<Exception> Exceptions => _exceptions;
		public int SuccessfulTasks => _tasks.Count - _exceptions.Count;
	}
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Buzzware.Cascade.Utilities
{
	public class TimingProfiler
	{
		private readonly string _name;
		private readonly Stopwatch _stopwatch;
		private readonly List<TimeSpan> _timings;
		private int _iterations;

		public TimingProfiler(string name)
		{
			_name = name;
			_stopwatch = new Stopwatch();
			_timings = new List<TimeSpan>();
		}

		public void Start(int? iterations = null)
		{
			_iterations = iterations ?? 1;
			_stopwatch.Restart();
		}

		public void Stop()
		{
			_stopwatch.Stop();
			_timings.Add(_stopwatch.Elapsed);

			if (_timings.Count < _iterations)
				_stopwatch.Restart();
		}

		public string Report()
		{
			var min = _timings.Min(t => t.TotalMilliseconds);
			var max = _timings.Max(t => t.TotalMilliseconds);
			var avg = _timings.Average(t => t.TotalMilliseconds);
			var sd = Math.Sqrt(_timings.Select(t => Math.Pow(t.TotalMilliseconds - avg, 2)).Average());

			var report = new StringBuilder($"TimingProfiler {_name} Report: Iterations: {_timings.Count}\n");
			foreach (var timing in _timings)
				report.AppendLine($"Iteration: {timing.TotalMilliseconds:0.000} ms");
			if (_timings.Count > 1) {
				report.AppendLine($"Min: {min:0.000} ms");
				report.AppendLine($"Max: {max:0.000} ms");
				report.AppendLine($"Average: {avg:0.000} ms");
				report.AppendLine($"Standard deviation: {sd:0.000} ms");
			}
			return report.ToString();
		}
	}
}

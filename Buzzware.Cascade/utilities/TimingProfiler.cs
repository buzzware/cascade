using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Buzzware.Cascade.Utilities
{
  /// <summary>
  /// A utility class for profiling the duration of code execution blocks.
  /// It allows for tracking multiple iterations and calculating statistics such as
  /// minimum, maximum, average, and standard deviation of execution times.
  /// </summary>
  public class TimingProfiler
  {
    private readonly string _name;
    private readonly Stopwatch _stopwatch;
    private readonly List<TimeSpan> _timings;
    private int _iterations;

    /// <summary>
    /// TimingProfiler Constructor
    /// </summary>
    /// <param name="name">The name identifier for this instance of the profiler.</param>
    public TimingProfiler(string name)
    {
      _name = name;
      _stopwatch = new Stopwatch();
      _timings = new List<TimeSpan>();
    }

    /// <summary>
    /// Starts the timing profile, optionally specifying the number of iterations to track.
    /// </summary>
    /// <param name="iterations">The total number of iterations for timing. Defaults to 1 if not provided.</param>
    public void Start(int? iterations = null)
    {
      _iterations = iterations ?? 1;
      _stopwatch.Restart();
    }

    /// <summary>
    /// Stops the current timing session and records the elapsed time.
    /// Automatically restarts the stopwatch if more iterations are expected.
    /// </summary>
    public void Stop()
    {
      _stopwatch.Stop();
      _timings.Add(_stopwatch.Elapsed);

      // Restart the stopwatch if fewer timed iterations than specified iterations have been recorded
      if (_timings.Count < _iterations)
        _stopwatch.Restart();
    }

    /// <summary>
    /// Generates a report of the timing statistics, including each iteration's timing, 
    /// and computed minimum, maximum, average, and standard deviation if applicable.
    /// </summary>
    /// <returns>A formatted string containing the timing statistics report.</returns>
    public string Report()
    {
      // Calculate statistical metrics such as min, max, average, and standard deviation
      var min = _timings.Min(t => t.TotalMilliseconds);
      var max = _timings.Max(t => t.TotalMilliseconds);
      var avg = _timings.Average(t => t.TotalMilliseconds);
      var sd = Math.Sqrt(_timings.Select(t => Math.Pow(t.TotalMilliseconds - avg, 2)).Average());

      // Prepare the report
      var report = new StringBuilder($"TimingProfiler {_name} Report: Iterations: {_timings.Count}\n");
      foreach (var timing in _timings)
        report.AppendLine($"Iteration: {timing.TotalMilliseconds:0.000} ms");

      // Append statistical information if there is more than one timing iteration
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
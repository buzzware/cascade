using System;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade
{
    /// <summary>
    /// Provides methods that execute a logging action only when the corresponding Serilog log level
    /// is enabled, avoiding the cost of building log output that would be discarded.
    /// </summary>
    public static class LogIf
    {
        /// <summary>
        /// Executes the given action only when Debug level logging is enabled.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        public static void Debug(Action action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                action();
            }
        }

        /// <summary>
        /// Executes and awaits the given async function only when Debug level logging is enabled.
        /// </summary>
        /// <param name="action">The async function to execute.</param>
        public static async Task Debug(Func<Task> action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                await action();
            }
        }

        /// <summary>
        /// Executes the given action only when Verbose level logging is enabled.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        public static void Verbose(Action action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            {
                action();
            }
        }

        /// <summary>
        /// Executes and awaits the given async function only when Verbose level logging is enabled.
        /// </summary>
        /// <param name="action">The async function to execute.</param>
        public static async Task Verbose(Func<Task> action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            {
                await action();
            }
        }
    }

}

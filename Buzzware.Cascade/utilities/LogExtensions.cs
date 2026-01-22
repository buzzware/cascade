using System;
using System.Threading.Tasks;
using Serilog;

namespace Buzzware.Cascade
{
    public static class LogIf
    {
        public static void Debug(Action action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                action();
            }
        }

        public static async Task Debug(Func<Task> action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                await action();
            }
        }

        public static void Verbose(Action action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            {
                action();
            }
        }

        public static async Task Verbose(Func<Task> action)
        {
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            {
                await action();
            }
        }
    }

}

using Serilog;
using Serilog.Events;

namespace Configur.AspNetCore
{
    public static class SerilogLogger
    {
        static SerilogLogger()
        {
            Instance = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(LogEventLevel.Debug) // TODO Make configurable
                .WriteTo.File
                (
                    "configur.log",
                    rollingInterval: RollingInterval.Day
                )
              .CreateLogger();
        }

        public static ILogger Instance { get; private set; }
    }
}

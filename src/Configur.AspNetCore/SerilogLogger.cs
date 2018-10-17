using Serilog;

namespace Configur.AspNetCore
{
    public static class SerilogLogger
    {
        static SerilogLogger()
        {
            Instance = new LoggerConfiguration()
                .Enrich.FromLogContext()
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

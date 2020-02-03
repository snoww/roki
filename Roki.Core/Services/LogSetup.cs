using NLog;
using NLog.Config;
using NLog.Targets;

namespace Roki.Services
{
    public static class LogSetup
    {
        public static void SetupLogger()
        {
            var config = new LoggingConfiguration();
            
            var console = new ColoredConsoleTarget
            {
                Layout = @"${longdate}|${level:uppercase=true}|${logger:shortName=True}|${message}"
            };
            
            config.AddTarget("Console", console);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, console);
            
            LogManager.Configuration = config;
        }
    }
}
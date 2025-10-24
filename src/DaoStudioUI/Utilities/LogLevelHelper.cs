using System;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DaoStudioUI.Utilities
{
    internal static class LogLevelHelper
    {
        private const string EnvironmentVariableName = "DAOSTUDIO_LOG_LEVEL";

        /// <summary>
        /// Gets the log level from environment variable or falls back to build configuration.
        /// Environment variable should use Microsoft.Extensions.Logging.LogLevel format:
        /// Trace, Debug, Information, Warning, Error, Critical, None
        /// </summary>
        /// <returns>The Microsoft.Extensions.Logging.LogLevel to use.</returns>
        public static LogLevel GetMicrosoftLogLevel()
        {
            var configuredLevel = TryGetEnvironmentLogLevel();
            return configuredLevel ?? GetDefaultMicrosoftLogLevel();
        }

        /// <summary>
        /// Gets the log level from environment variable or falls back to build configuration.
        /// Environment variable should use Microsoft.Extensions.Logging.LogLevel format:
        /// Trace, Debug, Information, Warning, Error, Critical, None
        /// </summary>
        /// <returns>The Serilog.Events.LogEventLevel to use.</returns>
        public static Serilog.Events.LogEventLevel GetSerilogLogLevel()
        {
            var configuredLevel = TryGetEnvironmentLogLevel();
            return configuredLevel.HasValue
                ? ConvertToSerilogLevel(configuredLevel.Value)
                : GetDefaultSerilogLogLevel();
        }

        private static LogLevel? TryGetEnvironmentLogLevel()
        {
            var rawValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            rawValue = rawValue.Trim();

            if (Enum.TryParse<LogLevel>(rawValue, true, out var level))
            {
                return level;
            }

            LogInvalidLevel(rawValue);
            return null;
        }

        private static void LogInvalidLevel(string envLogLevel)
        {
            Log.Warning("Invalid log level in {EnvVar} environment variable: {EnvLogLevel}. " +
                "Valid values: Trace, Debug, Information, Warning, Error, Critical, None. Using default.", 
                EnvironmentVariableName, envLogLevel);
        }

        private static LogLevel GetDefaultMicrosoftLogLevel()
        {
#if DEBUG
            return LogLevel.Trace;
#else
            return LogLevel.Information;
#endif
        }

        private static Serilog.Events.LogEventLevel GetDefaultSerilogLogLevel()
        {
#if DEBUG
            return Serilog.Events.LogEventLevel.Verbose;
#else
            return Serilog.Events.LogEventLevel.Information;
#endif
        }

        private static Serilog.Events.LogEventLevel ConvertToSerilogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => Serilog.Events.LogEventLevel.Verbose,
                LogLevel.Debug => Serilog.Events.LogEventLevel.Debug,
                LogLevel.Information => Serilog.Events.LogEventLevel.Information,
                LogLevel.Warning => Serilog.Events.LogEventLevel.Warning,
                LogLevel.Error => Serilog.Events.LogEventLevel.Error,
                LogLevel.Critical => Serilog.Events.LogEventLevel.Fatal,
                LogLevel.None => Serilog.Events.LogEventLevel.Fatal, // Serilog has no 'None'; Fatal keeps output minimal
                _ => Serilog.Events.LogEventLevel.Information
            };
        }
    }
}

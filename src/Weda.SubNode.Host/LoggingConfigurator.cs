using Serilog;
using Serilog.Events;

namespace Weda.SubNode.Host;

/// <summary>
/// Fluent API for configuring logging
/// Example:
///   builder.AddLogging(log => log
///       .WriteToConsole()
///       .WriteToFile("logs/app.log")
///       .SetMinimumLevel(LogEventLevel.Debug));
/// </summary>
public class LoggingConfigurator
{
    private readonly LoggerConfiguration _loggerConfig;
    private LogEventLevel _minimumLevel = LogEventLevel.Information;

    public LoggingConfigurator()
    {
        _loggerConfig = new LoggerConfiguration();
    }

    /// <summary>
    /// Set the minimum log level
    /// </summary>
    /// <param name="level">Minimum log level</param>
    /// <returns>The configurator for chaining</returns>
    public LoggingConfigurator SetMinimumLevel(LogEventLevel level)
    {
        _minimumLevel = level;
        return this;
    }

    /// <summary>
    /// Write logs to console
    /// </summary>
    /// <param name="outputTemplate">Optional output template. Default: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"</param>
    /// <returns>The configurator for chaining</returns>
    public LoggingConfigurator WriteToConsole(string? outputTemplate = null)
    {
        outputTemplate ??= "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        _loggerConfig.WriteTo.Console(outputTemplate: outputTemplate);
        return this;
    }

    /// <summary>
    /// Write logs to file
    /// </summary>
    /// <param name="path">File path (e.g., "logs/app.log" or "logs/app-.log" for rolling)</param>
    /// <param name="rollingInterval">Rolling interval (default: Day)</param>
    /// <param name="retainedFileCountLimit">Number of files to retain (default: 31)</param>
    /// <param name="outputTemplate">Optional output template</param>
    /// <returns>The configurator for chaining</returns>
    public LoggingConfigurator WriteToFile(
        string path,
        RollingInterval rollingInterval = RollingInterval.Day,
        int retainedFileCountLimit = 31,
        string? outputTemplate = null)
    {
        outputTemplate ??= "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        _loggerConfig.WriteTo.File(
            path: path,
            rollingInterval: rollingInterval,
            retainedFileCountLimit: retainedFileCountLimit,
            outputTemplate: outputTemplate);

        return this;
    }

    /// <summary>
    /// Add enrichers from log context
    /// Useful for adding custom properties to logs
    /// </summary>
    /// <returns>The configurator for chaining</returns>
    public LoggingConfigurator EnrichFromLogContext()
    {
        _loggerConfig.Enrich.FromLogContext();
        return this;
    }

    /// <summary>
    /// Build the Serilog logger
    /// </summary>
    /// <returns>Configured Serilog logger</returns>
    internal ILogger Build()
    {
        return _loggerConfig
            .MinimumLevel.Is(_minimumLevel)
            .CreateLogger();
    }
}

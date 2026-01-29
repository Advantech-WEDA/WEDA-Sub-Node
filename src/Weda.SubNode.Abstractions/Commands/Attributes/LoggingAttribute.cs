using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Abstractions.Commands.Attributes;

/// <summary>
/// Specifies that the handler should use logging behavior in the pipeline.
/// </summary>
/// <example>
/// <code>
/// [Logging]  // Uses default log levels
/// public class MyCommandHandler : ICommandHandler&lt;MyCommand, MyResult&gt;
///
/// [Logging(LogLevel.Information)]  // Custom log level for all events
/// public class ImportantCommandHandler : ICommandHandler&lt;ImportantCommand, ImportantResult&gt;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class LoggingAttribute : Attribute
{
    /// <summary>
    /// Gets the log level for command execution start.
    /// </summary>
    public LogLevel BeforeLevel { get; }

    /// <summary>
    /// Gets the log level for successful command completion.
    /// </summary>
    public LogLevel AfterLevel { get; }

    /// <summary>
    /// Gets the log level for command execution errors.
    /// </summary>
    public LogLevel ErrorLevel { get; }

    /// <summary>
    /// Initializes a new instance with default log levels.
    /// Before: Debug, After: Debug, Error: Warning.
    /// </summary>
    public LoggingAttribute()
        : this(LogLevel.Debug, LogLevel.Debug, LogLevel.Warning)
    {
    }

    /// <summary>
    /// Initializes a new instance with the same log level for all events.
    /// </summary>
    /// <param name="level">The log level to use for before, after, and error events.</param>
    public LoggingAttribute(LogLevel level)
        : this(level, level, level)
    {
    }

    /// <summary>
    /// Initializes a new instance with custom log levels.
    /// </summary>
    /// <param name="beforeLevel">Log level for command execution start.</param>
    /// <param name="afterLevel">Log level for successful command completion.</param>
    /// <param name="errorLevel">Log level for command execution errors.</param>
    public LoggingAttribute(LogLevel beforeLevel, LogLevel afterLevel, LogLevel errorLevel)
    {
        BeforeLevel = beforeLevel;
        AfterLevel = afterLevel;
        ErrorLevel = errorLevel;
    }
}

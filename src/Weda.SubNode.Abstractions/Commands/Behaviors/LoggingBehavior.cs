using System.Diagnostics;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Commands.Behaviors;

/// <summary>
/// Pipeline behavior that logs command execution.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public class LoggingBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    private readonly LogLevel _beforeLevel;
    private readonly LogLevel _afterLevel;
    private readonly LogLevel _errorLevel;

    /// <summary>
    /// Initializes a new instance with default log levels.
    /// Before: Debug, After: Debug, Error: Warning.
    /// </summary>
    public LoggingBehavior()
        : this(LogLevel.Debug, LogLevel.Debug, LogLevel.Warning)
    {
    }

    /// <summary>
    /// Initializes a new instance with custom log levels.
    /// </summary>
    /// <param name="beforeLevel">Log level for command execution start.</param>
    /// <param name="afterLevel">Log level for successful command completion.</param>
    /// <param name="errorLevel">Log level for command execution errors.</param>
    public LoggingBehavior(LogLevel beforeLevel, LogLevel afterLevel, LogLevel errorLevel)
    {
        _beforeLevel = beforeLevel;
        _afterLevel = afterLevel;
        _errorLevel = errorLevel;
    }

    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<LoggingBehavior<TCommand, TResult>>();
        var commandName = typeof(TCommand).Name;

        logger.Log(_beforeLevel, "{Marker} Executing command {CommandName}", ">>>>>", commandName);
        var sw = Stopwatch.StartNew();

        var result = await next();

        sw.Stop();

        if (result.IsError)
        {
            logger.Log(_errorLevel,
                "{Marker} Command {CommandName} failed in {ElapsedMilliseconds} ms with errors: {Errors}",
                "<<<<<",
                commandName,
                sw.ElapsedMilliseconds,
                string.Join(", ", result.Errors.Select(e => e.Code)));
        }
        else
        {
            logger.Log(_afterLevel,
                "{Marker} Command {CommandName} completed in {ElapsedMilliseconds} ms",
                "<<<<<",
                commandName,
                sw.ElapsedMilliseconds);
        }

        return result;
    }
}
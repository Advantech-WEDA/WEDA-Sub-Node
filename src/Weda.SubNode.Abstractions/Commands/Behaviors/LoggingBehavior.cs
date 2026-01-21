using System.Diagnostics;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Commands.Behaviors;

public class LoggingBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<LoggingBehavior<TCommand, TResult>>();
        var commandName = typeof(TCommand).Name;

        logger.LogDebug("Exeuting command {CommandName}", commandName);
        var sw = Stopwatch.StartNew();

        var result = await next();

        sw.Stop();

        if (result.IsError)
        {
            logger.LogWarning(
                "Command {CommandName} failed in {ElapsedMilliseconds} ms with errors: {Errors}",
                commandName,
                sw.ElapsedMilliseconds,
                string.Join(", ", result.Errors.Select(e => e.Code)));
        }
        else
        {
            logger.LogDebug(
                "Command {CommandName} completed in {ElapsedMilliseconds} ms",
                commandName,
                sw.ElapsedMilliseconds);
        }

        return result;
    }
}
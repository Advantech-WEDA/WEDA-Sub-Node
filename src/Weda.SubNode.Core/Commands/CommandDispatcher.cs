using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Core.Commands;

public class CommandDispatcher(CommandRegistry registry, IWedaApplicationContext context)
{
    private readonly ILogger _logger = context.GetLogger<CommandDispatcher>();

    public async Task<ErrorOr<object?>> DispatchAsync(
        CommandEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var handler = registry.GetHandler(envelope.CommandName);
        if (handler is null)
        {
            _logger.LogWarning("No handler found for command: {CommandName}", envelope.CommandName);
            return Errors.Command.HandlerNotFound($"No handler registered for command '{envelope.CommandName}'");
        }   

        try
        {
            return await handler(envelope, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command {CommandName} execution failed", envelope.CommandName);
            return Errors.Command.ExecutionFailed(ex.Message);
        }
    }
}
using ErrorOr;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Core.Commands;

public class CommandRegistry
{
    private readonly Dictionary<string, Func<CommandEnvelope, IWedaApplicationContext, CancellationToken, Task<ErrorOr<object?>>>> _handlers = new(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// Registers a command handler by command name.
    /// </summary>
    public void Register<TCommand, TResult>(
        string commandName,
        ICommandHandler<TCommand, TResult> handler)
        where TCommand : ICommand
    {
        if (_handlers.ContainsKey(commandName))
        {
            throw new InvalidOperationException($"The command name {commandName} has been registered");
        }

        _handlers[commandName] = async (envelope, context, ct) =>
        {
            var command = DeserializeCommand<TCommand>(envelope);
            var result = await handler.HandleAsync(command, context, ct);
            return result.Match<ErrorOr<object?>>(
                value => value,
                errors => errors);
        };
    }

    /// <summary>
    /// Register a delegate-based command handler.
    /// </summary>
    public void Register<TCommand, TResult>(
        string commandName,
        Func<TCommand, IWedaApplicationContext, CancellationToken, Task<ErrorOr<TResult>>> handler)
            where TCommand : ICommand
    {
        if (_handlers.ContainsKey(commandName))
        {
            throw new InvalidOperationException($"The command name {commandName} has been registered");
        }

        _handlers[commandName] = async (envelope, context, ct) =>
        {
            var command = DeserializeCommand<TCommand>(envelope);
            var result = await handler(command, context, ct);
            return result.Match<ErrorOr<object?>>(
                value => value, 
                errors => errors);
        };
    }

    /// <summary>
    /// Gets the handler for the specified command name.
    /// </summary>
    public Func<CommandEnvelope, IWedaApplicationContext, CancellationToken, Task<ErrorOr<object?>>>? GetHandler(string commandName)
    {
        return _handlers.TryGetValue(commandName, out var handler) ? handler : null;
    }

    private static TCommand DeserializeCommand<TCommand>(CommandEnvelope envelope)
        where TCommand : ICommand
    {
        throw new NotImplementedException();
    }   
}
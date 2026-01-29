using ErrorOr;

using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Defines a handler for a specific command type.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
/// <typeparam name="TResult">The type of result to return.</typeparam>
public interface ICommandHandler<TCommand, TResult> 
    where TCommand : ICommand
{
    Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default);
}
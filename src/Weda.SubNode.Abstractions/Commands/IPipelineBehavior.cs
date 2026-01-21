using ErrorOr;

using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Defines a pipeline behavior that wraps the command handler execution.
/// </summary>
/// <typeparam name="TCommand">The type of command.</typeparam>
/// <typeparam name="TResult">The type of result.</typeparam>
public interface IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default);
}
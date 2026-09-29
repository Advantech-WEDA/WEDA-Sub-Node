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

    /// <summary>
    /// Optionally produces a payload to attach to the framework-sent acknowledgment (ack).
    /// </summary>
    /// <remarks>
    /// The framework owns the ack: it stamps executedAt and sends the ack before the handler
    /// body runs. This hook lets a handler enrich that ack with command-specific data (for
    /// example, BatchReport's estimated download volume) without taking over ack ownership.
    /// The returned object becomes the ack's <c>resultData</c>; the default returns null, so a
    /// handler that does not override this ships a plain ack.
    /// <para>
    /// This runs before the handler body, so it must not rely on any work the body performs.
    /// It should be cheap and side-effect free. If it throws, the framework logs and sends a
    /// plain ack anyway — a failure to compute the payload must never fail the ack itself.
    /// </para>
    /// </remarks>
    /// <param name="command">The deserialized command.</param>
    /// <param name="context">The application context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ack payload, or null to send a plain ack.</returns>
    Task<object?> AckAsync(
        TCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(null);
}

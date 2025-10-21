namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event handler interface for strongly-typed event handling
/// </summary>
public interface IEventHandler<in TEvent>
    where TEvent : class
{
    /// <summary>
    /// Handle event
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

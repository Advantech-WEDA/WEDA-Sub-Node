using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Connection state changed (physical device or cloud service).
/// Fired when communication state transitions (e.g., Connected → Disconnected).
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="SubNodeType">The device type.</param>
/// <param name="PreviousState">The connection state before the change.</param>
/// <param name="CurrentState">The connection state after the change.</param>
/// <param name="Timestamp">The timestamp when connection state changed.</param>
public sealed record ConnectionStateChangedEvent(
    string DeviceId,
    SubNodeType SubNodeType,
    CommunicationState PreviousState,
    CommunicationState CurrentState,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the reason for the state change (optional).
    /// </summary>
    public string? Reason { get; init; }
}

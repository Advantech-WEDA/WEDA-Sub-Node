using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event data for device status transitions.
/// Contains information about the state change including before/after states and timing.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="FromStatus">The status before the transition.</param>
/// <param name="ToStatus">The status after the transition.</param>
/// <param name="Timestamp">The timestamp when the transition occurred.</param>
public sealed record DeviceStatusTransitionEvent(
    string DeviceId,
    DeviceStatus FromStatus,
    DeviceStatus ToStatus,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the duration of the transition operation.
    /// Typically very fast (microseconds) but may be longer if event handlers are slow.
    /// </summary>
    public TimeSpan TransitionDuration { get; init; }

    /// <summary>
    /// Gets additional context or reason for the transition (optional).
    /// </summary>
    public string? Reason { get; init; }
}

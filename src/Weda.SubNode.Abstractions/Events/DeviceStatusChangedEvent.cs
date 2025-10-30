using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Device status changed (lifecycle state transition).
/// Fired when device transitions between operational states.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="DeviceType">The device type.</param>
/// <param name="PreviousStatus">The status before the change.</param>
/// <param name="CurrentStatus">The status after the change.</param>
/// <param name="Timestamp">The timestamp when status changed.</param>
public sealed record DeviceStatusChangedEvent(
    string DeviceId,
    DeviceType DeviceType,
    DeviceStatus PreviousStatus,
    DeviceStatus CurrentStatus,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the reason for the status change (optional).
    /// </summary>
    public string? Reason { get; init; }
}

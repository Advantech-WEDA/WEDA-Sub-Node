namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Device health status changed.
/// Fired when device health transitions between Healthy, Degraded, and Unhealthy states.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="PreviousStatus">The health status before the change.</param>
/// <param name="CurrentStatus">The health status after the change.</param>
/// <param name="Reason">The reason for the health status change.</param>
/// <param name="Timestamp">The timestamp when health status changed.</param>
public sealed record DeviceHealthChangedEvent(
    string DeviceId,
    Devices.HealthStatus PreviousStatus,
    Devices.HealthStatus CurrentStatus,
    string Reason,
    DateTimeOffset Timestamp);

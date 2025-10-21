namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: SubNode internal (telemetry sent to cloud).
/// Fired after attempting to send telemetry to cloud service.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="MeasureCount">The number of measures sent (or attempted).</param>
/// <param name="Success">Whether the send operation succeeded.</param>
/// <param name="Timestamp">The timestamp when send was attempted.</param>
public sealed record TelemetrySentEvent(
    string DeviceId,
    int MeasureCount,
    bool Success,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the error message if the send operation failed.
    /// </summary>
    public string? Error { get; init; }
}

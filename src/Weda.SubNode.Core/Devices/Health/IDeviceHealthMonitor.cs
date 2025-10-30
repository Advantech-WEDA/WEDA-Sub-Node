using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Devices.Health;

/// <summary>
/// Device health monitoring service.
/// Collects metrics, computes health status, and detects anomalies.
/// </summary>
public interface IDeviceHealthMonitor
{
    /// <summary>
    /// Gets the current health status of the device.
    /// </summary>
    HealthStatus CurrentHealthStatus { get; }

    /// <summary>
    /// Records a successful operation.
    /// </summary>
    /// <param name="operationType">The type of operation (e.g., "TelemetryRead", "CloudSend").</param>
    void RecordSuccess(string operationType);

    /// <summary>
    /// Records a failed operation.
    /// </summary>
    /// <param name="operationType">The type of operation that failed.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    void RecordFailure(string operationType, Exception exception);

    /// <summary>
    /// Records the duration of a telemetry read operation.
    /// </summary>
    /// <param name="duration">The duration of the read operation.</param>
    void RecordTelemetryReadDuration(TimeSpan duration);

    /// <summary>
    /// Records the duration of a cloud send operation.
    /// </summary>
    /// <param name="duration">The duration of the send operation.</param>
    void RecordCloudSendDuration(TimeSpan duration);

    /// <summary>
    /// Records a connection attempt.
    /// </summary>
    /// <param name="success">Whether the connection was successful.</param>
    void RecordConnectionAttempt(bool success);

    /// <summary>
    /// Gets the current health status asynchronously.
    /// This may include additional system metrics collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current device health.</returns>
    Task<DeviceHealth> GetCurrentHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health metrics for a specific time window.
    /// </summary>
    /// <param name="window">The time window to query. If null, uses configured metrics window.</param>
    /// <returns>The aggregated health metrics.</returns>
    DeviceHealthMetrics GetMetrics(TimeSpan? window = null);

    /// <summary>
    /// Resets all collected metrics (for testing or recovery scenarios).
    /// </summary>
    void ResetMetrics();

    /// <summary>
    /// Event raised when device health status changes.
    /// </summary>
    event EventHandler<DeviceHealthChangedEvent>? HealthStatusChanged;
}

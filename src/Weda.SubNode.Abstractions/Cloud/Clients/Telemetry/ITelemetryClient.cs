using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;

/// <summary>
/// Telemetry Client for sending telemetry data and health reports.
/// Supports multiple devices with per-device topic assignments.
/// </summary>
public interface ITelemetryClient
{
    /// <summary>
    /// Configure topic assignments for a specific device.
    /// Each device has its own set of topic assignments.
    /// Must be called after device registration.
    /// </summary>
    /// <param name="deviceName">The device name (used as key for topic lookup)</param>
    /// <param name="topicAssignments">The NATS topic assignments for this device</param>
    void ConfigureTopics(string deviceName, NatsTopicAssignments topicAssignments);

    /// <summary>
    /// Send telemetry data to cloud
    /// </summary>
    Task<TelemetrySendResponse> SendTelemetryAsync(
        string deviceId,
        TelemetryData telemetryData,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send batch telemetry data to cloud (optimized for bulk data)
    /// </summary>
    Task<TelemetrySendResponse> SendBatchTelemetryAsync(
        string deviceId,
        List<TelemetryData> telemetryDataList,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Report device health to cloud
    /// </summary>
    Task<HealthReportResponse> ReportHealthAsync(
        string deviceId,
        DeviceHealth health,
        CancellationToken cancellationToken = default);
}

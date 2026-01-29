using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Net;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Cloud.Clients;

/// <summary>
/// Telemetry Client implementation for NATS JetStream.
/// Handles telemetry data transmission and health reporting with guaranteed delivery.
/// Supports multiple devices with per-device topic assignments.
/// </summary>
public class TelemetryClient : ITelemetryClient
{
    private readonly ILogger<TelemetryClient> _logger;
    private readonly NatsClient _client;
    private readonly ConcurrentDictionary<string, NatsTopicAssignments> _deviceTopics = new();

    public TelemetryClient(
        NatsClient client,
        ILogger<TelemetryClient>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<TelemetryClient>();
    }

    /// <summary>
    /// Configure topic assignments for a specific device.
    /// Each device has its own set of topic assignments.
    /// </summary>
    public void ConfigureTopics(string deviceName, NatsTopicAssignments topicAssignments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(topicAssignments);

        _deviceTopics[deviceName] = topicAssignments;

        _logger.LogInformation(
            "Telemetry client topics configured for device {DeviceName}: TelemetryTopic={TelemetryTopic}, HealthTopic={HealthTopic}",
            deviceName,
            topicAssignments.TelemetryTopic,
            topicAssignments.HealthTopic);
    }

    private NatsTopicAssignments? FindTopicsByDeviceId(string deviceId)
    {
        // Search through all device topics to find one whose topic contains the deviceId
        foreach (var kvp in _deviceTopics)
        {
            if (kvp.Value.TelemetryTopic.Contains(deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // Fallback: try using deviceId as deviceName directly
        if (_deviceTopics.TryGetValue(deviceId, out var topics))
        {
            return topics;
        }

        return null;
    }

    public async Task<TelemetrySendResponse> SendTelemetryAsync(
        string deviceId,
        TelemetryData telemetryData,
        CancellationToken cancellationToken = default)
    {
        var topicAssignments = FindTopicsByDeviceId(deviceId);
        if (topicAssignments?.TelemetryTopic == null)
        {
            throw new InvalidOperationException(
                $"Telemetry topics not configured for device {deviceId}. " +
                "Call ConfigureTopics() after device registration.");
        }

        // Create message with audit fields
        var message = TelemetrySendMessage.Create(TelemetryMeasureDto.From(telemetryData.Measures));

        _logger.LogDebug(
            "Sending telemetry: DeviceId={DeviceId}, MeasureCount={MeasureCount}, Topic={Topic}, ReqSeqId={ReqSeqId}",
            deviceId,
            telemetryData.Measures.Count,
            topicAssignments.TelemetryTopic,
            message.SeqId);

        foreach (var measure in telemetryData.Measures)
        {
            _logger.LogDebug(
                "  Measure: ResourceId={ResourceId}, Value={Value}, Timestamp={Timestamp}",
                measure.SensorId,
                measure.Value,
                DateTimeOffset.FromUnixTimeMilliseconds(measure.Timestamp));
        }

        // Send telemetry via NATS with fire and forget
        await _client.PublishAsync(
            subject: topicAssignments.TelemetryTopic,
            data: message,
            cancellationToken: cancellationToken);

        // Create success response
        var response = new TelemetrySendResponse
        {
            ReqSeqId = message.SeqId.ToString(),
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Code = 0,
            Message = "Telemetry sent successfully",
            Data = new TelemetrySendResponseData
            {
                Status = "success",
                MeasureCount = telemetryData.Measures.Count
            }
        };

        _logger.LogDebug(
            "Telemetry sent successfully: MeasureCount={MeasureCount}, Topic={Topic}",
            telemetryData.Measures.Count,
            topicAssignments.TelemetryTopic);

        return response;
    }

    public async Task<HealthReportResponse> ReportHealthAsync(
        string deviceId,
        DeviceHealth health,
        CancellationToken cancellationToken = default)
    {
        var topicAssignments = FindTopicsByDeviceId(deviceId);
        if (topicAssignments?.HealthTopic == null)
        {
            throw new InvalidOperationException(
                $"Telemetry topics not configured for device {deviceId}. " +
                "Call ConfigureTopics() after device registration.");
        }

        // Create message with audit fields
        var message = HealthReportMessage.Create(health);

        _logger.LogInformation(
            "Reporting health: DeviceId={DeviceId}, IsHealthy={IsHealthy}, CpuUsage={CpuUsage:F2}%, MemoryUsage={MemoryUsage:F2}%, Topic={Topic}, ReqSeqId={ReqSeqId}",
            deviceId,
            health.IsHealthy,
            health.CpuUsage,
            health.MemoryUsage,
            topicAssignments.HealthTopic,
            message.SeqId);

        if (!string.IsNullOrEmpty(health.LastError))
        {
            _logger.LogWarning("  Last error: {LastError}", health.LastError);
        }

        await _client.PublishAsync(
            subject: topicAssignments.HealthTopic,
            data: message,
            cancellationToken: cancellationToken);

        var response = new HealthReportResponse
        {
            ReqSeqId = message.SeqId.ToString(),
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Code = 0,
            Message = "Health report sent successfully",
            Data = new HealthReportResponseData
            {
                Status = "success",
                DeviceId = deviceId
            }
        };

        _logger.LogInformation(
            "Health report sent successfully: DeviceId={DeviceId}, Topic={Topic}",
            deviceId,
            topicAssignments.HealthTopic);

        return response;
    }
}

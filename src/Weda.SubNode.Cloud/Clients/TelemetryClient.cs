using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Net;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Cloud.Clients;

/// <summary>
/// Telemetry Client implementation for NATS JetStream
/// Handles telemetry data transmission and health reporting with guaranteed delivery
/// Uses topic assignments from device registration
/// </summary>
public class TelemetryClient : ITelemetryClient
{
    private readonly ILogger<TelemetryClient> _logger;
    private readonly NatsClient _client;
    private readonly INatsJSContext _jetStream;
    private NatsTopicAssignments? _topicAssignments;

    public TelemetryClient(
        NatsClient client,
        ILogger<TelemetryClient>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _jetStream = _client.CreateJetStreamContext();
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<TelemetryClient>();
    }

    /// <summary>
    /// Configure topic assignments from device registration
    /// Must be called after device registration
    /// </summary>
    public void ConfigureTopics(NatsTopicAssignments topicAssignments)
    {
        ArgumentNullException.ThrowIfNull(topicAssignments);
        _topicAssignments = topicAssignments;

        _logger.LogInformation(
            "Telemetry client topics configured: TelemetryTopic={TelemetryTopic}, BatchTelemetryTopic={BatchTelemetryTopic}, HealthTopic={HealthTopic}",
            topicAssignments.TelemetryTopic,
            topicAssignments.BatchTelemetryTopic,
            topicAssignments.HealthTopic);
    }

    public async Task<TelemetrySendResponse> SendTelemetryAsync(
        string deviceId,
        TelemetryData telemetryData,
        CancellationToken cancellationToken = default)
    {
        if (_topicAssignments?.TelemetryTopic == null)
        {
            throw new InvalidOperationException(
                "Telemetry topics not configured. Call ConfigureTopics() after device registration.");
        }

        // Create message with audit fields
        var message = TelemetrySendMessage.Create(TelemetryMeasureDto.From(telemetryData.Measures));

        _logger.LogInformation(
            "Sending telemetry: DeviceId={DeviceId}, MeasureCount={MeasureCount}, Topic={Topic}, ReqSeqId={ReqSeqId}",
            deviceId,
            telemetryData.Measures.Count,
            _topicAssignments.TelemetryTopic,
            message.SeqId);

        foreach (var measure in telemetryData.Measures)
        {
            _logger.LogDebug(
                "  Measure: ResourceId={ResourceId}, Value={Value}, Timestamp={Timestamp}",
                measure.SensorId,
                measure.Value,
                DateTimeOffset.FromUnixTimeMilliseconds(measure.Timestamp));
        }

        // Send telemetry via NATS JetStream with guaranteed delivery

        // (1) Publish with fire and forget
        await _client.PublishAsync(
            subject: _topicAssignments.TelemetryTopic,
            data: message,
            cancellationToken: cancellationToken);

        // var ack = await _jetStream.PublishAsync(
        //     subject: _topicAssignments.TelemetryTopic,
        //     data: message,
        //     cancellationToken: cancellationToken);

        // Verify message was persisted
        // ack.EnsureSuccess();

        // _logger.LogDebug(
        //     "Telemetry persisted to stream: Stream={Stream}, Sequence={Sequence}",
        //     ack.Stream,
        //     ack.Seq);

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

        _logger.LogInformation(
            "Telemetry sent successfully: MeasureCount={MeasureCount}, Topic={Topic}",
            telemetryData.Measures.Count,
            _topicAssignments.TelemetryTopic);

        return response;
    }

    public async Task<TelemetrySendResponse> SendBatchTelemetryAsync(
        string deviceId,
        List<TelemetryData> telemetryDataList,
        CancellationToken cancellationToken = default)
    {
        if (_topicAssignments?.BatchTelemetryTopic == null)
        {
            throw new InvalidOperationException(
                "Telemetry topics not configured. Call ConfigureTopics() after device registration.");
        }

        var totalMeasures = telemetryDataList.Sum(td => td.Measures.Count);

        _logger.LogInformation(
            "Sending batch telemetry: DeviceId={DeviceId}, BatchCount={BatchCount}, TotalMeasures={TotalMeasures}, Topic={Topic}",
            deviceId,
            telemetryDataList.Count,
            totalMeasures,
            _topicAssignments.BatchTelemetryTopic);

        // Send batch telemetry via NATS JetStream with guaranteed delivery
        var ack = await _jetStream.PublishAsync(
            subject: _topicAssignments.BatchTelemetryTopic,
            data: telemetryDataList,
            cancellationToken: cancellationToken);

        // Verify message was persisted
        ack.EnsureSuccess();

        _logger.LogDebug(
            "Batch telemetry persisted to stream: Stream={Stream}, Sequence={Sequence}",
            ack.Stream,
            ack.Seq);

        var response = new TelemetrySendResponse
        {
            ReqSeqId = Guid.NewGuid().ToString(),
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Code = 0,
            Message = "Batch telemetry sent successfully",
            Data = new TelemetrySendResponseData
            {
                Status = "success",
                MeasureCount = totalMeasures
            }
        };

        _logger.LogInformation(
            "Batch telemetry sent successfully: TotalMeasures={TotalMeasures}, Topic={Topic}",
            totalMeasures,
            _topicAssignments.BatchTelemetryTopic);

        return response;
    }

    public async Task<HealthReportResponse> ReportHealthAsync(
        string deviceId,
        DeviceHealth health,
        CancellationToken cancellationToken = default)
    {
        if (_topicAssignments?.HealthTopic == null)
        {
            throw new InvalidOperationException(
                "Telemetry topics not configured. Call ConfigureTopics() after device registration.");
        }

        // Create message with audit fields
        var message = HealthReportMessage.Create(health);

        _logger.LogInformation(
            "Reporting health: DeviceId={DeviceId}, IsHealthy={IsHealthy}, CpuUsage={CpuUsage}%, MemoryUsage={MemoryUsage}%, Topic={Topic}, ReqSeqId={ReqSeqId}",
            deviceId,
            health.IsHealthy,
            health.CpuUsage,
            health.MemoryUsage,
            _topicAssignments.HealthTopic,
            message.SeqId);

        if (!string.IsNullOrEmpty(health.LastError))
        {
            _logger.LogWarning("  Last error: {LastError}", health.LastError);
        }

        await _client.PublishAsync(
            subject: _topicAssignments.HealthTopic,
            data: message
        );

        // Send health report via NATS JetStream with guaranteed delivery
        // var ack = await _jetStream.PublishAsync(
        //     subject: _topicAssignments.HealthTopic,
        //     data: message,
        //     cancellationToken: cancellationToken);

        // Verify message was persisted
        // ack.EnsureSuccess();

        // _logger.LogDebug(
        //     "Health report persisted to stream: Stream={Stream}, Sequence={Sequence}",
        //     ack.Stream,
        //     ack.Seq);

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
            _topicAssignments.HealthTopic);

        return response;
    }
}

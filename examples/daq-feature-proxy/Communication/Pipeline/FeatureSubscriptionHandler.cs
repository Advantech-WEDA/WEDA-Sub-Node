using System.Text.Json;
using Microsoft.Extensions.Logging;
using NATS.Net;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace daq_feature_proxy.Communication.Pipeline;

public class FeatureSubscriptionHandler
{
    private readonly NatsClient _natsClient;
    private readonly string _subject;
    private readonly ITelemetryTransform _transform;
    private readonly ILogger _logger;
    private readonly Func<List<TelemetryMeasure>, CancellationToken, Task> _sendTelemetry;

    public FeatureSubscriptionHandler(
        NatsClient natsClient,
        string subject,
        ITelemetryTransform transform,
        ILogger logger,
        Func<List<TelemetryMeasure>, CancellationToken, Task> sendTelemetry)
    {
        _natsClient = natsClient;
        _subject = subject;
        _transform = transform;
        _logger = logger;
        _sendTelemetry = sendTelemetry;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Feature subscription started on '{Subject}'", _subject);

            await foreach (var msg in _natsClient.SubscribeAsync<string>(_subject, cancellationToken: ct))
            {
                if (string.IsNullOrEmpty(msg.Data))
                    continue;

                // Fire-and-forget per message to avoid blocking the subscription loop
                _ = ProcessMessageAsync(msg.Data, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Feature subscription stopped on '{Subject}'", _subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Feature subscription error on '{Subject}'", _subject);
        }
    }

    private async Task ProcessMessageAsync(string messageJson, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var root = doc.RootElement;

            var timestamp = root.GetProperty("timestamp").GetInt64();
            var deviceId = root.TryGetProperty("deviceId", out var did) ? did.GetString() ?? "" : "";
            var measuresArray = root.GetProperty("data").GetProperty("measures");

            var measures = new List<TelemetryMeasure>();
            foreach (var m in measuresArray.EnumerateArray())
            {
                var sensorId = m.GetProperty("sensorId").GetString();
                var value = m.GetProperty("value").GetDouble();
                var ts = m.TryGetProperty("timestamp", out var tsProp) ? tsProp.GetInt64() : timestamp;
                if (!string.IsNullOrEmpty(sensorId))
                    measures.Add(new TelemetryMeasure { ResourceId = sensorId, Value = value, Timestamp = ts });
            }

            if (measures.Count == 0)
            {
                _logger.LogWarning("Received empty feature set, skipping");
                return;
            }

            _logger.LogDebug("Received {Count} features at ts={Timestamp}", measures.Count, timestamp);

            var context = new TelemetryTransformContext
            {
                DeviceId = deviceId,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
            };

            var outputMeasures = await _transform.TransformAsync(measures, context, ct);

            if (outputMeasures.Count > 0)
                await _sendTelemetry(outputMeasures, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process feature message");
        }
    }
}

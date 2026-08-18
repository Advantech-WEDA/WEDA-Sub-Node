using System.Globalization;
using System.Text;
using System.Text.Json;

using ErrorOr;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace VisionObjectDetection.Protocols;

/// <summary>
/// Pub/Sub parser for the Advantech YOLO object-detection demo container.
///
/// <para>Subscribes to the CV container's three retained/streamed topics under
/// <c>advantech/&lt;DEVICE_ID&gt;/vision/</c> and maps the 1&#160;Hz <c>detections</c>
/// JSON into scalar telemetry measures. Each configured sensor selects one metric
/// via its <c>Parameters.Field</c>:</para>
///
/// <list type="bullet">
///   <item><c>objectCount</c> / <c>fps</c> / <c>frame</c> / <c>lap</c> — top-level scalars</item>
///   <item><c>confidence.max</c> / <c>confidence.avg</c> — aggregated over the detections array</item>
///   <item><c>classCount.&lt;name&gt;</c> — per-class count (0 when the class is absent)</item>
/// </list>
///
/// <para>The parser is read-only: the CV container publishes telemetry but exposes no
/// command surface, so <see cref="ExecuteCommandAsync"/> returns a not-supported error.</para>
/// </summary>
public sealed class VisionDetectionParser : IPubSubProtocolParser
{
    private const string DetectionsSuffix = "/vision/detections";
    private const string StatusSuffix = "/vision/status";
    private const string MetaSuffix = "/vision/meta";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPubSub _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly ILogger<VisionDetectionParser> _logger;

    private readonly string _detectionsTopic;
    private readonly string _statusTopic;
    private readonly string _metaTopic;

    /// <summary>Pre-resolved (sensor, field-selector) pairs, built once from configuration.</summary>
    private readonly List<(Sensor Sensor, string Field)> _fieldMap;

    private bool _isSubscribed;

    /// <inheritdoc />
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    public VisionDetectionParser(
        DeviceConfiguration configuration,
        IPubSub communication,
        ILogger<VisionDetectionParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // DeviceId "+" (default) subscribes to every CV container on the broker;
        // set a concrete 12-char hex id in devicecfg.json to pin to one device.
        var deviceId = configuration.DeviceCommunication.TryGetValue("DeviceId", out var id)
            && id?.ToString() is { Length: > 0 } value
                ? value
                : "+";

        _detectionsTopic = $"advantech/{deviceId}/vision/detections";
        _statusTopic = $"advantech/{deviceId}/vision/status";
        _metaTopic = $"advantech/{deviceId}/vision/meta";

        _fieldMap = BuildFieldMap(configuration);

        if (_fieldMap.Count == 0)
        {
            _logger.LogWarning(
                "No sensors declare a 'Field' parameter; no vision telemetry will be produced.");
        }
    }

    private static List<(Sensor, string)> BuildFieldMap(DeviceConfiguration configuration)
    {
        var map = new List<(Sensor, string)>();
        foreach (var sensor in configuration.Sensors)
        {
            if (sensor.Parameters?.TryGetValue("Field", out var field) == true
                && field?.ToString() is { Length: > 0 } selector)
            {
                map.Add((sensor, selector));
            }
        }

        return map;
    }

    /// <inheritdoc />
    public ICommunication Communication => _communication;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isSubscribed)
        {
            _logger.LogWarning("Already subscribed to vision topics; stop before restarting.");
            return;
        }

        _communication.MessageReceived += OnMessageReceived;

        await _communication.SubscribeAsync(_detectionsTopic, cancellationToken);
        await _communication.SubscribeAsync(_statusTopic, cancellationToken);
        await _communication.SubscribeAsync(_metaTopic, cancellationToken);

        _isSubscribed = true;

        _logger.LogInformation(
            "Subscribed to vision topics: {Detections}, {Status}, {Meta}",
            _detectionsTopic, _statusTopic, _metaTopic);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isSubscribed)
        {
            return;
        }

        _communication.MessageReceived -= OnMessageReceived;

        await _communication.UnsubscribeAsync(_detectionsTopic, cancellationToken);
        await _communication.UnsubscribeAsync(_statusTopic, cancellationToken);
        await _communication.UnsubscribeAsync(_metaTopic, cancellationToken);

        _isSubscribed = false;
        _logger.LogInformation("Unsubscribed from vision topics.");
    }

    /// <inheritdoc />
    public Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ErrorOr<object>>(
            Error.Failure(
                code: "Command.NotSupported",
                description: "The vision object-detection container is telemetry-only and accepts no commands."));
    }

    private void OnMessageReceived(object? sender, MessageReceivedEvent<byte[]> e)
    {
        try
        {
            if (e.Topic.EndsWith(DetectionsSuffix, StringComparison.Ordinal))
            {
                HandleDetections(e.Payload);
            }
            else if (e.Topic.EndsWith(StatusSuffix, StringComparison.Ordinal))
            {
                HandleStatus(e.Topic, e.Payload);
            }
            else if (e.Topic.EndsWith(MetaSuffix, StringComparison.Ordinal))
            {
                HandleMeta(e.Payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling vision message from topic {Topic}", e.Topic);
        }
    }

    private void HandleDetections(byte[] payload)
    {
        VisionDetectionMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<VisionDetectionMessage>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Discarding malformed detections payload ({Length} bytes)", payload.Length);
            return;
        }

        if (message is null)
        {
            _logger.LogWarning("Detections payload deserialized to null; skipping.");
            return;
        }

        var timestamp = ParseTimestamp(message.Timestamp);

        var measures = new List<TelemetryMeasure>(_fieldMap.Count);
        foreach (var (sensor, field) in _fieldMap)
        {
            if (!sensor.IsEffectivelyEnabled)
            {
                continue;
            }

            if (!TryComputeValue(field, message, out var value))
            {
                _logger.LogTrace("Field '{Field}' not resolvable for sensor {Sensor}", field, sensor.Name);
                continue;
            }

            // Metadata is deliberately left unset. It is the framework's chunked-transfer
            // descriptor, and these values are numeric so they are never chunked -- they never
            // receive the transferId the WedaNode telemetry proxy requires, and a measure carrying
            // metadata without one is rejected outright. The sensor's own identity already names
            // the field, and the publishing device is identified by the SubNode itself.
            measures.Add(new TelemetryMeasure
            {
                ResourceId = sensor.ResourceId,
                Value = value,
                Timestamp = timestamp,
            });
        }

        if (measures.Count > 0)
        {
            OnTelemetryReceived?.Invoke(measures);
        }
    }

    /// <summary>
    /// Resolves a field selector against a detections message.
    /// Returns false only when the selector is unknown; a valid-but-absent
    /// metric (e.g. a class not seen this frame) resolves to 0.
    /// </summary>
    private static bool TryComputeValue(string field, VisionDetectionMessage message, out double value)
    {
        switch (field)
        {
            case "objectCount":
                value = message.ObjectCount;
                return true;
            case "fps":
                value = message.Fps;
                return true;
            case "frame":
                value = message.Frame;
                return true;
            case "lap":
                value = message.Lap;
                return true;
            case "confidence.max":
                value = message.Detections is { Count: > 0 } dMax
                    ? dMax.Max(d => d.Confidence)
                    : 0.0;
                return true;
            case "confidence.avg":
                value = message.Detections is { Count: > 0 } dAvg
                    ? dAvg.Average(d => d.Confidence)
                    : 0.0;
                return true;
        }

        const string classPrefix = "classCount.";
        if (field.StartsWith(classPrefix, StringComparison.Ordinal))
        {
            var className = field[classPrefix.Length..];
            value = message.ClassCounts is not null
                && message.ClassCounts.TryGetValue(className, out var count)
                    ? count
                    : 0.0;
            return true;
        }

        value = 0.0;
        return false;
    }

    private static long ParseTimestamp(string? isoTimestamp)
    {
        if (!string.IsNullOrEmpty(isoTimestamp)
            && DateTimeOffset.TryParse(
                isoTimestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed.ToUnixTimeMilliseconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void HandleStatus(string topic, byte[] payload)
    {
        // Status is a bare string ("online"/"offline"), optionally JSON-quoted.
        var status = Encoding.UTF8.GetString(payload).Trim().Trim('"');
        _logger.LogInformation("CV container status on {Topic}: {Status}", topic, status);
    }

    private void HandleMeta(byte[] payload)
    {
        try
        {
            var meta = JsonSerializer.Deserialize<VisionMetaMessage>(payload, JsonOptions);
            if (meta is not null)
            {
                _logger.LogInformation(
                    "CV model metadata: model={Model}, source={Source}, conf>={Conf}, iou>={Iou}, demo={Version}",
                    meta.Model, meta.Source, meta.ConfThreshold, meta.IouThreshold, meta.DemoVersion);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse vision meta payload.");
        }
    }
}

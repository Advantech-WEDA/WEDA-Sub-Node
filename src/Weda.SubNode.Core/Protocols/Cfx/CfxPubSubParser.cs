using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// CFX protocol parser implementing the Pub/Sub pattern over a bridged transport (MQTT today,
/// AMQP 1.0 once an AMQP <see cref="IPubSub"/> is available).
/// </summary>
/// <remarks>
/// <para>
/// Subscribe-only. CFX endpoints publish process events; this parser captures them passively and
/// does not participate in CFX request/response exchanges, so
/// <see cref="ExecuteCommandAsync"/> always reports the command as unsupported rather than
/// silently succeeding.
/// </para>
/// <para>
/// Each configured sensor binds to exactly one CFX message name and reports that message's body as
/// raw JSON text, with the envelope's routing fields carried in
/// <see cref="TelemetryMeasure.Metadata"/>. Nothing in the payload is flattened or discarded, which
/// keeps deeply nested bodies (a 24-slot Hermes magazine, an inspection report with per-measurement
/// discriminators) intact for downstream consumers.
/// </para>
/// </remarks>
public sealed class CfxPubSubParser : IPubSubProtocolParser
{
    /// <summary>Configuration key naming the CFX handle of the endpoint to subscribe to.</summary>
    public const string CfxHandleKey = "CfxHandle";

    /// <summary>Configuration key overriding the topic root segment.</summary>
    public const string TopicRootKey = "TopicRoot";

    /// <summary>Configuration key overriding the handle segment count used by the any-endpoint filter.</summary>
    public const string HandleSegmentsKey = "HandleSegments";

    /// <summary>Metadata keys attached to every emitted measure.</summary>
    private const string MetaMessageName = "cfxMessageName";
    private const string MetaSource = "cfxSource";
    private const string MetaUniqueId = "cfxUniqueId";
    private const string MetaVersion = "cfxVersion";
    private const string MetaTimeStamp = "cfxTimeStamp";
    private const string MetaTopic = "cfxTopic";

    private readonly IPubSub _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly ILogger<CfxPubSubParser> _logger;
    private readonly string _subscriptionFilter;

    /// <summary>
    /// Sensors bound to each CFX message name. A message may feed more than one sensor, so the
    /// value is a list rather than a single sensor.
    /// </summary>
    private readonly Dictionary<string, List<Sensor>> _sensorsByMessageName;

    private bool _isSubscribed;

    /// <inheritdoc />
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Raised for a decoded CFX message that no sensor is bound to. Lets application code observe
    /// the full CFX stream without declaring a sensor per message type.
    /// </summary>
    public event Action<CfxEnvelope>? OnUnmappedMessageReceived;

    /// <summary>
    /// Initializes a new instance of <see cref="CfxPubSubParser"/>.
    /// </summary>
    /// <param name="configuration">
    /// Device configuration. Each sensor must carry a <c>MessageName</c> parameter naming the CFX
    /// message it reports. <c>DeviceCommunication</c> may carry <c>CfxHandle</c> to scope the
    /// subscription to one endpoint; without it, every endpoint on the broker is matched.
    /// </param>
    /// <param name="communication">Pub/Sub transport to subscribe on.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a sensor is missing its <c>MessageName</c> parameter. Validated eagerly so that a
    /// configuration typo fails at startup rather than producing a sensor that never reports.
    /// </exception>
    public CfxPubSubParser(
        DeviceConfiguration configuration,
        IPubSub communication,
        ILogger<CfxPubSubParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _sensorsByMessageName = BuildSensorIndex(configuration, logger);
        _subscriptionFilter = BuildSubscriptionFilter(configuration, logger);
    }

    /// <summary>
    /// The topic filter this parser subscribes to.
    /// </summary>
    public string SubscriptionFilter => _subscriptionFilter;

    #region IProtocolParserCore Implementation

    /// <inheritdoc />
    public ICommunication Communication => _communication;

    #endregion

    #region IPubSubProtocolParser Implementation

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isSubscribed)
        {
            _logger.LogWarning("Already subscribed to CFX messages. Stop first before restarting.");
            return;
        }

        _communication.MessageReceived += OnMessageReceived;

        try
        {
            await _communication.SubscribeAsync(_subscriptionFilter, cancellationToken);
        }
        catch (Exception ex)
        {
            // Leaving the handler attached after a failed subscribe would leak it across retries.
            _communication.MessageReceived -= OnMessageReceived;

            throw new InvalidOperationException(
                $"Failed to subscribe to CFX topic filter '{_subscriptionFilter}'.",
                ex);
        }

        _isSubscribed = true;

        _logger.LogInformation(
            "Subscribed to CFX topic filter {Filter} for {SensorCount} sensor(s) across {MessageCount} message type(s)",
            _subscriptionFilter,
            _configuration.Sensors.Count,
            _sensorsByMessageName.Count);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isSubscribed)
        {
            _logger.LogWarning("Not currently subscribed to CFX messages.");
            return;
        }

        _communication.MessageReceived -= OnMessageReceived;

        try
        {
            await _communication.UnsubscribeAsync(_subscriptionFilter, cancellationToken);
        }
        catch (Exception ex)
        {
            // The handler is already detached, so the parser is functionally stopped; surface the
            // broker failure without masking it or leaving _isSubscribed inconsistent.
            _isSubscribed = false;

            _logger.LogError(
                ex,
                "Failed to unsubscribe cleanly from CFX topic filter {Filter}",
                _subscriptionFilter);
            return;
        }

        _isSubscribed = false;

        _logger.LogInformation("Unsubscribed from CFX topic filter {Filter}", _subscriptionFilter);
    }

    /// <summary>
    /// Always reports the command as unsupported.
    /// </summary>
    /// <remarks>
    /// CFX defines request/response messages, but acting on them requires the SubNode to register as
    /// a CFX endpoint and publish under its own handle. That is out of scope for a passive
    /// subscriber, and reporting failure is safer than accepting a command that will never be sent.
    /// </remarks>
    public Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogWarning(
            "Rejected command {CommandName}: the CFX parser is subscribe-only",
            command.DeviceCmd);

        return Task.FromResult<ErrorOr<object>>(Error.Failure(
            code: "Cfx.Command.NotSupported",
            description:
                $"Command '{command.DeviceCmd}' is not supported: the CFX protocol parser is "
                + "subscribe-only and does not publish to CFX endpoints."));
    }

    #endregion

    #region Message Handling

    private void OnMessageReceived(object? sender, MessageReceivedEvent<byte[]> e)
    {
        try
        {
            HandleMessage(e.Topic, e.Payload);
        }
        catch (Exception ex)
        {
            // A transport callback must never propagate: one malformed message from one endpoint
            // must not tear down the subscription for every other endpoint on the filter.
            _logger.LogError(ex, "Unhandled error processing CFX message from topic {Topic}", e.Topic);
        }
    }

    private void HandleMessage(string? topic, byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            _logger.LogWarning("Discarded empty CFX payload from topic {Topic}", topic);
            return;
        }

        var decoded = CfxPayloadCodec.Decode(payload);
        if (decoded.IsError)
        {
            _logger.LogWarning(
                "Failed to decode CFX payload from topic {Topic}: {ErrorCode} {ErrorDescription}",
                topic,
                decoded.FirstError.Code,
                decoded.FirstError.Description);
            return;
        }

        var read = CfxEnvelopeReader.Read(decoded.Value);
        if (read.IsError)
        {
            _logger.LogWarning(
                "Failed to read CFX envelope from topic {Topic}: {ErrorCode} {ErrorDescription}",
                topic,
                read.FirstError.Code,
                read.FirstError.Description);
            return;
        }

        var envelope = read.Value;

        if (!_sensorsByMessageName.TryGetValue(envelope.MessageName, out var sensors))
        {
            _logger.LogTrace(
                "No sensor bound to CFX message {MessageName} from {Source}",
                envelope.MessageName,
                envelope.Source);

            OnUnmappedMessageReceived?.Invoke(envelope);
            return;
        }

        var timestamp = (envelope.TimeStamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();
        var metadata = BuildMetadata(envelope, topic);
        var measures = new List<TelemetryMeasure>(sensors.Count);

        foreach (var sensor in sensors)
        {
            if (!sensor.IsEffectivelyEnabled)
            {
                _logger.LogTrace("Sensor {SensorName} is disabled, skipping", sensor.Name);
                continue;
            }

            measures.Add(new TelemetryMeasure
            {
                ResourceId = sensor.ResourceId,
                Value = envelope.MessageBodyJson,
                Timestamp = timestamp,
                Metadata = metadata,
            });
        }

        if (measures.Count == 0)
        {
            return;
        }

        _logger.LogDebug(
            "Parsed CFX message {MessageName} from {Source} into {Count} measure(s)",
            envelope.MessageName,
            envelope.Source,
            measures.Count);

        OnTelemetryReceived?.Invoke(measures);
    }

    private static IReadOnlyDictionary<string, object> BuildMetadata(CfxEnvelope envelope, string? topic)
    {
        var metadata = new Dictionary<string, object>(6)
        {
            [MetaMessageName] = envelope.MessageName,
        };

        // Metadata carries the envelope's routing fields; the body itself is the measure's value.
        AddIfPresent(metadata, MetaSource, envelope.Source);
        AddIfPresent(metadata, MetaUniqueId, envelope.UniqueId);
        AddIfPresent(metadata, MetaVersion, envelope.Version);
        AddIfPresent(metadata, MetaTopic, topic);

        if (envelope.TimeStamp is { } stamp)
        {
            // Round-trip format keeps the endpoint's original UTC offset legible downstream.
            metadata[MetaTimeStamp] = stamp.ToString("O");
        }

        return metadata;
    }

    private static void AddIfPresent(Dictionary<string, object> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }

    #endregion

    #region Configuration

    private static Dictionary<string, List<Sensor>> BuildSensorIndex(
        DeviceConfiguration configuration,
        ILogger<CfxPubSubParser> logger)
    {
        // CFX message names are matched case-insensitively: the envelope's MessageName is echoed by
        // the publisher and casing has been observed to drift between vendors.
        var index = new Dictionary<string, List<Sensor>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sensor in configuration.Sensors)
        {
            var messageName = sensor.GetMessageName();

            if (!CfxMessageCatalog.IsSupported(messageName))
            {
                // Not fatal: CFX has hundreds of message types and a deployment may legitimately
                // bind one outside the captured set. Warn so a typo is still visible.
                logger.LogWarning(
                    "Sensor {SensorName} is bound to CFX message {MessageName}, which is outside the "
                    + "captured message catalogue. Verify the name is spelled correctly.",
                    sensor.Name,
                    messageName);
            }

            if (!index.TryGetValue(messageName, out var sensors))
            {
                sensors = [];
                index[messageName] = sensors;
            }

            sensors.Add(sensor);
        }

        return index;
    }

    private static string BuildSubscriptionFilter(
        DeviceConfiguration configuration,
        ILogger<CfxPubSubParser> logger)
    {
        var root = ReadString(configuration, TopicRootKey) ?? CfxTopic.DefaultRoot;
        var cfxHandle = ReadString(configuration, CfxHandleKey);

        if (!string.IsNullOrWhiteSpace(cfxHandle))
        {
            return CfxTopic.SubscriptionFilterFor(cfxHandle, root);
        }

        var handleSegments = ReadInt(configuration, HandleSegmentsKey) ?? 3;

        logger.LogInformation(
            "No {CfxHandleKey} configured; subscribing to every {Segments}-segment endpoint on the broker",
            CfxHandleKey,
            handleSegments);

        return CfxTopic.SubscriptionFilterForAnyEndpoint(handleSegments, root);
    }

    private static string? ReadString(DeviceConfiguration configuration, string key) =>
        configuration.DeviceCommunication.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;

    private static int? ReadInt(DeviceConfiguration configuration, string key)
    {
        var raw = ReadString(configuration, key);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!int.TryParse(raw, out var parsed))
        {
            throw new InvalidOperationException(
                $"CFX configuration key '{key}' must be an integer but was '{raw}'.");
        }

        return parsed;
    }

    #endregion
}

using ErrorOr;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Image;

/// <summary>
/// Image protocol parser implementing Pub/Sub pattern.
/// Subscribes to each sensor's MQTT topic (from Parameters.Topic),
/// receives raw image bytes, converts to Base64 TelemetryMeasure via ImageProtocolParser.
/// </summary>
public class ImagePubSubParser : IPubSubProtocolParser
{
    private readonly IPubSub _communication;
    private readonly ImageProtocolParser _parser;
    private readonly ILogger<ImagePubSubParser> _logger;

    /// <summary>
    /// Maps MQTT topic to the corresponding Sensor for routing incoming messages.
    /// </summary>
    private readonly Dictionary<string, Sensor> _topicSensorMap = new();

    private bool _isSubscribed;

    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    public ImagePubSubParser(
        DeviceConfiguration configuration,
        IPubSub communication,
        ILogger<ImagePubSubParser> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parser = new ImageProtocolParser(communication);

        // Build topic -> sensor mapping from each sensor's Topic parameter
        foreach (var sensor in configuration.Sensors)
        {
            if (sensor.Parameters?.TryGetValue("Topic", out var t) == true
                && t?.ToString() is { } topic)
            {
                _topicSensorMap[topic] = sensor;
            }
        }
    }

    #region IProtocolParserCore

    public ICommunication Communication => _communication;

    #endregion

    #region IPubSubProtocolParser

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isSubscribed)
        {
            _logger.LogWarning("Already subscribed to image topics");
            return;
        }

        _communication.MessageReceived += OnMessageReceived;

        foreach (var topic in _topicSensorMap.Keys)
        {
            await _communication.SubscribeAsync(topic, cancellationToken);
            _logger.LogInformation("Subscribed to image topic: {Topic}", topic);
        }

        _isSubscribed = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isSubscribed) return;

        _communication.MessageReceived -= OnMessageReceived;

        foreach (var topic in _topicSensorMap.Keys)
        {
            await _communication.UnsubscribeAsync(topic, cancellationToken);
        }

        _isSubscribed = false;
        _logger.LogInformation("Unsubscribed from all image topics");
    }

    public Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ErrorOr<object>>(
            Error.Failure(description: "Image protocol does not support commands"));
    }

    #endregion

    private void OnMessageReceived(object? sender, MessageReceivedEvent<byte[]> e)
    {
        try
        {
            // Look up the sensor by the incoming topic
            if (!_topicSensorMap.TryGetValue(e.Topic, out var sensor))
            {
                _logger.LogTrace("No sensor mapping for topic {Topic}, skipping", e.Topic);
                return;
            }

            var sensorMapping = new SensorMapping
            {
                FieldToResourceId = new Dictionary<string, string>
                {
                    ["image"] = sensor.ResourceId
                }
            };

            var measures = _parser.ParseSensorData(e.Payload, sensorMapping);

            if (measures.Count > 0)
            {
                _logger.LogDebug(
                    "Parsed image from topic {Topic}: {Size} bytes, contentType={ContentType}",
                    e.Topic, e.Payload.Length,
                    measures[0].Metadata?["contentType"] ?? "unknown");

                OnTelemetryReceived?.Invoke(measures);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing image from topic {Topic}", e.Topic);
        }
    }
}

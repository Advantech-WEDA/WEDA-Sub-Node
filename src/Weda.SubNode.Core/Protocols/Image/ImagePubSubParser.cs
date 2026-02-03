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
/// Subscribes to a single MQTT topic, receives raw image bytes,
/// converts to Base64 TelemetryMeasure via ImageProtocolParser.
/// </summary>
public class ImagePubSubParser : IPubSubProtocolParser
{
    private readonly IPubSub _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly ImageProtocolParser _parser;
    private readonly ILogger<ImagePubSubParser> _logger;
    private readonly string _dataTopic;

    private bool _isSubscribed;

    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    public ImagePubSubParser(
        DeviceConfiguration configuration,
        IPubSub communication,
        ILogger<ImagePubSubParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parser = new ImageProtocolParser(communication);

        _dataTopic = configuration.DeviceCommunication.TryGetValue("Topic", out var topic)
            ? topic?.ToString() ?? "sensor/image/#"
            : "sensor/image/#";
    }

    #region IProtocolParserCore

    public ICommunication Communication => _communication;
    public string ProtocolName => "Image MQTT";
    public IReadOnlyList<string> SupportedDataTypes => ["image/png", "image/jpeg", "application/octet-stream"];
    public bool SupportsBidirectional => false;

    #endregion

    #region IPubSubProtocolParser

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isSubscribed)
        {
            _logger.LogWarning("Already subscribed to image topic");
            return;
        }

        _communication.MessageReceived += OnMessageReceived;
        await _communication.SubscribeAsync(_dataTopic, cancellationToken);
        _isSubscribed = true;

        _logger.LogInformation("Subscribed to image topic: {Topic}", _dataTopic);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isSubscribed) return;

        _communication.MessageReceived -= OnMessageReceived;
        await _communication.UnsubscribeAsync(_dataTopic, cancellationToken);
        _isSubscribed = false;

        _logger.LogInformation("Unsubscribed from image topic");
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
            // Use ImageProtocolParser to convert raw bytes → Base64 TelemetryMeasure
            var sensorMapping = BuildSensorMapping();
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

    private SensorMapping BuildSensorMapping()
    {
        var imageSensor = _configuration.Sensors.FirstOrDefault();
        if (imageSensor == null)
            return new SensorMapping();

        return new SensorMapping
        {
            FieldToResourceId = new Dictionary<string, string>
            {
                ["image"] = imageSensor.ResourceId
            }
        };
    }
}

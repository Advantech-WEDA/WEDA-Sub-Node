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
using Weda.SubNode.Core.Protocols.ISensing.Models;

namespace Weda.SubNode.Core.Protocols.ISensing;

/// <summary>
/// ISensing protocol parser implementing Pub/Sub pattern.
/// Handles MQTT-based ISensing protocol for devices like WISE-4012SE.
/// Parser owns DeviceConfiguration and handles all mapping logic internally.
/// </summary>
public class ISensingPubSubParser : IPubSubProtocolParser
{
    private readonly IPubSub _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly ILogger<ISensingPubSubParser> _logger;
    private readonly string _dataTopic;
    private readonly string _statusTopic;
    private readonly string _ctlTopicPrefix;

    private bool _isSubscribed;

    /// <summary>
    /// Event raised when telemetry data is received from subscription.
    /// </summary>
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    public ISensingPubSubParser(
        DeviceConfiguration configuration,
        IPubSub communication,
        ILogger<ISensingPubSubParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Extract MQTT settings from configuration
        var macAddress = configuration.DeviceCommunication.TryGetValue("MacAddress", out var mac)
            ? mac?.ToString() ?? throw new InvalidOperationException("MacAddress not found in communication configuration")
            : throw new InvalidOperationException("MacAddress not found in communication configuration");

        var manufacturer = configuration.DeviceCommunication.TryGetValue("Manufacturer", out var mfg)
            ? mfg?.ToString() ?? "Advantech"
            : "Advantech";

        // ISensing topic pattern: {Manufacturer}/{MacAddress}/{type}
        _dataTopic = $"{manufacturer}/{macAddress}/data";
        _statusTopic = $"{manufacturer}/{macAddress}/status";
        _ctlTopicPrefix = $"{manufacturer}/{macAddress}/ctl";
    }

    #region IProtocolParserCore Implementation

    public ICommunication Communication => _communication;

    #endregion

    #region IPubSubProtocolParser Implementation

    /// <summary>
    /// Start subscription to receive telemetry data.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isSubscribed)
        {
            _logger.LogWarning("Already subscribed to sensor data. Stop first before restarting.");
            return;
        }

        // Subscribe to message broker events
        _communication.MessageReceived += OnMessageReceived;

        // Subscribe to MQTT topics
        await _communication.SubscribeAsync(_dataTopic, cancellationToken);
        await _communication.SubscribeAsync(_statusTopic, cancellationToken);

        _isSubscribed = true;

        _logger.LogInformation(
            "Subscribed to ISensing topics: {DataTopic}, {StatusTopic}",
            _dataTopic, _statusTopic);
    }

    /// <summary>
    /// Stop subscription and clean up resources.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isSubscribed)
        {
            _logger.LogWarning("Not currently subscribed to sensor data.");
            return;
        }

        // Unsubscribe from message broker events
        _communication.MessageReceived -= OnMessageReceived;

        // Unsubscribe from MQTT topics
        await _communication.UnsubscribeAsync(_dataTopic, cancellationToken);
        await _communication.UnsubscribeAsync(_statusTopic, cancellationToken);

        _isSubscribed = false;

        _logger.LogInformation("Unsubscribed from ISensing topics");
    }

    /// <summary>
    /// Execute command on device (publish to appropriate topic).
    /// For DO control, uses ISensing ctl topic format: {Manufacturer}/{MAC}/ctl/{do_key}
    /// </summary>
    public async Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return command.DeviceCmd switch
            {
                "SetDO" or "SetDigitalOutput" => await ExecuteDigitalOutputControlAsync(command, cancellationToken),
                "SetAO" or "SetAnalogOutput" => await ExecuteAnalogOutputControlAsync(command, cancellationToken),
                _ => Error.Failure(
                        code: "Command.NotSupported",
                        description: $"Command '{command.DeviceCmd}' is not supported by ISensing protocol")
            };
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish command {CommandName}", command.DeviceCmd);
            return Error.Failure(
                code: "Command.PublishFailed",
                description: ex.Message);
        }
    }

    /// <summary>
    /// Execute DO control using ISensing ctl topic format.
    /// Topic: {Manufacturer}/{MAC}/ctl/{do_key}
    /// Payload: {"v": true/false} for output state
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteDigitalOutputControlAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        var outputName = ExtractStringParameter(command.Parameters, ["name", "outputName", "do"]);
        if (string.IsNullOrEmpty(outputName))
        {
            return Error.Validation("SetDO.MissingName", "Missing 'name', 'outputName', or 'do' parameter");
        }

        var state = ExtractBooleanParameter(command.Parameters, "state");
        if (state is null)
        {
            return Error.Validation("SetDO.MissingState", "Missing 'state' parameter");
        }

        // ISensing DO control topic: {Manufacturer}/{MAC}/ctl/{do_key}
        // Convert DO name to lowercase for topic (e.g., "DO2" -> "do2")
        var doKey = outputName.ToLowerInvariant();
        var topic = $"{_ctlTopicPrefix}/{doKey}";

        // ISensing DO control payload: {"v": true/false}
        var payloadObj = new { v = state.Value };
        var json = JsonSerializer.Serialize(payloadObj);
        var payload = Encoding.UTF8.GetBytes(json);

        await _communication.PublishAsync(topic, payload, cancellationToken);

        _logger.LogInformation(
            "Published DO control: {Topic} = {Payload}",
            topic, json);

        return new { success = true, name = outputName, state = state.Value };
    }

    /// <summary>
    /// Execute AO control using ISensing ctl topic format.
    /// Topic: {Manufacturer}/{MAC}/ctl/{ao_key}
    /// Payload: {"v": value} for output state
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteAnalogOutputControlAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        var outputName = ExtractStringParameter(command.Parameters, ["name", "outputName", "ao"]);
        if (string.IsNullOrEmpty(outputName))
        {
            return Error.Validation("SetAO.MissingName", "Missing 'name', 'outputName', or 'ao' parameter");
        }

        var value = ExtractNumericParameter(command.Parameters, "value");
        if (value is null)
        {
            return Error.Validation("SetAO.MissingValue", "Missing 'value' parameter");
        }

        // ISensing AO control topic: {Manufacturer}/{MAC}/ctl/{ao_key}
        // Convert AO name to lowercase for topic (e.g., "AO2" -> "ao2")
        var aoKey = outputName.ToLowerInvariant();
        var topic = $"{_ctlTopicPrefix}/{aoKey}";

        // ISensing AO control payload: {"v": value}
        var payloadObj = new { v = value.Value };
        var json = JsonSerializer.Serialize(payloadObj);
        var payload = Encoding.UTF8.GetBytes(json);

        await _communication.PublishAsync(topic, payload, cancellationToken);

        _logger.LogInformation(
            "Published AO control: {Topic} = {Payload}",
            topic, json);

        return new { success = true, name = outputName, value = value.Value };
    }

    #endregion

    #region Message Handling

    private void OnMessageReceived(object? sender, MessageReceivedEvent<byte[]> e)
    {
        try
        {
            if (e.Topic == _dataTopic || e.Topic.EndsWith("/data"))
            {
                HandleDataMessage(e.Payload);
            }
            else if (e.Topic == _statusTopic || e.Topic.EndsWith("/status"))
            {
                HandleStatusMessage(e.Payload);
            }
            else if (e.Topic.EndsWith("/cmd/response"))
            {
                HandleCommandResponse(e.Payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from topic {Topic}", e.Topic);
        }
    }

    private void HandleDataMessage(byte[] payload)
    {
        try
        {
            var json = Encoding.UTF8.GetString(payload);
            var sensorData = JsonSerializer.Deserialize<ISensingSensorData>(json);

            if (sensorData == null || sensorData.AdditionalData == null || sensorData.AdditionalData.Count == 0)
            {
                _logger.LogWarning("Failed to parse ISensing sensor data");
                return;
            }

            var measures = new List<TelemetryMeasure>();

            // Parse timestamp
            long timestamp = sensorData.Timestamp.ValueKind switch
            {
                JsonValueKind.Number => sensorData.Timestamp.GetInt64(),
                JsonValueKind.String => DateTimeOffset.Parse(sensorData.Timestamp.GetString()!).ToUnixTimeMilliseconds(),
                _ => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Process each sensor field in AdditionalData using DeviceConfiguration for mapping
            foreach (var (fieldName, fieldValue) in sensorData.AdditionalData)
            {
                // Find matching sensor in configuration by channel name
                var sensor = _configuration.Sensors.FirstOrDefault(s =>
                    s.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

                if (sensor == null)
                {
                    _logger.LogTrace("No sensor configuration found for channel {Channel}", fieldName);
                    continue;
                }

                if (!sensor.IsEffectivelyEnabled)
                {
                    _logger.LogTrace("Sensor {SensorName} is disabled, skipping", sensor.Name);
                    continue;
                }

                // Extract value - preserve boolean type for DO sensors, numeric for others
                object value = fieldValue.ValueKind switch
                {
                    JsonValueKind.Number => fieldValue.GetDouble(),
                    JsonValueKind.String when double.TryParse(fieldValue.GetString(), out var d) => d,
                    JsonValueKind.String => fieldValue.GetString()!,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => 0.0
                };

                var measure = new TelemetryMeasure
                {
                    ResourceId = sensor.ResourceId,
                    Value = value,
                    Timestamp = timestamp
                };

                measures.Add(measure);

                _logger.LogTrace(
                    "Parsed ISensing data: {FieldName} = {Value} (ResourceId: {ResourceId}, Quality: {Quality})",
                    fieldName, value, sensor.ResourceId, ISensingQualityCode.MapToQuality(sensorData.QualityCode));
            }

            // Raise event with parsed measures
            if (measures.Count > 0)
            {
                OnTelemetryReceived?.Invoke(measures);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ISensing data message");
        }
    }

    private void HandleStatusMessage(byte[] payload)
    {
        try
        {
            var json = Encoding.UTF8.GetString(payload);
            var statusMessage = JsonSerializer.Deserialize<ConnectionStatusMessage>(json);

            if (statusMessage != null)
            {
                _logger.LogInformation(
                    "Device {DeviceName} ({MacAddress}) status: {Status}",
                    statusMessage.DeviceName,
                    statusMessage.MacAddress,
                    statusMessage.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ISensing status message");
        }
    }

    private void HandleCommandResponse(byte[] payload)
    {
        try
        {
            var json = Encoding.UTF8.GetString(payload);
            _logger.LogDebug("Received command response: {Response}", json);
            // Command responses can be handled by subscribing to specific topics if needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing command response");
        }
    }

    #endregion

    #region Parameter Extraction Helpers (handles JsonElement from JSON deserialization)

    /// <summary>
    /// Extract string parameter from dictionary, handling JsonElement
    /// </summary>
    private static string? ExtractStringParameter(Dictionary<string, object> parameters, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (parameters.TryGetValue(alias, out var value) && value != null)
            {
                if (value is JsonElement jsonElement)
                {
                    return jsonElement.GetString();
                }
                return value.ToString();
            }
        }
        return null;
    }

    /// <summary>
    /// Extract boolean parameter from dictionary, handling JsonElement
    /// </summary>
    private static bool? ExtractBooleanParameter(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => jsonElement.GetInt32() != 0,
                JsonValueKind.String => bool.Parse(jsonElement.GetString()!),
                _ => throw new InvalidOperationException($"Cannot convert JsonElement of kind {jsonElement.ValueKind} to boolean")
            };
        }

        return Convert.ToBoolean(value);
    }

    /// <summary>
    /// Extract numeric parameter from dictionary, handling JsonElement
    /// </summary>
    private static double? ExtractNumericParameter(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.GetDouble(),
                JsonValueKind.String when double.TryParse(jsonElement.GetString(), out var d) => d,
                _ => throw new InvalidOperationException($"Cannot convert JsonElement of kind {jsonElement.ValueKind} to number")
            };
        }

        return Convert.ToDouble(value);
    }

    #endregion
}

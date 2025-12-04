using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.ISensing.Commands;
using Weda.SubNode.Core.Protocols.ISensing.Models;

namespace Weda.SubNode.Core.Protocols.ISensing;

/// <summary>
/// ISensing protocol parser implementing Publish-Subscribe pattern.
/// Handles MQTT-based ISensing protocol for devices like WISE-4012SE.
/// Parser owns DeviceConfiguration and handles all mapping logic internally.
/// </summary>
public class ISensingPubSubParser : IPublishSubscribeProtocolParser
{
    private readonly IMessageBroker _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly ILogger<ISensingPubSubParser> _logger;
    private readonly string _dataTopic;
    private readonly string _statusTopic;
    private readonly string _commandTopic;

    private bool _isSubscribed;

    /// <summary>
    /// Event raised when telemetry data is received from subscription.
    /// </summary>
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    public ISensingPubSubParser(
        DeviceConfiguration configuration,
        IMessageBroker communication,
        ILogger<ISensingPubSubParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Extract MQTT settings from configuration
        var macAddress = configuration.Communication.TryGetValue("MacAddress", out var mac)
            ? mac?.ToString() ?? throw new InvalidOperationException("MacAddress not found in communication configuration")
            : throw new InvalidOperationException("MacAddress not found in communication configuration");

        var manufacturer = configuration.Communication.TryGetValue("Manufacturer", out var mfg)
            ? mfg?.ToString() ?? "Advantech"
            : "Advantech";

        // ISensing topic pattern: {Manufacturer}/{MacAddress}/{type}
        _dataTopic = $"{manufacturer}/{macAddress}/data";
        _statusTopic = $"{manufacturer}/{macAddress}/status";
        _commandTopic = $"{manufacturer}/{macAddress}/cmd";
    }

    #region IProtocolParserCore Implementation

    public ICommunication Communication => _communication;

    public string ProtocolName => "ISensing MQTT";

    public IReadOnlyList<string> SupportedDataTypes => new[]
    {
        "AnalogInput", "DigitalInput", "AnalogOutput", "DigitalOutput",
        "Temperature", "Humidity", "Accelerometer", "Battery", "Geolocation"
    };

    public bool SupportsBidirectional => true;

    #endregion

    #region IPublishSubscribeProtocolParser Implementation

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
    /// Execute command on device (publish to command topic).
    /// </summary>
    public async Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = EncodeCommandToJson(command);
            var payload = Encoding.UTF8.GetBytes(json);

            await _communication.PublishAsync(_commandTopic, payload, cancellationToken);

            _logger.LogInformation(
                "Published command {CommandName} to topic {Topic}",
                command.DeviceCmd, _commandTopic);

            return "Command published successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish command {CommandName}", command.DeviceCmd);
            return Error.Failure(
                code: "Command.PublishFailed",
                description: ex.Message);
        }
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

                if (!sensor.Config.Enabled)
                {
                    _logger.LogTrace("Sensor {SensorName} is disabled, skipping", sensor.Name);
                    continue;
                }

                // Extract numeric value
                double value = fieldValue.ValueKind switch
                {
                    JsonValueKind.Number => fieldValue.GetDouble(),
                    JsonValueKind.String when double.TryParse(fieldValue.GetString(), out var d) => d,
                    JsonValueKind.True => 1.0,
                    JsonValueKind.False => 0.0,
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

    #region Command Encoding

    private string EncodeCommandToJson(DeviceCommand command)
    {
        // Map DeviceCommand to ISensingCommand based on command name
        ISensingCommand isensingCommand = command.DeviceCmd switch
        {
            "SetDigitalOutput" or "SetDO" => new DigitalOutputCommand
            {
                OutputName = ExtractStringParameter(command.Parameters, ["name", "outputName", "do"])
                    ?? throw new ArgumentException("Missing 'name', 'outputName', or 'do' parameter"),
                State = ExtractBooleanParameter(command.Parameters, "state")
                    ?? throw new ArgumentException("Missing 'state' parameter")
            },

            "SetAnalogOutput" or "SetAO" => new AnalogOutputCommand
            {
                OutputName = ExtractStringParameter(command.Parameters, ["name", "outputName", "ao"])
                    ?? throw new ArgumentException("Missing 'name', 'outputName', or 'ao' parameter"),
                Value = ExtractDoubleParameter(command.Parameters, "value")
                    ?? throw new ArgumentException("Missing 'value' parameter")
            },

            "GetConfig" or "GetConfiguration" => new ConfigurationRequestCommand
            {
                Index = ExtractUInt16Parameter(command.Parameters, "index") ?? 0
            },

            "SetConfig" or "SetConfiguration" => new ConfigurationUpdateCommand
            {
                Index = ExtractUInt16Parameter(command.Parameters, "index")
                    ?? throw new ArgumentException("Missing 'index' parameter"),
                ConfigData = command.Parameters.GetValueOrDefault("config") as Dictionary<string, object>
                    ?? throw new ArgumentException("Missing or invalid 'config' parameter")
            },

            "SetSensorEnable" => new SensorEnableCommand
            {
                SensorName = ExtractStringParameter(command.Parameters, ["sensorName"])
                    ?? throw new ArgumentException("Missing 'sensorName' parameter"),
                Enabled = ExtractBooleanParameter(command.Parameters, "enabled")
                    ?? throw new ArgumentException("Missing 'enabled' parameter")
            },

            _ => throw new NotSupportedException($"Command '{command.DeviceCmd}' is not supported by ISensing protocol")
        };

        return JsonSerializer.Serialize(isensingCommand);
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
    /// Extract double parameter from dictionary, handling JsonElement
    /// </summary>
    private static double? ExtractDoubleParameter(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.GetDouble(),
                JsonValueKind.String => double.Parse(jsonElement.GetString()!),
                _ => throw new InvalidOperationException($"Cannot convert JsonElement of kind {jsonElement.ValueKind} to double")
            };
        }

        return Convert.ToDouble(value);
    }

    /// <summary>
    /// Extract ushort parameter from dictionary, handling JsonElement
    /// </summary>
    private static ushort? ExtractUInt16Parameter(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.GetUInt16(),
                JsonValueKind.String => ushort.Parse(jsonElement.GetString()!),
                _ => throw new InvalidOperationException($"Cannot convert JsonElement of kind {jsonElement.ValueKind} to ushort")
            };
        }

        return Convert.ToUInt16(value);
    }

    #endregion
}

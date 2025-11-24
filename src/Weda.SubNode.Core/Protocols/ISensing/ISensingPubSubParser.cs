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
/// Supports both data subscription and command publishing.
/// </summary>
public class ISensingPubSubParser : IPublishSubscribeProtocolParser
{
    private readonly IMessageBroker _communication;
    private readonly ILogger<ISensingPubSubParser> _logger;
    private readonly string _dataTopic;
    private readonly string _statusTopic;
    private readonly string _commandTopic;

    private Action<List<TelemetryMeasure>>? _dataCallback;
    private Action<ErrorOr<object>>? _commandResponseCallback;
    private bool _isSubscribed;

    public ISensingPubSubParser(
        IMessageBroker communication,
        string macAddress,
        ILogger<ISensingPubSubParser> logger,
        string manufacturer = "Advantech")
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(macAddress))
            throw new ArgumentException("MAC address cannot be null or empty", nameof(macAddress));

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

    public async Task SubscribeToSensorDataAsync(
        SensorMapping sensorMapping,
        Action<List<TelemetryMeasure>> callback,
        CancellationToken cancellationToken = default)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        if (_isSubscribed)
        {
            _logger.LogWarning("Already subscribed to sensor data. Unsubscribe first before resubscribing.");
            return;
        }

        _dataCallback = callback;

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

    public async Task UnsubscribeFromSensorDataAsync(CancellationToken cancellationToken = default)
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

        _dataCallback = null;
        _isSubscribed = false;

        _logger.LogInformation("Unsubscribed from ISensing topics");
    }

    public async Task PublishCommandAsync(
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish command {CommandName}", command.DeviceCmd);
            throw;
        }
    }

    public async Task SubscribeToCommandResponseAsync(
        Action<ErrorOr<object>> callback,
        CancellationToken cancellationToken = default)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        _commandResponseCallback = callback;

        // Subscribe to command response topic if needed
        var responseTopic = $"{_commandTopic}/response";
        await _communication.SubscribeAsync(responseTopic, cancellationToken);

        _logger.LogInformation(
            "Subscribed to command response topic: {Topic}",
            responseTopic);
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

            // Process each sensor field in AdditionalData
            foreach (var (fieldName, fieldValue) in sensorData.AdditionalData)
            {
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
                    ResourceId = fieldName, // Will be mapped by SensorMapping in Device layer
                    Value = value,
                    Timestamp = timestamp
                };

                measures.Add(measure);

                _logger.LogTrace(
                    "Parsed ISensing data: {FieldName} = {Value} (Quality: {Quality})",
                    fieldName, value, ISensingQualityCode.MapToQuality(sensorData.QualityCode));
            }

            // Invoke callback with parsed measures
            if (measures.Count > 0)
            {
                _dataCallback?.Invoke(measures);
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
            var response = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (response != null)
            {
                _commandResponseCallback?.Invoke(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing command response");
            _commandResponseCallback?.Invoke(Error.Failure(
                code: "CommandResponse.ParseFailed",
                description: ex.Message));
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
                OutputName = command.Parameters.GetValueOrDefault("outputName")?.ToString()
                    ?? command.Parameters.GetValueOrDefault("do")?.ToString()
                    ?? throw new ArgumentException("Missing 'outputName' or 'do' parameter"),
                State = Convert.ToBoolean(command.Parameters.GetValueOrDefault("state")
                    ?? throw new ArgumentException("Missing 'state' parameter"))
            },

            "SetAnalogOutput" or "SetAO" => new AnalogOutputCommand
            {
                OutputName = command.Parameters.GetValueOrDefault("outputName")?.ToString()
                    ?? command.Parameters.GetValueOrDefault("ao")?.ToString()
                    ?? throw new ArgumentException("Missing 'outputName' or 'ao' parameter"),
                Value = Convert.ToDouble(command.Parameters.GetValueOrDefault("value")
                    ?? throw new ArgumentException("Missing 'value' parameter"))
            },

            "GetConfig" or "GetConfiguration" => new ConfigurationRequestCommand
            {
                Index = Convert.ToUInt16(command.Parameters.GetValueOrDefault("index") ?? 0)
            },

            "SetConfig" or "SetConfiguration" => new ConfigurationUpdateCommand
            {
                Index = Convert.ToUInt16(command.Parameters.GetValueOrDefault("index")
                    ?? throw new ArgumentException("Missing 'index' parameter")),
                ConfigData = command.Parameters.GetValueOrDefault("config") as Dictionary<string, object>
                    ?? throw new ArgumentException("Missing or invalid 'config' parameter")
            },

            "SetSensorEnable" => new SensorEnableCommand
            {
                SensorName = command.Parameters.GetValueOrDefault("sensorName")?.ToString()
                    ?? throw new ArgumentException("Missing 'sensorName' parameter"),
                Enabled = Convert.ToBoolean(command.Parameters.GetValueOrDefault("enabled")
                    ?? throw new ArgumentException("Missing 'enabled' parameter"))
            },

            _ => throw new NotSupportedException($"Command '{command.DeviceCmd}' is not supported by ISensing protocol")
        };

        return JsonSerializer.Serialize(isensingCommand);
    }

    #endregion
}

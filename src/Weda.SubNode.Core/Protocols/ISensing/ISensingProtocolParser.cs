using System.Text;
using System.Text.Json;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.ISensing.Commands;
using Weda.SubNode.Core.Protocols.ISensing.Models;

namespace Weda.SubNode.Core.Protocols.ISensing;

/// <summary>
/// ISensing MQTT protocol parser.
/// Implements IProtocolParser&lt;byte[], object&gt; for ISensing JSON protocol.
/// Supports both Parse (JSON -> TelemetryMeasure) and Encode (TelemetryMeasure -> JSON).
/// </summary>
public class ISensingProtocolParser : IProtocolParser
{
    private readonly IPubSub _communication;

    /// <summary>
    /// Gets the underlying communication instance (MQTT communication for ISensing)
    /// </summary>
    public ICommunication Communication => _communication;

    public ISensingProtocolParser(IPubSub communication)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
    }

    // ===== Low-Level Protocol Operations (IProtocolParser<byte[], object>) =====

    /// <summary>
    /// Parse raw byte array to object (typically ISensingSensorData).
    /// </summary>
    public object Parse(byte[] rawData)
    {
        var json = Encoding.UTF8.GetString(rawData);
        var sensorData = JsonSerializer.Deserialize<ISensingSensorData>(json);
        return sensorData ?? throw new ISensingProtocolException("Failed to parse ISensing data", json);
    }

    /// <summary>
    /// Encode object to byte array (reverse of Parse).
    /// </summary>
    public byte[] Encode(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Encoding.UTF8.GetBytes(json);
    }

    // ===== High-Level Telemetry Operations =====

    public List<TelemetryMeasure> ParseSensorData(byte[] payload, SensorMapping? sensorMapping = null)
    {
        var json = Encoding.UTF8.GetString(payload);
        return ParseSensorData(json, sensorMapping);
    }

    public List<TelemetryMeasure> ParseSensorData(string jsonPayload, SensorMapping? sensorMapping = null)
    {
        var sensorData = JsonSerializer.Deserialize<ISensingSensorData>(jsonPayload);

        if (sensorData == null || sensorData.AdditionalData == null || sensorData.AdditionalData.Count == 0)
        {
            throw new ISensingProtocolException("Failed to parse ISensing sensor data or no sensor data found", jsonPayload);
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

            // Map field name to ResourceId using SensorMapping if provided
            var resourceId = sensorMapping?.FieldToResourceId.GetValueOrDefault(fieldName) ?? fieldName;

            var measure = new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = value,
                Timestamp = timestamp
            };

            measures.Add(measure);
        }

        return measures;
    }

    /// <summary>
    /// Parse connection status message
    /// </summary>
    public ConnectionStatusMessage ParseConnectionStatus(string jsonPayload)
    {
        var statusMessage = JsonSerializer.Deserialize<ConnectionStatusMessage>(jsonPayload);

        if (statusMessage == null)
        {
            throw new ISensingProtocolException("Failed to parse ISensing connection status message", jsonPayload);
        }

        return statusMessage;
    }

    /// <summary>
    /// Check if payload is a connection message
    /// </summary>
    public bool IsConnectionMessage(string jsonPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;

            // Connection messages have "status" and "macid" fields
            return root.TryGetProperty("status", out _) && root.TryGetProperty("macid", out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if payload is a sensor data message
    /// </summary>
    public bool IsSensorDataMessage(string jsonPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;

            // Sensor data messages have standard fields: s (sequence), t (timestamp), q (quality), c (config)
            return root.TryGetProperty("s", out _) &&
                   root.TryGetProperty("t", out _) &&
                   root.TryGetProperty("q", out _);
        }
        catch
        {
            return false;
        }
    }

    // ===== Encode Methods (Internal Format -> ISensing JSON) =====

    public byte[] EncodeSensorData(IEnumerable<TelemetryMeasure> measures)
    {
        var json = EncodeSensorDataToJson(measures);
        return Encoding.UTF8.GetBytes(json);
    }

    public byte[] EncodeCommand(DeviceCommand command)
    {
        var json = EncodeCommandToJson(command);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Encode digital output command
    /// </summary>
    public string EncodeDigitalOutputCommand(string outputName, bool state)
    {
        var command = new DigitalOutputCommand
        {
            OutputName = outputName,
            State = state
        };

        return JsonSerializer.Serialize(command);
    }

    /// <summary>
    /// Encode analog output command
    /// </summary>
    public string EncodeAnalogOutputCommand(string outputName, double value)
    {
        var command = new AnalogOutputCommand
        {
            OutputName = outputName,
            Value = value
        };

        return JsonSerializer.Serialize(command);
    }

    /// <summary>
    /// Encode configuration request (GET/SET)
    /// </summary>
    public string EncodeConfigurationRequest(string operation, string sensorType, object? config = null)
    {
        if (operation.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var command = new ConfigurationRequestCommand();
            return JsonSerializer.Serialize(command);
        }
        else if (operation.Equals("SET", StringComparison.OrdinalIgnoreCase))
        {
            if (config == null || config is not Dictionary<string, object> configDict)
            {
                throw new ArgumentException("Config must be a Dictionary<string, object> for SET operation", nameof(config));
            }

            var command = new ConfigurationUpdateCommand
            {
                Index = 0, // Default to 0
                ConfigData = configDict
            };

            return JsonSerializer.Serialize(command);
        }
        else
        {
            throw new ArgumentException($"Unknown operation: {operation}. Expected 'GET' or 'SET'", nameof(operation));
        }
    }

    // ===== Internal Helper Methods =====

    private string EncodeSensorDataToJson(IEnumerable<TelemetryMeasure> measures)
    {
        // Convert TelemetryMeasure list to ISensing JSON format
        var data = new Dictionary<string, object>
        {
            ["s"] = 0, // Sequence number (placeholder)
            ["t"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["q"] = ISensingQualityCode.Good,
            ["c"] = 0 // Configuration index
        };

        // Add sensor values
        foreach (var measure in measures)
        {
            data[measure.ResourceId] = measure.Value;
        }

        return JsonSerializer.Serialize(data);
    }

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

        // Serialize with options to include all properties
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
        return JsonSerializer.Serialize(isensingCommand, isensingCommand.GetType(), options);
    }

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

/// <summary>
/// ISensing protocol exception
/// </summary>
public class ISensingProtocolException : Exception
{
    public string? Payload { get; init; }

    public ISensingProtocolException(string message, string? payload = null)
        : base(message)
    {
        Payload = payload;
    }

    public ISensingProtocolException(string message, Exception innerException, string? payload = null)
        : base(message, innerException)
    {
        Payload = payload;
    }
}

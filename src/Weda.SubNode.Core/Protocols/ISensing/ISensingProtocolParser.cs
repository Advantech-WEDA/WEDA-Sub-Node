using System.Text;
using System.Text.Json;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.ISensing.Models;

namespace Weda.SubNode.Core.Protocols.ISensing;

/// <summary>
/// ISensing MQTT protocol parser.
/// Implements IProtocolParser&lt;byte[], object&gt; for ISensing JSON protocol.
/// Supports both Parse (JSON -> TelemetryMeasure) and Encode (TelemetryMeasure -> JSON).
/// </summary>
public class ISensingProtocolParser : IProtocolParser
{
    private readonly IMessageBroker _communication;

    /// <summary>
    /// Gets the underlying communication instance (MQTT communication for ISensing)
    /// </summary>
    public ICommunication Communication => _communication;

    public ISensingProtocolParser(IMessageBroker communication)
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
        // TODO: Implement actual JSON parsing with SensorMapping
        // For now, throw NotImplementedException to indicate this needs to be implemented
        throw new NotImplementedException("ParseSensorData with SensorMapping not yet implemented");
    }

    /// <summary>
    /// Parse connection status message
    /// </summary>
    public ConnectionStatusMessage ParseConnectionStatus(string jsonPayload)
    {
        // TODO: Implement connection status parsing
        throw new NotImplementedException("ParseConnectionStatus not yet implemented");
    }

    /// <summary>
    /// Check if payload is a connection message
    /// </summary>
    public bool IsConnectionMessage(string jsonPayload)
    {
        // TODO: Implement message type detection
        throw new NotImplementedException("IsConnectionMessage not yet implemented");
    }

    /// <summary>
    /// Check if payload is a sensor data message
    /// </summary>
    public bool IsSensorDataMessage(string jsonPayload)
    {
        // TODO: Implement message type detection
        throw new NotImplementedException("IsSensorDataMessage not yet implemented");
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
        // TODO: Implement DO command encoding
        throw new NotImplementedException("EncodeDigitalOutputCommand not yet implemented");
    }

    /// <summary>
    /// Encode analog output command
    /// </summary>
    public string EncodeAnalogOutputCommand(string outputName, double value)
    {
        // TODO: Implement AO command encoding
        throw new NotImplementedException("EncodeAnalogOutputCommand not yet implemented");
    }

    /// <summary>
    /// Encode configuration request (GET/SET)
    /// </summary>
    public string EncodeConfigurationRequest(string operation, string sensorType, object? config = null)
    {
        // TODO: Implement configuration request encoding
        throw new NotImplementedException("EncodeConfigurationRequest not yet implemented");
    }

    // ===== Internal Helper Methods =====

    private string EncodeSensorDataToJson(IEnumerable<TelemetryMeasure> measures)
    {
        // TODO: Implement sensor data encoding
        throw new NotImplementedException("EncodeSensorDataToJson not yet implemented");
    }

    private string EncodeCommandToJson(DeviceCommand command)
    {
        // TODO: Implement command encoding
        throw new NotImplementedException("EncodeCommandToJson not yet implemented");
    }
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

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Protocol parser interface for bidirectional protocol conversion
/// Supports both parsing (protocol -> internal format) and encoding (internal format -> protocol)
/// </summary>
public interface IProtocolParser
{
    // ===== Parse Methods (Protocol -> Internal Format) =====

    /// <summary>
    /// Parse protocol payload to telemetry measures
    /// </summary>
    /// <param name="payload">Raw protocol payload</param>
    /// <param name="sensorMapping">Optional sensor mapping for field-to-resourceId conversion</param>
    /// <returns>List of telemetry measures</returns>
    List<TelemetryMeasure> ParseSensorData(byte[] payload, SensorMapping? sensorMapping = null);

    /// <summary>
    /// Parse protocol payload to telemetry measures (string overload)
    /// </summary>
    /// <param name="payload">Protocol payload as string (e.g., JSON)</param>
    /// <param name="sensorMapping">Optional sensor mapping for field-to-resourceId conversion</param>
    /// <returns>List of telemetry measures</returns>
    List<TelemetryMeasure> ParseSensorData(string payload, SensorMapping? sensorMapping = null);

    // ===== Encode Methods (Internal Format -> Protocol) =====

    /// <summary>
    /// Encode telemetry measures to protocol payload
    /// Used for simulation or forwarding scenarios
    /// </summary>
    /// <param name="measures">Telemetry measures to encode</param>
    /// <returns>Protocol payload as byte array</returns>
    byte[] EncodeSensorData(IEnumerable<TelemetryMeasure> measures);

    /// <summary>
    /// Encode device command to protocol payload
    /// </summary>
    /// <param name="command">Device command to encode</param>
    /// <returns>Protocol payload as byte array</returns>
    byte[] EncodeCommand(DeviceCommand command);
}

/// <summary>
/// Sensor mapping for converting protocol field names to ResourceIds
/// </summary>
public class SensorMapping
{
    /// <summary>
    /// Maps protocol field names to ResourceIds (e.g., "ai1" -> "AnalogInput1")
    /// </summary>
    public Dictionary<string, string> FieldToResourceId { get; set; } = new();

    /// <summary>
    /// Maps protocol field names to sensor types (for type-specific parsing)
    /// </summary>
    public Dictionary<string, SensorType> FieldToSensorType { get; set; } = new();

    /// <summary>
    /// Reverse mapping: ResourceId to protocol field name (for encoding)
    /// </summary>
    public Dictionary<string, string> ResourceIdToField
    {
        get
        {
            return FieldToResourceId.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }
    }
}

/// <summary>
/// Sensor type enumeration
/// </summary>
public enum SensorType
{
    Analog,
    Digital,
    Temperature,
    Humidity,
    Accelerometer,
    Battery,
    Geolocation,
    StackLight,
    Other
}

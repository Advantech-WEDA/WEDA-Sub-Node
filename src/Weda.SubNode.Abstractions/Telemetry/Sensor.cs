namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Sensor definition for describing device sensors
/// </summary>
public class Sensor
{
    /// <summary>
    /// Sensor Resource ID, following Device Capability UUID Generation Guideline (e.g., "21af0dc4-5389-a7dd-df64d7cf782c")
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Sensor name or channel identifier (e.g., "ai.channel[0]")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Digital Twin Model Identifier (DTMI) for the sensor
    /// Following DTDL v2 specification (e.g., "dtmi:advantech:EdgeSync:AI;1")
    /// Defines unit and dataType
    /// </summary>
    public required string Dtmi { get; set; }

    /// <summary>
    /// Logical grouping of sensor (e.g., "AI", "DO", "DI")
    /// </summary>
    public SensorGroup SensorGroup { get; set; }

    /// <summary>
    /// Protocol-specific parameters (e.g., Modbus: startAddress, count; MQTT: topic, qos)
    /// Different implementations can use different parameter sets
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Sensor-specific configuration (calibration, DSP, thresholds)
    /// </summary>
    public SensorConfig Config { get; set; } = new();

    /// <summary>
    /// Additional metadata for the sensor
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Reference to parent device resource, following Device Capability UUID Generation Guideline (e.g., "74fe488d5d54-ffff")
    /// </summary>
    public string DeviceResourceId { get; set; } = string.Empty;
}
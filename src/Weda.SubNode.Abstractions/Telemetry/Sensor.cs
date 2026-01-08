namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Sensor definition for describing device sensors.
/// Used both for configuration (from appsettings.json) and for DTDL generation.
/// </summary>
public class Sensor
{
    /// <summary>
    /// Sensor Resource ID, following Device Capability UUID Generation Guideline (e.g., "21af0dc4-5389-a7dd-df64d7cf782c")
    /// Auto-generated if not provided.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Sensor name or channel identifier (e.g., "ai.channel[0]", "temperature.sensor")
    /// Required field.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Digital Twin Model Identifier (DTMI) for the sensor.
    /// Following DTDL v2 specification (e.g., "dtmi:advantech:EdgeSync:AI;1")
    /// - When AutoGenEnabled=true: Auto-generated from Name using short ID generator
    /// - When AutoGenEnabled=false: Required field, must be specified in appsettings.json
    /// </summary>
    public string? Dtmi { get; set; }

    /// <summary>
    /// Logical grouping of sensor (e.g., AI, DO, DI, SYS, TEMP, PWR)
    /// Used to infer default schema type when Schema is not specified.
    /// </summary>
    public SensorGroup SensorGroup { get; set; }

    /// <summary>
    /// DTDL-related information (Schema, DisplayName, Description).
    /// Used for auto-generating DTDL when AutoGenEnabled is true.
    /// </summary>
    public SensorInfo Info { get; set; } = new();

    /// <summary>
    /// Protocol-specific parameters (e.g., Modbus: RegisterAddress, RegisterCount, DataType; MQTT: topic, qos)
    /// Different implementations can use different parameter sets.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Sensor report configuration (sampling interval, transforms, DSP, thresholds)
    /// </summary>
    public SensorReport Report { get; set; } = new();

    /// <summary>
    /// Additional metadata for the sensor.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Reference to parent device resource, following Device Capability UUID Generation Guideline (e.g., "74fe488d5d54-ffff")
    /// Auto-populated during device initialization.
    /// </summary>
    public string DeviceResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the effective schema type for DTDL generation.
    /// Delegates to SensorInfo.GetEffectiveSchema().
    /// </summary>
    public string GetEffectiveSchema() => Info.GetEffectiveSchema(SensorGroup);

    /// <summary>
    /// Gets the effective display name for DTDL generation.
    /// Delegates to SensorInfo.GetEffectiveDisplayName().
    /// </summary>
    public string GetEffectiveDisplayName() => Info.GetEffectiveDisplayName(Name);
}
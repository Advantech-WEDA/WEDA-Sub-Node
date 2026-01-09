using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.ISensing;

/// <summary>
/// WISE-4012SE specific configuration extensions for DeviceConfiguration
/// </summary>
public static class Wise4012SeConfigurationExtensions
{
    /// <summary>
    /// Get MAC Address from Communication settings (required for ISensing MQTT topics)
    /// </summary>
    public static string GetMacAddress(this DeviceConfiguration config)
    {
        return config.DeviceCommunication.TryGetValue("MacAddress", out var mac)
            ? mac?.ToString() ?? throw new InvalidOperationException("MacAddress not found in communication configuration")
            : throw new InvalidOperationException("MacAddress not found in communication configuration");
    }

    /// <summary>
    /// Get Manufacturer from DeviceCommunication settings (defaults to "Advantech")
    /// </summary>
    public static string GetManufacturer(this DeviceConfiguration config)
    {
        return config.DeviceCommunication.TryGetValue("Manufacturer", out var mfg)
            ? mfg?.ToString() ?? "Advantech"
            : "Advantech";
    }

    /// <summary>
    /// Convert Sensor to WISE-4012SE sensor configuration
    /// Maps sensor parameters to ISensing protocol field names
    /// </summary>
    public static Wise4012SeSensor ToWise4012SeSensor(this Sensor sensor)
    {
        var parameters = sensor.Parameters ?? new Dictionary<string, object>();

        return new Wise4012SeSensor
        {
            ResourceId = sensor.ResourceId,
            Name = sensor.Name,
            Dtmi = sensor.Dtmi ?? string.Empty,
            SensorGroup = sensor.SensorGroup,
            FieldName = parameters.TryGetValue("FieldName", out var field) && field != null
                ? field.ToString() ?? sensor.Name
                : sensor.Name,
            SensorType = ParseSensorType(sensor.SensorGroup),
            Unit = sensor.Report.Unit ?? string.Empty,
            Metadata = sensor.Metadata
        };
    }

    /// <summary>
    /// Parse SensorGroup to ISensing SensorType
    /// </summary>
    private static Abstractions.Protocols.SensorType ParseSensorType(SensorGroup sensorGroup)
    {
        return sensorGroup switch
        {
            SensorGroup.AI => Abstractions.Protocols.SensorType.Analog,
            SensorGroup.AO => Abstractions.Protocols.SensorType.Analog,
            SensorGroup.DI => Abstractions.Protocols.SensorType.Digital,
            SensorGroup.DO => Abstractions.Protocols.SensorType.Digital,
            SensorGroup.TEMP => Abstractions.Protocols.SensorType.Temperature,
            SensorGroup.PWR => Abstractions.Protocols.SensorType.Other,
            SensorGroup.SYS => Abstractions.Protocols.SensorType.Other,
            _ => Abstractions.Protocols.SensorType.Other
        };
    }
}

/// <summary>
/// WISE-4012SE sensor configuration (internal representation)
/// Maps between device sensors and ISensing protocol field names
/// </summary>
public class Wise4012SeSensor
{
    /// <summary>
    /// Sensor Resource ID (matches Sensor.ResourceId)
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    /// Sensor name (e.g., "AI0", "DI1")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Digital Twin Model Identifier
    /// </summary>
    public required string Dtmi { get; set; }

    /// <summary>
    /// Sensor group (AI, DI, DO, etc.)
    /// </summary>
    public SensorGroup SensorGroup { get; set; }

    /// <summary>
    /// ISensing protocol field name (e.g., "ai0", "di1")
    /// This is the key used in the ISensing JSON payload
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Sensor type for protocol parsing
    /// </summary>
    public Abstractions.Protocols.SensorType SensorType { get; set; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

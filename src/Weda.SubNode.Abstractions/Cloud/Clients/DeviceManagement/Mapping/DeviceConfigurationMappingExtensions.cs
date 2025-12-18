using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Mapping;

/// <summary>
/// Mapping extensions for DeviceConfiguration to Contract DTOs
/// Manual mapping for optimal performance
/// </summary>
public static class DeviceConfigurationMappingExtensions
{

    /// <summary>
    /// Convert DeviceConfiguration to DeviceConfigurationDto
    /// Used for configuration upload/sync
    /// </summary>
    public static DeviceConfigurationDto ToConfigurationDto(this DeviceConfiguration config)
    {
        if (string.IsNullOrEmpty(config.DeviceId))
        {
            throw new InvalidOperationException(
                "DeviceId must be set before creating configuration DTO.");
        }

        return new DeviceConfigurationDto(
            DeviceId: config.DeviceId!,
            Dtdl: ConvertDtdl(config.Dtdl),
            DeviceCapabilities: config.ToDeviceCapabilitiesDto());
    }

    /// <summary>
    /// Convert DeviceConfiguration to DeviceCapDto
    /// </summary>
    private static DeviceCapDto ToDeviceCapabilitiesDto(this DeviceConfiguration config)
    {
        return new DeviceCapDto(
            Manufacturer: config.Manufacturer,
            Model: config.Model,
            SubNodeType: config.SubNodeType.ToStringValue(),
            SubNodeSwVersion: config.SwVersion,
            DeviceName: config.DeviceName,
            DeviceInfo: config.Metadata,
            Sensors: config.Sensors.Select(s => s.ToSensorDto()).ToList());
    }

    /// <summary>
    /// Convert Sensor to SensorDto (for configuration upload)
    /// </summary>
    private static SensorDto ToSensorDto(this Sensor sensor)
    {
        return new SensorDto(
            ResourceId: sensor.ResourceId,
            Dtmi: sensor.Dtmi,
            Name: sensor.Name,
            SensorGroup: sensor.SensorGroup.ToStringValue(),
            DeviceResourceId: sensor.DeviceResourceId);
    }

    /// <summary>
    /// Convert DTDL object to dictionary
    /// Handles different DTDL object types - flattens structure for cloud API
    /// </summary>
    private static Dictionary<string, object> ConvertDtdl(object? dtdl)
    {
        if (dtdl == null)
        {
            return new Dictionary<string, object>();
        }

        // If already a dictionary, return as-is (already flat)
        if (dtdl is Dictionary<string, object> dict)
        {
            return dict;
        }

        // For other object types (like DTDL model classes), serialize to dictionary
        // Use JsonSerializerOptions to ignore null values
        var options = new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        var json = System.Text.Json.JsonSerializer.Serialize(dtdl, options);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json, options)
            ?? new Dictionary<string, object>();
    }
}

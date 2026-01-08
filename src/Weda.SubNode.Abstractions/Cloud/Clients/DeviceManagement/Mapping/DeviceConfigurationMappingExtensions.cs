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
    public static DeviceConfigurationDto ToConfigurationDto(this DeviceConfigurations configs)
    {
        if (configs.Count == 0)
        {
            throw new InvalidOperationException(
                "DeviceConfigurations must be not empty");
        }

        var enabledConfigs = configs.Values.Where(c => c.Enabled).ToList();
        if (enabledConfigs.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one DeviceConfiguration must be enabled");
        }

        if (enabledConfigs.Any(c => string.IsNullOrEmpty(c.DeviceId)))
        {
            throw new InvalidOperationException(
                "DeviceId must be set before creating configuration DTO.");
        }

        var config = configs.First().Value;
        var subNodeInfo = config.SubNodeInfo ?? throw new InvalidOperationException("SubNodeInfo must be set");

        return new DeviceConfigurationDto(
            DeviceId: config.DeviceId!,
            Dtdl: MergeDtdl(enabledConfigs),
            DeviceCapabilities: ToDeviceCapabilitiesDto(enabledConfigs));
    }    

    /// <summary>
    /// Convert DeviceConfiguration to DeviceCapDto
    /// </summary>
    private static DeviceCapDto ToDeviceCapabilitiesDto(List<DeviceConfiguration> configs)
    {
        var config = configs.First();
        var subNodeInfo = config.SubNodeInfo!;

        var sensors = configs
            .SelectMany(c => c.Sensors)
            .Select(s => s.ToSensorDto())
            .ToList();

        return new DeviceCapDto(
            Manufacturer: subNodeInfo.Manufacturer,
            Model: subNodeInfo.Model,
            SubNodeType: subNodeInfo.SubNodeType.ToStringValue(),
            SubNodeSwVersion: subNodeInfo.SwVersion,
            DeviceName: subNodeInfo.Name,
            DeviceInfo: subNodeInfo.Metadata,
            Sensors: sensors);
    }

    /// <summary>
    /// Convert Sensor to SensorDto (for configuration upload)
    /// </summary>
    private static SensorDto ToSensorDto(this Sensor sensor)
    {
        return new SensorDto(
            ResourceId: sensor.ResourceId,
            Dtmi: sensor.Dtmi!,
            Name: sensor.Name,
            SensorGroup: sensor.SensorGroup.ToStringValue(),
            DeviceResourceId: sensor.DeviceResourceId);
    }

    private static Dictionary<string, object> MergeDtdl(List<DeviceConfiguration> configs)
    {
        var merged = new Dictionary<string, object>();

        foreach (var config in configs)
        {
            var dtdl = ConvertDtdl(config.DtdlInterface);
            foreach (var kvp in dtdl)
            {
                merged.TryAdd(kvp.Key, kvp.Value);
            }
        }   

        return merged;
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

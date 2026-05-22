using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Core.Transforms;

namespace Weda.SubNode.Core.Cloud.Clients.DeviceManagement.Mapping;

/// <summary>
/// Mapping extensions for DeviceConfiguration to Contract DTOs.
/// </summary>
/// <remarks>
/// Located in Core rather than Abstractions because assembling the
/// <see cref="SubNodeCapabilitiesDto"/> requires pulling descriptors from
/// <see cref="TransformFactory"/>, <see cref="DspFilterFactory"/>, and
/// <see cref="CommandRegistry"/> — all of which live in Core.
/// </remarks>
public static class DeviceConfigurationMappingExtensions
{
    /// <summary>
    /// Convert <see cref="DeviceConfigurations"/> to <see cref="DeviceConfigurationDto"/> for upload.
    /// </summary>
    /// <param name="configs">Device configurations from the SubNode.</param>
    /// <param name="commandRegistry">
    /// Optional. When provided, command descriptors are included in
    /// <c>SubNodeCapabilitiesDto.Commands</c>. When null, commands list is empty.
    /// Transform and DSP filter descriptors are always pulled from the static
    /// factories.
    /// </param>
    public static DeviceConfigurationDto ToConfigurationDto(
        this DeviceConfigurations configs,
        CommandRegistry? commandRegistry = null)
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
        _ = config.SubNodeInfo ?? throw new InvalidOperationException("SubNodeInfo must be set");

        return new DeviceConfigurationDto(
            DeviceId: config.DeviceId!,
            Dtdl: MergeDtdl(enabledConfigs),
            DeviceCapabilities: ToDeviceCapabilitiesDto(enabledConfigs, commandRegistry));
    }

    private static DeviceCapDto ToDeviceCapabilitiesDto(
        List<DeviceConfiguration> configs,
        CommandRegistry? commandRegistry)
    {
        var config = configs.First();
        var subNodeInfo = config.SubNodeInfo!;

        var sensors = configs
            .SelectMany(c => c.Sensors)
            .Select(s => s.ToSensorDto())
            .ToList();

        var capabilities = new SubNodeCapabilitiesDto(
            Transforms: TransformFactory.GetDescriptors(),
            DspFilters: DspFilterFactory.GetDescriptors(),
            Commands: commandRegistry?.GetDescriptors() ?? []);

        return new DeviceCapDto(
            Manufacturer: subNodeInfo.Manufacturer,
            Model: subNodeInfo.Model,
            SubNodeType: subNodeInfo.SubNodeType.ToStringValue(),
            SubNodeSwVersion: subNodeInfo.SwVersion,
            DeviceName: subNodeInfo.Name,
            DeviceInfo: subNodeInfo.Metadata,
            Sensors: sensors,
            Capabilities: capabilities);
    }

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

    private static Dictionary<string, object> ConvertDtdl(object? dtdl)
    {
        if (dtdl == null)
        {
            return new Dictionary<string, object>();
        }

        if (dtdl is Dictionary<string, object> dict)
        {
            return dict;
        }

        var options = new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(dtdl, options);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json, options)
            ?? new Dictionary<string, object>();
    }
}

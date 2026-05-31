using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Core.Telemetry;
using Weda.SubNode.Core.Transforms;

namespace Weda.SubNode.Core.Cloud.Clients.DeviceManagement.Mapping;

/// <summary>
/// Mapping extensions for DeviceConfiguration to Contract DTOs.
/// </summary>
/// <remarks>
/// Located in Core rather than Abstractions because assembling the
/// <see cref="DeviceCapDto"/> requires pulling descriptors from
/// <see cref="TransformFactory"/>, <see cref="DspFilterFactory"/>, and
/// <see cref="CommandRegistry"/> — all of which live in Core.
/// <para>Layering:
/// <c>Dtdl</c> = full DTDL Interface definitions (sensor telemetry + every
/// transform / DSP / command schema). <c>DeviceCapabilities.Sensors</c> = sensor
/// entities (resourceId etc.). <c>DeviceCapabilities.Transforms / DspFilters /
/// Commands</c> = thin <c>{name, dtmi}</c> catalog references into <c>Dtdl</c>.</para>
/// </remarks>
public static class DeviceConfigurationMappingExtensions
{
    private static readonly JsonSerializerOptions DtdlJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Convert <see cref="DeviceConfigurations"/> to <see cref="DeviceConfigurationDto"/> for upload.
    /// </summary>
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

        var transformDescriptors = TransformFactory.GetDescriptors();
        var dspDescriptors = DspFilterFactory.GetDescriptors();
        var commandDescriptors = commandRegistry?.GetDescriptors() ?? [];
        var deviceTypes = DeviceTypeRegistry.All;
        var sensorTypes = SensorTypeRegistry.All;

        return new DeviceConfigurationDto(
            DeviceId: config.DeviceId!,
            Dtdl: BuildDtdlList(enabledConfigs, transformDescriptors, dspDescriptors,
                                commandDescriptors, deviceTypes, sensorTypes),
            DeviceCapabilities: ToDeviceCapabilitiesDto(
                enabledConfigs, transformDescriptors, dspDescriptors, commandDescriptors,
                deviceTypes, sensorTypes));
    }

    private static DeviceCapDto ToDeviceCapabilitiesDto(
        List<DeviceConfiguration> configs,
        IReadOnlyList<TransformDescriptorDto> transformDescriptors,
        IReadOnlyList<DspFilterDescriptorDto> dspDescriptors,
        IReadOnlyList<CommandDescriptorDto> commandDescriptors,
        IReadOnlyList<DeviceTypeRegistry.DeviceTypeRegistration> deviceTypes,
        IReadOnlyList<SensorTypeRegistry.SensorTypeRegistration> sensorTypes)
    {
        var subNodeInfo = configs.First().SubNodeInfo!;

        var sensors = configs
            .SelectMany(c => c.Sensors)
            .Select(s => s.ToSensorDto())
            .ToList();

        var devices = deviceTypes
            .Select(d => new CatalogRefDto(d.DeviceTypeName, d.Dtmi))
            .ToList();

        var sensorTypeRefs = sensorTypes
            .Select(s => new SensorTypeCatalogRefDto(s.SensorTypeName, s.Dtmi, s.DeviceTypeName))
            .ToList();

        var transforms = transformDescriptors
            .Select(d => new CatalogRefDto(d.TypeName, ExtractDtmi(d.ParameterSchema)))
            .ToList();

        var dspFilters = dspDescriptors
            .Select(d => new CatalogRefDto(d.TypeName, ExtractDtmi(d.ParameterSchema)))
            .ToList();

        var commands = commandDescriptors
            .Select(d => new CatalogRefDto(d.Name, ExtractDtmi(d.Schema)))
            .ToList();

        return new DeviceCapDto(
            Manufacturer: subNodeInfo.Manufacturer,
            Model: subNodeInfo.Model,
            SubNodeType: subNodeInfo.SubNodeType.ToStringValue(),
            SubNodeSwVersion: subNodeInfo.SwVersion,
            DeviceName: subNodeInfo.Name,
            DeviceInfo: subNodeInfo.Metadata,
            Sensors: sensors,
            Devices: devices,
            SensorTypes: sensorTypeRefs,
            Transforms: transforms,
            DspFilters: dspFilters,
            Commands: commands);
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

    private static List<JsonObject> BuildDtdlList(
        List<DeviceConfiguration> configs,
        IReadOnlyList<TransformDescriptorDto> transformDescriptors,
        IReadOnlyList<DspFilterDescriptorDto> dspDescriptors,
        IReadOnlyList<CommandDescriptorDto> commandDescriptors,
        IReadOnlyList<DeviceTypeRegistry.DeviceTypeRegistration> deviceTypes,
        IReadOnlyList<SensorTypeRegistry.SensorTypeRegistration> sensorTypes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var dtdl = new List<JsonObject>();

        // 1. Per-device sensor telemetry Interfaces from the untyped (legacy)
        //    DtdlGenerator / loaded-file path. Typed devices have null
        //    DtdlInterface (their schema lives in the sensor-type Interfaces below).
        foreach (var config in configs)
        {
            if (ToJsonObject(config.DtdlInterface) is { } iface) AddIfNew(iface);
        }

        // 2. Strong-typed device + sensor catalog Interfaces (Phase 1 Track B).
        //    Sensor:base is added on first sensor-type extends-target, dedup keeps it singular.
        if (sensorTypes.Count > 0) AddIfNew(SensorBase.GetInterface());
        foreach (var d in deviceTypes) AddIfNew(d.Schema);
        foreach (var s in sensorTypes) AddIfNew(s.Schema);

        // 3. Capability Interfaces: transform / DSP / command.
        foreach (var d in transformDescriptors) AddIfNew(d.ParameterSchema);
        foreach (var d in dspDescriptors) AddIfNew(d.ParameterSchema);
        foreach (var d in commandDescriptors) AddIfNew(d.Schema);

        return dtdl;

        void AddIfNew(JsonObject iface)
        {
            var id = iface["@id"]?.GetValue<string>();
            if (id is null) return;
            if (seen.Add(id)) dtdl.Add(iface);
        }
    }

    private static JsonObject? ToJsonObject(object? dtdl)
    {
        if (dtdl is null) return null;
        if (dtdl is JsonObject jo) return jo;
        var json = JsonSerializer.Serialize(dtdl, DtdlJsonOpts);
        return JsonNode.Parse(json) as JsonObject;
    }

    private static string ExtractDtmi(JsonObject schema)
    {
        return schema["@id"]?.GetValue<string>()
            ?? throw new InvalidOperationException(
                "DTDL Interface is missing required '@id' field; cannot derive catalog dtmi.");
    }
}

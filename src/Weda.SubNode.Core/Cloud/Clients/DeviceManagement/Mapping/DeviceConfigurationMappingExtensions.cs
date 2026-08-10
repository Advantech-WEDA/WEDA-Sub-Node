using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Context;
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
        CommandRegistry? commandRegistry = null,
        ProjectInfo? projectInfo = null)
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
        var subNodeInfo = config.SubNodeInfo 
            ?? throw new InvalidOperationException("SubNodeInfo must be set");

        var transformDescriptors = TransformFactory.GetDescriptors();
        var dspDescriptors = DspFilterFactory.GetDescriptors();
        var commandDescriptors = commandRegistry?.GetDescriptors() ?? [];
        var deviceTypes = DeviceTypeRegistry.All;
        var sensorTypes = SensorTypeRegistry.All;

        return new DeviceConfigurationDto(
            DeviceId: config.DeviceId!,
            Dtdl: BuildWrapperInterface(enabledConfigs, projectInfo ?? ProjectInfo.Empty, config.DeviceId!, subNodeInfo),
            RefModels: [], // remove to reduce capa
            RefModelsMap: BuildRefModelsMap(transformDescriptors, dspDescriptors, commandDescriptors, deviceTypes, sensorTypes),
            DeviceCapabilities: ToDeviceCapabilitiesDto(
                enabledConfigs, transformDescriptors, dspDescriptors, commandDescriptors,
                deviceTypes, sensorTypes));
    }

    /// <summary>
    /// Build the SubNode wrapper interface - a single DTDL v3 Interface that aggregates
    /// every enabled device's sensor Telemetries into <c>contents</c>. Maps to cloud's
    /// <c>DtdlModel.DeviceModel</c>
    /// </summary>
    private static JsonObject BuildWrapperInterface(
        List<DeviceConfiguration> configs,
        ProjectInfo projectInfo,
        string deviceId,
        SubNodeInfo subNodeInfo)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var contents = new JsonArray();

        foreach (var config in configs)
        {
            if (ToJsonObject(config.DtdlInterface) is not { } iface) continue;
            if (iface["contents"] is not JsonArray ifaceContents) continue;

            foreach (var item in ifaceContents)
            {
                if (item is not JsonObject content) continue;
                var id = content["@id"]?.GetValue<string>();
                if (id is null || !seen.Add(id)) continue;

                // DTDL requires content names to be unique within an Interface.
                // Device-namespaced DTMIs (dtmi:sub:<device>:...) no longer collide
                // on @id when two devices expose a same-named sensor, so guard on
                // name too — first device wins, matching the old dedup behavior.
                var name = content["name"]?.GetValue<string>();
                if (name is not null && !seenNames.Add(name)) continue;

                // Detach from old parent (DtdlInterface) before adding to wrapper
                contents.Add(content.DeepClone());
            }
        }

        return new JsonObject
        {
            ["@context"] = "dtmi:dtdl:context;3",
            ["@id"] = BuildSubNodeDtmi(deviceId),
            ["@type"] = "Interface",
            ["displayName"] = projectInfo.Name ?? subNodeInfo.Name,
            ["description"] = projectInfo.Description ?? string.Empty,
            ["contents"] = contents
        };
    }

    private static List<JsonObject> BuildRefModels(
        IReadOnlyList<TransformDescriptorDto> transforms,
        IReadOnlyList<DspFilterDescriptorDto> dspFilters,
        IReadOnlyList<CommandDescriptorDto> commands,
        IReadOnlyList<DeviceTypeRegistry.DeviceTypeRegistration> deviceTypes,
        IReadOnlyList<SensorTypeRegistry.SensorTypeRegistration> sensorTypes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var refModels = new List<JsonObject>();
        
        void AddIfNew(JsonObject iface)
        {
            var id = iface["@id"]?.GetValue<string>();
            if (id is null) return;
            if (seen.Add(id)) refModels.Add(iface);
        }

        if (sensorTypes.Count > 0) AddIfNew(SensorBase.GetInterface());
        if (deviceTypes.Count > 0) AddIfNew(DeviceBaseDtdl.GetInterface());
        foreach (var d in deviceTypes) AddIfNew(d.Schema);
        foreach (var s in sensorTypes) AddIfNew(s.Schema);
        foreach (var t in transforms) AddIfNew(t.ParameterSchema);
        foreach (var f in dspFilters) AddIfNew(f.ParameterSchema);
        foreach (var c in commands) AddIfNew(c.Schema);

        return refModels;
    }

    private static RefModelsMapDto BuildRefModelsMap(
        IReadOnlyList<TransformDescriptorDto> transforms,
        IReadOnlyList<DspFilterDescriptorDto> dspFilters,
        IReadOnlyList<CommandDescriptorDto> commands,
        IReadOnlyList<DeviceTypeRegistry.DeviceTypeRegistration> deviceTypes,
        IReadOnlyList<SensorTypeRegistry.SensorTypeRegistration> sensorTypes)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var configBucket = new List<JsonObject>();
        var commandBucket = new List<JsonObject>();

        void Add(List<JsonObject> bucket, JsonObject iface)
        {
            var id = iface["@id"]?.GetValue<string>();
            if (id is null || !seen.Add(id)) return;
            bucket.Add(iface.DeepClone().AsObject());
        }

        if (sensorTypes.Count > 0) Add(configBucket, SensorBase.GetInterface());
        if (deviceTypes.Count > 0) Add(configBucket, DeviceBaseDtdl.GetInterface());

        foreach (var el in deviceTypes) Add(configBucket, el.Schema);
        foreach (var el in sensorTypes) Add(configBucket, el.Schema);
        foreach (var el in transforms) Add(configBucket, el.ParameterSchema);
        foreach (var el in dspFilters) Add(configBucket, el.ParameterSchema);

        foreach (var cmd in commands) Add(commandBucket, cmd.Schema);
        
        return new RefModelsMapDto(
            Configs: configBucket,
            Commands: commandBucket);
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

        var deviceConfigs = configs
            .Select(cfg =>
            {
                var reg = DeviceTypeRegistry.Get(cfg.DeviceTypeName ?? "");
                return reg is null ? null : new CatalogRefDto(cfg.DeviceName, reg.Dtmi);
            })
            .Where(r => r is not null)
            .Cast<CatalogRefDto>()
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
            Commands: commands)
        {
            DeviceConfigs = deviceConfigs,
            SdkVersion = subNodeInfo.SdkVersion,
            SchemaVersion = subNodeInfo.SchemaVersion
        };
    }

    private static SensorDto ToSensorDto(this Sensor sensor)
    {
        return new SensorDto(
            ResourceId: sensor.ResourceId,
            Dtmi: sensor.Dtmi!,
            Name: sensor.Name,
            SensorGroup: sensor.SensorGroup.ToStringValue(),
            DeviceResourceId: sensor.DeviceResourceId,
            // Unit lives on the SensorDto (not in the DTDL Telemetry) because
            // core DTDL v3 does not define `unit` on a bare Telemetry — see
            // DtdlGenerator.GenerateTelemetryContent. Cloud + front-end keep
            // access via deviceCapabilities.sensors[i].unit.
            Unit: string.IsNullOrEmpty(sensor.Report.Unit) ? null : sensor.Report.Unit);
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

    private static string BuildSubNodeDtmi(string deviceId)
    {
        const int rawSegmentBudget = 32;

        var safeRaw = deviceId.Length > 0
            && deviceId.Length <= rawSegmentBudget
            && deviceId.All(c => char.IsLetterOrDigit(c) || c == '_')
            && char.IsLetterOrDigit(deviceId[^1]);

        var segment = safeRaw ? deviceId : ShortHashOf(deviceId);
        return $"dtmi:advantech:edgesync:subnode_{segment};1";
    }

    private static string ShortHashOf(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }
}

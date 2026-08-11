using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Cloud.Clients.Common;
using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Configuration upload request
/// Used for uploading complete device configuration to cloud
/// </summary>
public class ConfigurationUploadRequest : Request<DeviceConfigurationDto>
{
    /// <summary>
    /// Create a new configuration upload request with auto-generated audit fields
    /// </summary>
    public static ConfigurationUploadRequest Create(DeviceConfigurationDto data)
    {
        return Create<ConfigurationUploadRequest>(data);
    }
}

public record SensorDto(
    [property: JsonPropertyName("resourceId")] string ResourceId,
    [property: JsonPropertyName("dtmi")] string Dtmi,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sensorGroup")] string SensorGroup,
    [property: JsonPropertyName("deviceResourceId")] string DeviceResourceId,
    [property: JsonPropertyName("unit")] string? Unit);

public record DeviceCapDto(
    [property: JsonPropertyName("manufacturer")] string Manufacturer,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("deviceType")] string SubNodeType,
    [property: JsonPropertyName("subDeviceSwVersion")] string SubNodeSwVersion,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("deviceInfo")] Dictionary<string, object> DeviceInfo,
    [property: JsonPropertyName("sensors")] IReadOnlyList<SensorDto> Sensors,
    [property: JsonPropertyName("devices")] IReadOnlyList<CatalogRefDto> Devices,
    [property: JsonPropertyName("sensorTypes")] IReadOnlyList<SensorTypeCatalogRefDto> SensorTypes,
    [property: JsonPropertyName("transforms")] IReadOnlyList<CatalogRefDto> Transforms,
    [property: JsonPropertyName("dspFilters")] IReadOnlyList<CatalogRefDto> DspFilters,
    [property: JsonPropertyName("commands")] IReadOnlyList<CatalogRefDto> Commands)
{
    // Non-positional init property — does not change the constructor signature.
    // Defaults to empty list so existing new DeviceCapDto(...) call sites compile unchanged.
    // device-agent (Go) silently ignores unknown JSON fields, so this addition is wire-safe.
    [JsonPropertyName("deviceConfigs")]
    public IReadOnlyList<CatalogRefDto> DeviceConfigs { get; init; } = [];

    // Non-positional init properties — additive, do not change the constructor signature.
    // sdkVersion carries the SubNode SDK package version; schemaVersion marks the
    // camelCase wire-contract version. device-agent (Go) ignores unknown fields, so wire-safe.
    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; init; } = string.Empty;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = SubNodeInfo.CurrentSchemaVersion;
}

/// <summary>
/// Materialized grouping of <see cref="DeviceConfigurationDto.RefModels"/> 
/// for consumers that want a category slice without inferring from @id / extends.
/// </summary>
public record RefModelsMapDto(
    [property: JsonPropertyName("configs")] IReadOnlyList<JsonObject> Configs,
    [property: JsonPropertyName("commands")] IReadOnlyList<JsonObject> Commands);

/// <summary>
/// Device configuration data for upload (v1.2 shape).
/// <para><c>Dtdl</c> is the SubNode wrapper Interface (single DTDL v3 Interface)
/// with every sensor flattened into <c>contents</c> as Telemetry. Cloud maps
/// this to <c>DtdlModel.DeviceModel</c>.</para>
/// <para><c>RefModels</c> is <b>obsolete</b>. It used to carry the full typed
/// catalog DTDL (<c>Sensor:base</c>, every device-type / sensor-type / transform /
/// DSP / command Interface) — a heavy, redundant payload. The catalog is now
/// resolved cloud-side from <c>refModelsMap</c> (backed by the shared
/// <c>Weda.Dtdl</c> package), so the SubNode always uploads an empty array to
/// avoid wasting message bandwidth. Kept on the wire for backward compatibility.</para>
/// <para><c>DeviceCapabilities</c> carries instance state (sensor entities) and thin
/// <c>{name, dtmi}</c> catalog references — the map cloud uses to resolve models.</para>
/// </summary>
public record DeviceConfigurationDto(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("dtdl")] JsonObject Dtdl,
    [property: JsonPropertyName("refModels")]
    [property: Obsolete("refModels no longer carries content; the SubNode always uploads []. The typed catalog is resolved cloud-side via refModelsMap. Do not read or populate this field.")]
    IReadOnlyList<JsonObject> RefModels,
    [property: JsonPropertyName("refModelsMap")] RefModelsMapDto RefModelsMap,
    [property: JsonPropertyName("deviceCapabilities")] DeviceCapDto DeviceCapabilities);

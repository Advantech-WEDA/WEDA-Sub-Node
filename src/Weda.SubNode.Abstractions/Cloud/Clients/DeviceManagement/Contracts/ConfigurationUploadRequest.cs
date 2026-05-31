using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Cloud.Clients.Common;

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
    [property: JsonPropertyName("deviceResourceId")] string DeviceResourceId);

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
    [property: JsonPropertyName("commands")] IReadOnlyList<CatalogRefDto> Commands);

/// <summary>
/// Device configuration data for upload.
/// <para><c>Dtdl</c> is a flat list of every DTDL v3 Interface this SubNode contributes
/// — sensor telemetry, plus one entry per transform / DSP filter / command schema.
/// <c>DeviceCapabilities</c> carries instance state (sensor entities) and thin
/// <c>{name, dtmi}</c> catalog references into <c>Dtdl</c>.</para>
/// </summary>
public record DeviceConfigurationDto(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("dtdl")] IReadOnlyList<JsonObject> Dtdl,
    [property: JsonPropertyName("deviceCapabilities")] DeviceCapDto DeviceCapabilities);

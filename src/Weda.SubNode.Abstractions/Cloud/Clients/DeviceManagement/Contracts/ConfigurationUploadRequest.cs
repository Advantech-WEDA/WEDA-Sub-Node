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
    [property: JsonPropertyName("deviceType")] string DeviceType,
    [property: JsonPropertyName("subDeviceSwVersion")] string SubNodeSwVersion,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("deviceInfo")] Dictionary<string, object> DeviceInfo,
    [property: JsonPropertyName("sensors")] IReadOnlyList<SensorDto> Sensors);

/// <summary>
/// Device configuration data for upload
/// Contains complete device metadata and sensors
/// </summary>
public record DeviceConfigurationDto(
    [property: JsonPropertyName("deviceId")] string DeviceId,
    [property: JsonPropertyName("dtdl")] Dictionary<string, object> Dtdl,
    [property: JsonPropertyName("deviceCapabilities")] DeviceCapDto DeviceCapabilities);

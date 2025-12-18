using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Cloud.Clients.Common;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Device registration request
/// Used for full device registration after obtaining device ID
/// </summary>
public class DeviceRegistrationRequest : Request<DeviceRegistrationDto>
{
    /// <summary>
    /// Create a new device registration request from DeviceConfiguration
    /// Only DeviceName and SubNodeType are used for registration
    /// </summary>
    public static DeviceRegistrationRequest Create(DeviceInfo info)
    {
        var dto = new DeviceRegistrationDto(info.DeviceId, info.DeviceName, info.SubNodeType.ToStringValue());
        return Create<DeviceRegistrationRequest>(dto);
    }
}

/// <summary>
/// Device registration data
/// Contains DeviceName and SubNodeType
/// </summary>
public record DeviceRegistrationDto(
    [property: JsonPropertyName("deviceId")] string? DeviceId,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("deviceType")] string SubNodeType);
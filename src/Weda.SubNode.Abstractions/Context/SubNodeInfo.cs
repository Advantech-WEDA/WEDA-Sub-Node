using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Information about the Sub-Node instance.
/// A Sub-Node is a single dotnet program that may manage multiple internal devices.
/// From the cloud's perspective, the entire Sub-Node is treated as a single "virtual device"
/// with one globally unique DeviceId.
/// </summary>
/// <remarks>
/// <para>
/// <b>WEDA Device Management Rules:</b>
/// </para>
/// <list type="bullet">
/// <item>Each Sub-Node has a globally unique DeviceId within WEDA Core</item>
/// <item>Each internal device has a DeviceName that is unique within the Sub-Node</item>
/// <item>Sensor ResourceIds are generated using: sha1(SubNode.DeviceId + DeviceName + SensorName)</item>
/// </list>
/// </remarks>
public class SubNodeInfo
{
    /// <summary>
    /// Gets or sets the Sub-Node name used for cloud registration.
    /// This name identifies the Sub-Node when registering with WEDA Core.
    /// </summary>
    /// <example>"MyFactorySubNode"</example>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the globally unique Device ID assigned by WEDA Core during registration.
    /// This ID is used for all cloud communications (telemetry, commands, config updates).
    /// </summary>
    /// <remarks>
    /// This value is null before registration and populated after successful registration.
    /// Once registered, it is cached locally in .weda/subnode.registration.json.
    /// </remarks>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the manufacturer name.
    /// </summary>
    public string Manufacturer { get; set; } = "Advantech";

    /// <summary>
    /// Gets or sets the model name.
    /// </summary>
    public string Model { get; set; } = "SubNode-SDK";

    /// <summary>
    /// Gets or sets the software version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the device type for cloud registration.
    /// Default is CustomDevice.
    /// </summary>
    public DeviceType DeviceType { get; set; } = DeviceType.CustomDevice;

    /// <summary>
    /// Gets whether the Sub-Node has been registered with the cloud.
    /// </summary>
    public bool IsRegistered => !string.IsNullOrEmpty(DeviceId);
}

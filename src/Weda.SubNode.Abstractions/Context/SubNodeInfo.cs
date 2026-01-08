
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
/// <item>Each Sub-Node has a globally unique DeviceId within WEDA Node</item>
/// <item>Each internal device has a DeviceName that is unique within the Sub-Node</item>
/// <item>Sensor ResourceIds are generated using: sha1(SubNode.DeviceId + DeviceName + SensorName)</item>
/// </list>
/// </remarks>
public class SubNodeInfo
{
    /// <summary>
    /// Gets or sets the Sub-Node name used for cloud registration.
    /// This name identifies the Sub-Node when registering with WEDA Node.
    /// </summary>
    /// <example>"MyFactorySubNode"</example>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the globally unique Device ID assigned by WEDA Node during registration.
    /// This ID is used for all cloud communications (telemetry, commands, config updates).
    /// </summary>
    /// <remarks>
    /// This value is null before registration and populated after successful registration.
    /// Once registered, it is cached locally in .weda/subnode.registration.json.
    /// </remarks>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Alias of Sub-Node unique identifier.
    /// </summary>
    public string? Id => DeviceId;

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
    public string SwVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the device type for cloud registration.
    /// Default is CustomDevice.
    /// </summary>
    public SubNodeType SubNodeType { get; set; } = SubNodeType.CustomDevice;

    /// <summary>
    /// Gets or sets whether to automatically generate DTDL content from sensor definitions.
    /// When true:
    /// - DTDL is auto-generated based on Sensor definitions (Schema, DisplayName, Description)
    /// - Sensor.Dtmi is auto-generated using short ID generator
    /// - DtdlPath is optional (ignored if specified)
    /// When false:
    /// - DTDL must be loaded from DtdlPath
    /// - Each Sensor must have Dtmi specified
    /// Default is false for backwards compatibility.
    /// </summary>
    public bool AutoGenEnabled { get; set; } = false;

    /// <summary>
    /// Gets whether the Sub-Node has been registered with the cloud.
    /// </summary>
    public bool IsRegistered => !string.IsNullOrEmpty(DeviceId);
    
    /// <summary>
    /// The metadata of Sub-Node
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}

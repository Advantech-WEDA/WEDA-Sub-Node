using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Configuration for the Sub-Node loaded from devicecfg.json.
/// Located under DeviceConfig section (loaded from devicecfg.json).
/// </summary>
/// <example>
/// devicecfg.json:
/// <code>
/// {
///   "SubNode": {
///     "Name": "MyFactorySubNode",
///     "SubNodeType": "CustomDevice",
///     "Manufacturer": "Advantech",
///     "Model": "SubNode-SDK",
///     "Version": "1.0.0"
///   },
///   "DeviceConfigs": { ... }
/// }
/// </code>
/// </example>
public class SubNodeConfiguration
{
    /// <summary>
    /// The configuration section name.
    /// Located under DeviceConfig section (loaded from devicecfg.json).
    /// </summary>
    public const string SectionName = "DeviceConfig:SubNode";

    /// <summary>
    /// Gets or sets the Sub-Node name used for cloud registration.
    /// This name identifies the Sub-Node when registering with WEDA Core.
    /// </summary>
    /// <remarks>
    /// If not specified, defaults to the assembly name of the entry assembly.
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the device type for cloud registration.
    /// Valid values: AdamEthernet, SerialDevice, DaqDevice, SystemMonitor, CustomDevice
    /// Default is CustomDevice.
    /// </summary>
    public SubNodeType SubNodeType { get; set; } = SubNodeType.CustomDevice;

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
    /// Gets or sets whether to automatically generate DTDL content from sensor definitions.
    /// When true, DTDL is auto-generated based on Sensors[].SensorGroup and Sensors[].Parameters.
    /// When false, loads DTDL from the path specified in DeviceConfigs[].DtdlPath.
    /// Default is false.
    /// </summary>
    public bool AutoGenDtdl { get; set; } = false;

    /// <summary>
    /// Converts this configuration to a SubNodeInfo instance.
    /// </summary>
    /// <param name="fallbackName">Fallback name if Name is not configured (e.g., assembly name)</param>
    /// <returns>SubNodeInfo populated from this configuration</returns>
    public SubNodeInfo ToSubNodeInfo(string? fallbackName = null)
    {
        return new SubNodeInfo
        {
            Name = !string.IsNullOrWhiteSpace(Name) ? Name : (fallbackName ?? "SubNode"),
            SubNodeType = SubNodeType,
            Manufacturer = Manufacturer,
            Model = Model,
            SwVersion = SwVersion,
            AutoGenDtdl = AutoGenDtdl
        };
    }
}

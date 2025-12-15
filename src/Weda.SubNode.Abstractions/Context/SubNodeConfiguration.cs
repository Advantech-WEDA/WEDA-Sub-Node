using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Configuration for the Sub-Node loaded from appsettings.json.
/// </summary>
/// <example>
/// appsettings.json:
/// <code>
/// {
///   "SubNode": {
///     "Name": "MyFactorySubNode",
///     "DeviceType": "CustomDevice",
///     "Manufacturer": "Advantech",
///     "Model": "SubNode-SDK",
///     "Version": "1.0.0"
///   }
/// }
/// </code>
/// </example>
public class SubNodeConfiguration
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "SubNode";

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
    public DeviceType DeviceType { get; set; } = DeviceType.CustomDevice;

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
    /// Converts this configuration to a SubNodeInfo instance.
    /// </summary>
    /// <param name="fallbackName">Fallback name if Name is not configured (e.g., assembly name)</param>
    /// <returns>SubNodeInfo populated from this configuration</returns>
    public SubNodeInfo ToSubNodeInfo(string? fallbackName = null)
    {
        return new SubNodeInfo
        {
            Name = !string.IsNullOrWhiteSpace(Name) ? Name : (fallbackName ?? "SubNode"),
            DeviceType = DeviceType,
            Manufacturer = Manufacturer,
            Model = Model,
            Version = Version
        };
    }
}

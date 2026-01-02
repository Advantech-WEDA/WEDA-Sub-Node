using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Device-level configuration loaded from devicecfg.json.
/// Contains SubNode identity and all device configurations.
/// Can be configured programmatically or loaded from configuration files.
/// </summary>
/// <example>
/// Programmatic configuration:
/// <code>
/// var deviceCfg = new DeviceCfg
/// {
///     SubNode = new SubNodeConfig
///     {
///         Name = "MySubNode",
///         SubNodeType = SubNodeType.AdamEthernet,
///         Manufacturer = "Advantech",
///         Model = "WISE-4012",
///         AutoGenEnabled = true
///     },
///     DeviceConfigs = new Dictionary&lt;string, DeviceConfiguration&gt;
///     {
///         ["MyDevice"] = new DeviceConfiguration
///         {
///             Enabled = true,
///             Sensors = new List&lt;Sensor&gt; { ... }
///         }
///     }
/// };
/// </code>
///
/// devicecfg.json:
/// <code>
/// {
///   "SubNode": {
///     "Name": "MySubNode",
///     "SubNodeType": "AdamEthernet",
///     "Manufacturer": "Advantech",
///     "Model": "WISE-4012",
///     "SwVersion": "1.0.0",
///     "AutoGenEnabled": true
///   },
///   "DeviceConfigs": {
///     "MyDevice": {
///       "Enabled": true,
///       "DeviceCommunication": { "Host": "192.168.1.100", "Port": 502 },
///       "Sensors": [ ... ]
///     }
///   }
/// }
/// </code>
/// </example>
public class DeviceCfg
{
    /// <summary>
    /// The configuration section name for the entire device configuration.
    /// </summary>
    public const string SectionName = "DeviceConfig";

    /// <summary>
    /// SubNode identity and metadata configuration.
    /// </summary>
    public SubNodeConfig SubNode { get; set; } = new();

    /// <summary>
    /// Dictionary of device configurations keyed by device name.
    /// Each entry represents a device with its sensors and communication settings.
    /// </summary>
    public Dictionary<string, DeviceConfiguration> DeviceConfigs { get; set; } = new();

    /// <summary>
    /// Gets a device configuration by key.
    /// </summary>
    /// <param name="key">The device configuration key</param>
    /// <returns>The device configuration, or null if not found</returns>
    public DeviceConfiguration? this[string key] =>
        DeviceConfigs.TryGetValue(key, out var config) ? config : null;
}

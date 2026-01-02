namespace Weda.SubNode.Abstractions.DigitalTwin;

/// <summary>
/// DTDL configuration for a device.
/// This section controls how DTDL is generated or loaded for a device.
/// </summary>
/// <example>
/// devicecfg.json:
/// <code>
/// {
///   "DeviceConfigs": {
///     "MyDevice": {
///       "Dtdl": {
///         "AutoGenEnabled": true,
///         "DtdlPath": "path/to/dtdl.json"
///       }
///     }
///   }
/// }
/// </code>
/// </example>
public class DtdlConfig
{
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
    /// Path to DTDL JSON file. Only used when AutoGenEnabled is false.
    /// </summary>
    public string? DtdlPath { get; set; }
}
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace CommandHandlerExample.Commands.SetSensorTags.Models;

/// <summary>
/// Command to attach metadata tags to a named sensor.
/// Maps to payload: data.deviceCmd = "tag.set"
/// </summary>
/// <remarks>
/// The <c>tags</c> parameter is a map (Dictionary&lt;string, string&gt;),
/// which the SDK emits as a DTDL v3 <c>Map</c> schema
/// (<c>mapKey: string</c>, <c>mapValue: string</c>) in the command Interface
/// uploaded to the cloud.
///
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "tag.set",
///   "timeout": 10,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "MyFirstDevice",
///     "sensorName": "temperature_sensor",
///     "tags": {
///       "location": "line-3",
///       "zone": "assembly"
///     }
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("tag.set")]
[Display(Name = "Set Sensor Tags")]
[Description("Attach metadata tags (key/value map) to a named sensor.")]
public class SetSensorTagsCommand : CommandData<SetSensorTagsParameters>
{
}

/// <summary>
/// Parameters for the tag.set command.
/// </summary>
public class SetSensorTagsParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, all registered devices are searched for the sensor.
    /// </summary>
    [JsonPropertyName("deviceName")]
    [Display(Name = "Target Device")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// The sensor name to tag (must match a sensor name in devicecfg.json).
    /// </summary>
    [JsonPropertyName("sensorName")]
    [Display(Name = "Sensor Name")]
    [Required(ErrorMessage = "Sensor name is required")]
    public string SensorName { get; init; } = string.Empty;

    /// <summary>
    /// Tags to apply, as a key/value map. Existing metadata keys are
    /// overwritten; other metadata entries are preserved.
    /// Emitted as a DTDL Map schema (string → string).
    /// </summary>
    [JsonPropertyName("tags")]
    [Display(Name = "Tags")]
    [Required(ErrorMessage = "Tags are required")]
    [MinLength(1, ErrorMessage = "At least one tag must be specified")]
    public Dictionary<string, string> Tags { get; init; } = [];
}

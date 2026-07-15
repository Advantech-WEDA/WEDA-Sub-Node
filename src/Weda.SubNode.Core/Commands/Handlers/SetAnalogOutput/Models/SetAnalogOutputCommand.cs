using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.SetAnalogOutput.Models;

/// <summary>
/// Command to set analog output value on a device.
/// Maps to payload: data.deviceCmd = "ao.set"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "ao.set",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "...",
///     "outputs": [
///       { "name": "ao_0", "value": 1.23 },
///       { "name": "ao_1", "value": 2.45 }
///     ]
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("ao.set")]
[Display(Name = "Set Analog Output")]
public class SetAnalogOutputCommand : CommandData<SetAnalogOutputParameters>
{
}

/// <summary>
/// Parameters for ao.set command.
/// </summary>
public class SetAnalogOutputParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, the command applies to all devices that support analog output.
    /// </summary>
    [JsonPropertyName("deviceName")]
    [Display(Name = "Target Device")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// List of analog outputs to set.
    /// </summary>
    [JsonPropertyName("outputs")]
    [Display(Name = "Outputs to Set")]
    [Required(ErrorMessage = "Outputs are required")]
    [MinLength(1, ErrorMessage = "At least one output must be specified")]
    public AnalogOutputState[] Outputs { get; init; } = [];
}

/// <summary>
/// Represents a single analog output state to set.
/// </summary>
public record AnalogOutputState
{
    /// <summary>
    /// The name of the analog output (e.g., "ao_0", "ao_1").
    /// Must match the sensor name in devicecfg.json.
    /// </summary>
    [JsonPropertyName("name")]
    [Display(Name = "Output Name")]
    [Required(ErrorMessage = "Output name is required")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The desired output value.
    /// Can be integer, float, or other numeric types dependening on the register configuration.
    /// </summary>
    [JsonPropertyName("value")]
    [Display(Name = "Output Value")]
    public object Value { get; init; } = 0;
}

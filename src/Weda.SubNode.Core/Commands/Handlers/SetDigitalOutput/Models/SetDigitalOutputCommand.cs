using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.SetDigitalOutput.Models;

/// <summary>
/// Command to set digital output state on a device.
/// Maps to payload: data.deviceCmd = "do.set"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "do.set",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "...",
///     "outputs": [
///       { "name": "do_0", "state": true },
///       { "name": "do_1", "state": false }
///     ]
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("do.set")]
[Display(Name = "Set Digital Output")]
[Description("Set digital output channels on a target device.")]
public class SetDigitalOutputCommand : CommandData<SetDigitalOutputParameters>
{
}

/// <summary>
/// Parameters for do.set command.
/// </summary>
public class SetDigitalOutputParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, the command applies to all devices that support digital output.
    /// </summary>
    [JsonPropertyName("deviceName")]
    [Display(Name = "Target Device")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// List of digital outputs to set.
    /// </summary>
    [JsonPropertyName("outputs")]
    [Display(Name = "Outputs to Set")]
    [Required(ErrorMessage = "Outputs are required")]
    [MinLength(1, ErrorMessage = "At least one output must be specified")]
    public DigitalOutputState[] Outputs { get; init; } = [];
}

/// <summary>
/// Represents a single digital output state to set.
/// </summary>
public record DigitalOutputState
{
    /// <summary>
    /// The name of the digital output (e.g., "do_0", "do_1").
    /// Must match the sensor name in devicecfg.json.
    /// </summary>
    [JsonPropertyName("name")]
    [Display(Name = "Output Name")]
    [Required(ErrorMessage = "Output name is required")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The desired output state: true = ON/HIGH, false = OFF/LOW.
    /// </summary>
    [JsonPropertyName("state")]
    [Display(Name = "Output State")]
    public bool State { get; init; }
}

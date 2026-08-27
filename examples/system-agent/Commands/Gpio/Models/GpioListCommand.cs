using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace SystemAgentExample.Commands.Gpio.Models;

/// <summary>
/// Command to list all GPIO pins with their direction and current level.
/// Maps to payload: data.deviceCmd = "gpio.list"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "gpio.list",
///   "parameters": { "deviceName": "system-agent" }
/// }
/// </code>
/// Use this to discover the pin names accepted by the built-in
/// do.set / do.get / di.get commands.
/// </remarks>
[DeviceCmd("gpio.list")]
[Display(Name = "List GPIO Pins")]
[Description("List all GPIO pins with direction (input/output) and current level.")]
public class GpioListCommand : CommandData<GpioListParameters>
{
}

/// <summary>
/// Parameters for the gpio.list command.
/// </summary>
public class GpioListParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, all devices exposing GPIO pins are queried.
    /// </summary>
    [JsonPropertyName("deviceName")]
    [Display(Name = "Target Device")]
    public string? DeviceName { get; init; }
}

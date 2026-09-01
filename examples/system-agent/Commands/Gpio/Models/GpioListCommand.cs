using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace SystemAgentExample.Commands.Gpio.Models;

/// <summary>
/// Command to list every GPIO pin of all pin-listable devices, with direction
/// and current level. Maps to payload: data.deviceCmd = "gpio.list"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure (no parameters):
/// <code>
/// {
///   "deviceCmd": "gpio.list",
///   "parameters": {}
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
/// gpio.list takes no parameters: it always reports every IGpioPinListable device.
/// </summary>
public class GpioListParameters
{
}

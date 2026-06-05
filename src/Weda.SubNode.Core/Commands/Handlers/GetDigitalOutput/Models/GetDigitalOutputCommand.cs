using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalOutput.Models;

/// <summary>
/// Command to read digital output states from a device.
/// Maps to payload: data.deviceCmd = "do.get"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "do.get",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "...",
///     "outputs": ["do_0", "do_1"]
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("do.get")]
public class GetDigitalOutputCommand : CommandData<GetDigitalOutputParameters>
{
}

/// <summary>
/// Parameters for do.get command.
/// </summary>
public class GetDigitalOutputParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, the command applies to all devices that support digital output reading.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// List of digital output names to read.
    /// If empty, reads all available digital outputs.
    /// </summary>
    [JsonPropertyName("outputs")]
    public string[] Outputs { get; init; } = [];
}

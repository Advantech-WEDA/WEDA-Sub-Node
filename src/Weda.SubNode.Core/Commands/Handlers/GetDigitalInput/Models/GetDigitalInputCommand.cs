using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalInput.Models;

/// <summary>
/// Command to read digital input states from a device.
/// Maps to payload: data.deviceCmd = "di.get"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "di.get",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "...",
///     "inputs": ["di_0", "di_1"]
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("di.get")]
public class GetDigitalInputCommand : CommandData<GetDigitalInputParameters>
{
}

/// <summary>
/// Parameters for di.get command.
/// </summary>
public class GetDigitalInputParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, the command applies to all devices that support digital input reading.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// List of digital input names to read.
    /// If empty, reads all available digital inputs.
    /// </summary>
    [JsonPropertyName("inputs")]
    public string[] Inputs { get; init; } = [];
}

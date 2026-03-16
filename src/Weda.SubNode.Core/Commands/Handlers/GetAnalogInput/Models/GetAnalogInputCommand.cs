using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogInput.Models;

/// <summary>
/// Command to read analog input values from a device.
/// Maps to payload: data.deviceCmd = "ai.get"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "ai.get",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "...",
///     "inputs": ["ai_0", "temperature"]
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("ai.get")]
public class GetAnalogInputCommand : CommandData<GetAnalogInputParameters>
{
}

/// <summary>
/// Parameters for ai.get command.
/// </summary>
public class GetAnalogInputParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, the command applies to all devices that support analog input reading.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// List of analog input names to read.
    /// If empty, reads all available analog inputs.
    /// </summary>
    [JsonPropertyName("inputs")]
    public string[] Inputs { get; init; } = [];
}

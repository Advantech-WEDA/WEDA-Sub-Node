using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogOutput.Models;

/// <summary>
/// Command to read analog output values from a device.
/// Maps to payload: data.deviceCmd = "ao.get"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "ao.get",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "...",
///     "outputs": ["ao_0", "ao_1"]
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("ao.get")]
public class GetAnalogOutputCommand : CommandData<GetAnalogOutputParameters>
{
}

/// <summary>
/// Parameters for ao.get command.
/// </summary>
public class GetAnalogOutputParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, the command applies to all devices that support analog output reading.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// List of analog output names to read.
    /// If empty, reads all available analog outputs.
    /// </summary>
    [JsonPropertyName("outputs")]
    public string[] Outputs { get; init; } = [];
}

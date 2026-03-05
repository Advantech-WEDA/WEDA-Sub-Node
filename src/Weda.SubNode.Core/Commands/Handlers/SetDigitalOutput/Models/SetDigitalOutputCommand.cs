using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.SetDigitalOutput.Models;

/// <summary>
/// Command to set digital output state on a device.
/// Maps to payload: data.deviceCmd = "cmd.do"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "cmd.do",
///   "deviceName": "...",
///   "outputs": [
///     { "name": "do_0", "state": true },
///     { "name": "do_1", "state": false }
///   ],
///   "respTopic": "...",
///   "timeout": 30
/// }
/// </code>
/// </remarks>
[DeviceCmd("cmd.do")]
public record SetDigitalOutputCommand : ICommand
{
    /// <summary>
    /// The device command identifier.
    /// </summary>
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; init; } = "cmd.do";

    /// <summary>
    /// Sequence ID from the original command envelope.
    /// Set by CommandDispatcher for response correlation.
    /// </summary>
    [JsonIgnore]
    public ulong SeqId { get; set; }

    /// <summary>
    /// Request sequence ID from the original command envelope.
    /// Set by CommandDispatcher for response correlation.
    /// </summary>
    [JsonIgnore]
    public string? ReqSeqId { get; set; }

    /// <summary>
    /// Target device name (optional).
    /// If null or empty, the command applies to all devices that support digital output.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// List of digital outputs to set.
    /// </summary>
    [JsonPropertyName("outputs")]
    [Required(ErrorMessage = "Outputs are required")]
    [MinLength(1, ErrorMessage = "At least one output must be specified")]
    public DigitalOutputState[] Outputs { get; init; } = [];

    /// <summary>
    /// Response topic for command acknowledgment.
    /// If empty, no response will be sent.
    /// </summary>
    [JsonPropertyName("respTopic")]
    public string RespTopic { get; init; } = string.Empty;

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    [JsonPropertyName("timeout")]
    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds")]
    public int Timeout { get; init; } = 30;
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
    [Required(ErrorMessage = "Output name is required")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The desired output state: true = ON/HIGH, false = OFF/LOW.
    /// </summary>
    [JsonPropertyName("state")]
    public bool State { get; init; }
}

using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Commands.Contracts;

/// <summary>
/// Structure of "data" field in CommandMessage.
/// Contains common command metadata and command-specific parameters.
/// </summary>
/// <example>
/// {
///   "deviceCmd": "report.historical"
///   "timeout": 300,
///   "respTopic": "eco1j.weda.74fe488d5d54.subnode.cmd.rsp",
///   "parameters": { ... }
/// }
/// </example>
public class CommandData<TParameter> : ICommand<TParameter>
{
    /// <summary>
    /// Device request command composed of `cmdType` and `subCmd`. e.g., "report.historical", "report.data"
    /// </summary>
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; set; } = string.Empty;

    /// <summary>
    /// Command execution timeout in seconds.
    /// </summary>
    [JsonPropertyName("timeout")]
    public uint Timeout { get; set; } = 30;

    /// <summary>
    /// Topic to response command execution result.
    /// </summary>
    [JsonPropertyName("respTopic")]
    public string? RespTopic { get; set; }

    /// <summary>
    /// Device command parameters.
    /// </summary>
    [JsonPropertyName("parameters")]
    public TParameter Parameters { get; set; } = default!;

    /// <summary>
    /// Message sequence ID from the command envelope.
    /// Populated by CommandDispatcher for response correlation.
    /// </summary>
    [JsonIgnore]
    public ulong SeqId { get; set; }

    /// <summary>
    /// Request sequence ID from the command envelope.
    /// Populated by CommandDispatcher for response correlation.
    /// </summary>
    [JsonIgnore]
    public string? ReqSeqId { get; set; }

    /// <summary>
    /// Time the command was accepted for execution (Unix milliseconds), stamped by the
    /// framework when it sends the ack and written back here. Handlers that emit their own
    /// progress responses read this so every response in the lifecycle carries the same
    /// executedAt; handlers do not compute it themselves.
    /// </summary>
    [JsonIgnore]
    public long ExecutedAt { get; set; }
}
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Commands.Contracts;

/// <summary>
/// Command message envelope received from cloud.
/// Contains metadata for tracking and the command data payload.
/// </summary>
/// <example>
/// Sample payload:
/// {
///   "cmd": "deviceCmd",
///   "seqId": 100,
///   "reqSeqId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
///   "timestamp": 1737004691020,
///   "parentDeviceId": "74fe488d5d54",
///   "data": { ... }
/// }
/// </example>
public class CommandMessage
{
    /// <summary>
    /// Specific message type of payload message. (e.g., "deviceCmd").
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = string.Empty;

    /// <summary>
    /// Message increment sequence id at sender side.
    /// </summary>
    [JsonPropertyName("seqId")]
    public ulong SeqId { get; set; }

    /// <summary>
    /// Command request unique id in UUID format.
    /// </summary>
    [JsonPropertyName("reqSeqId")]
    public string? ReqSeqId { get; set; }

    /// <summary>
    /// Unix timestamp in milliseconds.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Parent WEDA Node device ID.
    /// </summary>
    [JsonPropertyName("parentDeviceId")]
    public string ParentDeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Specific data payload message of each type of request.
    /// Raw JsonElement for deferred deserialization by CommandRegistry.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

/// <summary>
/// Strongly-typed command message with generic data payload.
/// </summary>
public class CommandMessage<T> : CommandMessage
{
    /// <summary>
    /// Strongly-typed data payload.
    /// </summary>
    [JsonIgnore]
    public CommandData<T>? TypedData { get; set; }
}

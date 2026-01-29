using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Command.Contracts;

/// <summary>
/// NATS command message envelope received from cloud.
/// This is the raw structure of command messages on the NATS topic.
/// </summary>
/// <example>
/// Sample payload:
/// {
///   "cmd": "deviceCmd",
///   "seqId": 100,
///   "reqSeqId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
///   "timestamp": 1737004691020,
///   "data": {
///     "deviceCmd": "report",
///     "reportType": "historicalTelemetry",
///     ...
///   }
/// }
/// </example>
public class NatsCommandMessage
{
    /// <summary>
    /// Command type identifier (e.g., "deviceCmd").
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = string.Empty;

    /// <summary>
    /// Sequence ID for message tracking.
    /// </summary>
    [JsonPropertyName("seqId")]
    public ulong SeqId { get; set; }

    /// <summary>
    /// Request sequence ID for correlation (optional).
    /// </summary>
    [JsonPropertyName("reqSeqId")]
    public string? ReqSeqId { get; set; }

    /// <summary>
    /// Message timestamp in Unix milliseconds.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Command data payload as raw JSON element.
    /// This will be deserialized to the specific command type by CommandRegistry.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

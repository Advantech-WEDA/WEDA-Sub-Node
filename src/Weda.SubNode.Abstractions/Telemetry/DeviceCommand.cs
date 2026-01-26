using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Device command received from cloud.
/// Maps to the "data" field of NatsCommandMessage.
/// </summary>
/// <example>
/// Sample payload (data field):
/// {
///   "deviceCmd": "report",
///   "respTopic": "...",
///   "timeout": 300,
///   "reportType": "historicalTelemetry",
///   "timeRange": { "startTime": 123, "endTime": 456 },
///   ...
/// }
/// </example>
public class DeviceCommand
{
    /// <summary>
    /// Device command name (e.g., "report", "setDo", "calibrate").
    /// </summary>
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; set; } = string.Empty;

    /// <summary>
    /// Sequence ID from the original command envelope.
    /// All responses (initial ack, progress, final) should use this same SeqId.
    /// </summary>
    [JsonIgnore]
    public ulong SeqId { get; set; }

    /// <summary>
    /// Request sequence ID from the original command envelope.
    /// Used for response correlation.
    /// </summary>
    [JsonIgnore]
    public string? ReqSeqId { get; set; }

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    [JsonPropertyName("timeout")]
    public uint Timeout { get; set; }

    /// <summary>
    /// Response topic for command result.
    /// </summary>
    [JsonPropertyName("respTopic")]
    public string RespTopic { get; set; } = string.Empty;

    /// <summary>
    /// Additional command-specific parameters (for device protocol parsers).
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>
    /// Raw JSON data from the command envelope.
    /// Used by CommandRegistry to deserialize to specific command types.
    /// Contains the complete "data" field as JsonElement for full-fidelity deserialization.
    /// </summary>
    [JsonIgnore]
    public JsonElement? RawData { get; set; }
}
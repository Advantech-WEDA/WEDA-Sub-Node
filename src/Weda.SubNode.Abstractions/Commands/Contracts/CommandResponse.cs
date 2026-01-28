using System.Text.Json.Serialization;


namespace Weda.SubNode.Abstractions.Commands.Contracts;

/// <summary>
/// Command response message sent back to cloud after command execution.
/// </summary>
/// <remarks>
/// Payload format:
/// <code>
/// {
///   "cmd": "deviceCmd",
///   "seqId": 100,
///   "reqSeqId": "a1b2c3d4-...",
///   "rspSeqId": "b2c3d4e5-...",
///   "timestamp": 1737004691500,
///   "deviceId": "74fe488d5d54",
///   "data": { ... }
/// }
/// </code>
/// </remarks>
public class CommandResponse
{
    /// <summary>
    /// Command type identifier (always "deviceCmd" for device command responses).
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "deviceCmd";

    /// <summary>
    /// Sequence ID from the original request.
    /// All responses (initial ack, progress, final) for the same command should use the same SeqId.
    /// </summary>
    [JsonPropertyName("seqId")]
    public ulong SeqId { get; set; }

    /// <summary>
    /// Request sequence ID for correlation (from original command).
    /// </summary>
    [JsonPropertyName("reqSeqId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReqSeqId { get; set; }

    /// <summary>
    /// Response sequence ID (unique per response).
    /// </summary>
    [JsonPropertyName("rspSeqId")]
    public string RspSeqId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Response timestamp (Unix milliseconds).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Device ID (SubNode ID).
    /// </summary>
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; set; }

    /// <summary>
    /// Response data payload.
    /// Contains command-specific result data.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CommandResponseData? Data { get; set; }

    /// <summary>
    /// Create a "received" response (command accepted, execution starting).
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    /// <param name="message">The response message.</param>
    /// <param name="resultData">Optional response data.</param>
    public static CommandResponse Received(string deviceId, string command, ulong seqId, string? reqSeqId = null, string? message = null, object? resultData = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = new CommandResponseData 
        { 
            DeviceCmd = command,
            MsgType = "ack",
            Status = CommandResponseStatusCode.Success,
            Message = message ?? $"The command {command} for device {deviceId} received",
            ResultData = resultData
        }
    };

    /// <summary>
    /// Create a "success" response (execution completed successfully).
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="status">The status code (0 = Success, 1 = PartialSuccess, etc.).</param>
    /// <param name="message">The response message.</param>
    /// <param name="resultData">Optional response data.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse Success(string deviceId, string command, ulong seqId, int status = CommandResponseStatusCode.Success, string? message = null, object? resultData = null, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = new CommandResponseData
        {
            DeviceCmd = command,
            MsgType = "result",
            Status = status,
            Message = message ?? $"The command {command} for device {deviceId} executed successfully",
            ResultData = resultData
        }
    };

    /// <summary>
    /// Create a "rejected" response (validation failed).
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="status">The status code to include in data.</param>
    /// <param name="message">The response message.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse Rejected(string deviceId, string command, ulong seqId, int status, string? message = null, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = new CommandResponseData
        {
            DeviceCmd = command,
            MsgType = "ack",
            Status = status,
            Message = message ?? $"The command {command} for device {deviceId} was rejected",
        }
    };

    /// <summary>
    /// Create a "failed" response (execution failed).
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="status">The status code to include in data.</param>
    /// <param name="message">The response message.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse Failed(string deviceId, string command, ulong seqId, int status, string? message = null, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = new CommandResponseData
        {
            DeviceCmd = command,
            MsgType = "result",
            Status = status,
            Message = message ?? $"The command {command} for device {deviceId} failed",
        }
    };
}

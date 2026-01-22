using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Command response status codes.
/// </summary>
public static class CommandResponseStatusCode
{
    /// <summary>
    /// Command received and execution started (status = 0)
    /// </summary>
    public const int Received = 0;

    /// <summary>
    /// Command executed successfully (status = 1)
    /// </summary>
    public const int Success = 1;

    /// <summary>
    /// Command rejected due to validation failure (status = -1)
    /// </summary>
    public const int Rejected = -1;

    /// <summary>
    /// Command execution failed (status = -2)
    /// </summary>
    public const int Failed = -2;
}

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
///   "status": 0,
///   "message": "Historical data query started",
///   "data": { ... }
/// }
/// </code>
/// </remarks>
public class CommandResponse
{
    private static int _rspSeqCounter = 0;

    /// <summary>
    /// Command type identifier (always "deviceCmd" for device command responses).
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "deviceCmd";

    /// <summary>
    /// Response sequence ID for tracking.
    /// </summary>
    [JsonPropertyName("seqId")]
    public int SeqId { get; set; }

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
    /// Response status code.
    /// 0 = Received, 1 = Success, -1 = Rejected, -2 = Failed
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Response data payload.
    /// Contains command-specific result data.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    /// <summary>
    /// Create a "received" response (command accepted, execution starting).
    /// </summary>
    public static CommandResponse Received(string deviceId, string command, string? message = null, object? data = null) => new()
    {
        DeviceId = deviceId,
        SeqId = Interlocked.Increment(ref _rspSeqCounter),
        Status = CommandResponseStatusCode.Received,
        Message = message ?? $"Command '{command}' received and execution started",
        Data = data ?? new CommandResponseData { DeviceCmd = command }
    };

    /// <summary>
    /// Create a "success" response (execution completed successfully).
    /// </summary>
    public static CommandResponse Success(string deviceId, string command, object? data = null, string? message = null) => new()
    {
        DeviceId = deviceId,
        SeqId = Interlocked.Increment(ref _rspSeqCounter),
        Status = CommandResponseStatusCode.Success,
        Message = message ?? $"Command '{command}' executed successfully",
        Data = data
    };

    /// <summary>
    /// Create a "rejected" response (validation failed).
    /// </summary>
    public static CommandResponse Rejected(string deviceId, string command, string errorCode, string errorMessage) => new()
    {
        DeviceId = deviceId,
        SeqId = Interlocked.Increment(ref _rspSeqCounter),
        Status = CommandResponseStatusCode.Rejected,
        Message = errorMessage,
        Data = new CommandResponseData
        {
            DeviceCmd = command,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        }
    };

    /// <summary>
    /// Create a "failed" response (execution failed).
    /// </summary>
    public static CommandResponse Failed(string deviceId, string command, string errorCode, string errorMessage) => new()
    {
        DeviceId = deviceId,
        SeqId = Interlocked.Increment(ref _rspSeqCounter),
        Status = CommandResponseStatusCode.Failed,
        Message = errorMessage,
        Data = new CommandResponseData
        {
            DeviceCmd = command,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        }
    };
}

/// <summary>
/// Base data payload for command responses.
/// </summary>
public class CommandResponseData
{
    /// <summary>
    /// The device command name.
    /// </summary>
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; set; } = string.Empty;

    /// <summary>
    /// Error code (only for rejected/failed responses).
    /// </summary>
    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Error message (only for rejected/failed responses).
    /// </summary>
    [JsonPropertyName("errorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }
}

// Keep the old enum for backward compatibility (can be removed later)
[Obsolete("Use CommandResponseStatusCode instead")]
public enum CommandResponseStatus
{
    Received,
    Rejected,
    Success,
    Failed
}

[Obsolete("Use CommandResponseData instead")]
public class CommandError
{
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}

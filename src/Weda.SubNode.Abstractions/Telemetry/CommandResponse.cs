using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Command response status codes.
/// </summary>
/// <remarks>
/// Status codes follow the specification:
/// 0 = SUCCESS: Command executed successfully, all data retrieved
/// 1 = PARTIAL_SUCCESS: Command completed with data gaps or quality issues
/// 2 = INVALID_TIME_RANGE: Time range is invalid or exceeds retention period
/// 3 = NO_DATA_AVAILABLE: No data found for specified time range
/// 4 = STORAGE_ERROR: Local storage unavailable or corrupted
/// 5 = TIMEOUT: Command execution exceeded timeout
/// 6 = PERMISSION_DENIED: Insufficient permissions for data access
/// 7 = RESOURCE_EXHAUSTED: Device resources insufficient for query
/// </remarks>
public static class CommandResponseStatusCode
{
    /// <summary>
    /// Command executed successfully, all data retrieved (status = 0)
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Command completed with data gaps or quality issues (status = 1)
    /// </summary>
    public const int PartialSuccess = 1;

    /// <summary>
    /// Time range is invalid or exceeds retention period (status = 2)
    /// </summary>
    public const int InvalidTimeRange = 2;

    /// <summary>
    /// No data found for specified time range (status = 3)
    /// </summary>
    public const int NoDataAvailable = 3;

    /// <summary>
    /// Local storage unavailable or corrupted (status = 4)
    /// </summary>
    public const int StorageError = 4;

    /// <summary>
    /// Command execution exceeded timeout (status = 5)
    /// </summary>
    public const int Timeout = 5;

    /// <summary>
    /// Insufficient permissions for data access (status = 6)
    /// </summary>
    public const int PermissionDenied = 6;

    /// <summary>
    /// Device resources insufficient for query (status = 7)
    /// </summary>
    public const int ResourceExhausted = 7;

    // Legacy status codes for backward compatibility (initial ack / progress)
    // These are used for the initial "received" acknowledgment before command execution completes

    /// <summary>
    /// Command rejected due to validation failure.
    /// </summary>
    public const int Rejected = -1;

    /// <summary>
    /// Command execution failed.
    /// </summary>
    public const int Failed = -2;

    /// <summary>
    /// Gets a human-readable description for a status code.
    /// </summary>
    public static string GetDescription(int code) => code switch
    {
        Success => "Command executed successfully, all data retrieved",
        PartialSuccess => "Command completed with data gaps or quality issues",
        InvalidTimeRange => "Time range is invalid or exceeds retention period",
        NoDataAvailable => "No data found for specified time range",
        StorageError => "Local storage unavailable or corrupted",
        Timeout => "Command execution exceeded timeout",
        PermissionDenied => "Insufficient permissions for data access",
        ResourceExhausted => "Device resources insufficient for query",
        Rejected => "Command rejected due to validation failure",
        Failed => "Command execution failed",
        _ => "Unknown status"
    };
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
    public object? Data { get; set; }

    /// <summary>
    /// Create a "received" response (command accepted, execution starting).
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    /// <param name="data">Optional response data.</param>
    public static CommandResponse Received(string deviceId, string command, ulong seqId, string? reqSeqId = null, object? data = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = data ?? new CommandResponseData { DeviceCmd = command }
    };

    /// <summary>
    /// Create a "success" response (execution completed successfully).
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="data">Optional response data.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse Success(string deviceId, string command, ulong seqId, object? data = null, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = data
    };

    /// <summary>
    /// Create a "rejected" response (validation failed).
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse Rejected(string deviceId, string command, ulong seqId, string errorCode, string errorMessage, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
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
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse Failed(string deviceId, string command, ulong seqId, string errorCode, string errorMessage, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = new CommandResponseData
        {
            DeviceCmd = command,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        }
    };

    /// <summary>
    /// Create a "failed" response for report.historical command with status/message inside data.
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="status">The status code to include in data.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse FailedWithStatusInData(string deviceId, string command, ulong seqId, int status, string errorCode, string errorMessage, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = new HistoricalReportErrorData
        {
            DeviceCmd = command,
            Status = status,
            Message = errorMessage,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        }
    };

    /// <summary>
    /// Create a "rejected" response for report.historical command with status/message inside data.
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID).</param>
    /// <param name="command">The command name.</param>
    /// <param name="seqId">The sequence ID from the original request.</param>
    /// <param name="status">The status code to include in data.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="reqSeqId">The request sequence ID for correlation.</param>
    public static CommandResponse RejectedWithStatusInData(string deviceId, string command, ulong seqId, int status, string errorCode, string errorMessage, string? reqSeqId = null) => new()
    {
        DeviceId = deviceId,
        SeqId = seqId,
        ReqSeqId = reqSeqId,
        Data = new HistoricalReportErrorData
        {
            DeviceCmd = command,
            Status = status,
            Message = errorMessage,
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

/// <summary>
/// Error data payload for report.historical command responses.
/// Includes status and message inside data per specification.
/// </summary>
public class HistoricalReportErrorData : CommandResponseData
{
    /// <summary>
    /// Status code inside data.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// Human-readable message inside data.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
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

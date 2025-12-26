using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Command response status
/// </summary>
public enum CommandResponseStatus
{
    /// <summary>
    /// Command received and validation passed, execution started
    /// </summary>
    Received,

    /// <summary>
    /// Command rejected due to validation failure
    /// </summary>
    Rejected,

    /// <summary>
    /// Command executed successfully
    /// </summary>
    Success,

    /// <summary>
    /// Command execution failed
    /// </summary>
    Failed
}

/// <summary>
/// Command error details
/// </summary>
public class CommandError
{
    /// <summary>
    /// Error code (e.g., "SetDO.SensorNotFound")
    /// </summary>
    [JsonPropertyName("code")]
    public required string Code { get; set; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; set; }
}

/// <summary>
/// Command response sent back to cloud after command execution
/// </summary>
public class CommandResponse
{
    /// <summary>
    /// Response status
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required CommandResponseStatus Status { get; set; }

    /// <summary>
    /// Original command name
    /// </summary>
    [JsonPropertyName("command")]
    public required string Command { get; set; }

    /// <summary>
    /// Device ID
    /// </summary>
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; set; }

    /// <summary>
    /// Command result (only present when Status = Success)
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// Error details (only present when Status = Rejected or Failed)
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CommandError? Error { get; set; }

    /// <summary>
    /// Response timestamp (Unix milliseconds)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Create a "received" response (validation passed, execution starting)
    /// </summary>
    public static CommandResponse Received(string deviceId, string command) => new()
    {
        Status = CommandResponseStatus.Received,
        DeviceId = deviceId,
        Command = command
    };

    /// <summary>
    /// Create a "rejected" response (validation failed)
    /// </summary>
    public static CommandResponse Rejected(string deviceId, string command, string errorCode, string errorMessage) => new()
    {
        Status = CommandResponseStatus.Rejected,
        DeviceId = deviceId,
        Command = command,
        Error = new CommandError { Code = errorCode, Message = errorMessage }
    };

    /// <summary>
    /// Create a "success" response (execution completed successfully)
    /// </summary>
    public static CommandResponse Success(string deviceId, string command, object? result = null) => new()
    {
        Status = CommandResponseStatus.Success,
        DeviceId = deviceId,
        Command = command,
        Result = result
    };

    /// <summary>
    /// Create a "failed" response (execution failed)
    /// </summary>
    public static CommandResponse Failed(string deviceId, string command, string errorCode, string errorMessage) => new()
    {
        Status = CommandResponseStatus.Failed,
        DeviceId = deviceId,
        Command = command,
        Error = new CommandError { Code = errorCode, Message = errorMessage }
    };
}

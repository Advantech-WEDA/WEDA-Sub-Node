using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.SetDigitalOutput.Models;

/// <summary>
/// Result of the SetDigitalOutput command execution.
/// This object is serialized as the "data" field in the command response.
/// </summary>
public class SetDigitalOutputResult : IResult
{
    /// <summary>
    /// Status code indicating the result of the operation.
    /// </summary>
    /// <seealso cref="SetDigitalOutputStatusCode"/>
    public int Status { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Result data containing operation details.
    /// </summary>
    public SetDigitalOutputResultData? ResultData { get; init; }

    /// <summary>
    /// Error details (only for error cases).
    /// </summary>
    public SetDigitalOutputErrorDetails? ErrorDetails { get; init; }

    /// <summary>
    /// Timestamp when execution started (Unix ms).
    /// </summary>
    public long ExecutedAt { get; init; }

    /// <summary>
    /// Timestamp when execution completed (Unix ms).
    /// </summary>
    public long CompletedAt { get; init; }

    // IResult explicit implementation
    object? IResult.ResultData => ResultData ?? (object?)ErrorDetails;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SetDigitalOutputResult Success(
        SetDigitalOutputResultData resultData,
        long executedAt) => new()
        {
            Status = SetDigitalOutputStatusCode.Success,
            Message = "Digital output set successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    /// <summary>
    /// Creates a partial success result (some outputs failed).
    /// </summary>
    public static SetDigitalOutputResult PartialSuccess(
        SetDigitalOutputResultData resultData,
        long executedAt) => new()
        {
            Status = SetDigitalOutputStatusCode.PartialSuccess,
            Message = "Some digital outputs failed to set",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static SetDigitalOutputResult Error(
        int status,
        string errorCode,
        string errorMessage,
        long executedAt) => new()
        {
            Status = status,
            Message = errorMessage,
            ErrorDetails = new SetDigitalOutputErrorDetails
            {
                ErrorCode = errorCode,
                Recommendation = GetRecommendation(status)
            },
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    private static string? GetRecommendation(int status) => status switch
    {
        SetDigitalOutputStatusCode.DeviceNotFound => "Check device name matches configuration",
        SetDigitalOutputStatusCode.NotSupported => "Ensure device implements IDigitalOutputControllable",
        SetDigitalOutputStatusCode.ExecutionFailed => "Check device connectivity and Modbus configuration",
        SetDigitalOutputStatusCode.Timeout => "Increase timeout or check device responsiveness",
        _ => null
    };
}

/// <summary>
/// Result data for successful SetDigitalOutput operations.
/// </summary>
public class SetDigitalOutputResultData
{
    /// <summary>
    /// Number of outputs successfully set.
    /// </summary>
    [JsonPropertyName("successCount")]
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of outputs that failed to set.
    /// </summary>
    [JsonPropertyName("failureCount")]
    public int FailureCount { get; init; }

    /// <summary>
    /// Details of each output operation.
    /// </summary>
    [JsonPropertyName("outputs")]
    public DigitalOutputOperationResult[]? Outputs { get; init; }
}

/// <summary>
/// Result of a single digital output operation.
/// </summary>
public class DigitalOutputOperationResult
{
    /// <summary>
    /// The output name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The requested state.
    /// </summary>
    [JsonPropertyName("state")]
    public bool State { get; init; }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>
    /// The device name that owns this output.
    /// </summary>
    [JsonPropertyName("deviceName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceName { get; init; }
}

/// <summary>
/// Error details for failed SetDigitalOutput operations.
/// </summary>
public class SetDigitalOutputErrorDetails
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>
    /// Recommendation for resolving the error.
    /// </summary>
    [JsonPropertyName("recommendation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Recommendation { get; init; }
}

using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.SetAnalogOutput.Models;

/// <summary>
/// Result of the SetAnalogOutput command execution.
/// This object is serialized as the "data" field in the command response.
/// </summary>
public class SetAnalogOutputResult : IResult
{
    /// <summary>
    /// Status code indicating the result of the operation.
    /// </summary>
    /// <seealso cref="CommandStatusCode"/>
    public int Status { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Result data containing operation details.
    /// </summary>
    public SetAnalogOutputResultData? ResultData { get; init; }

    /// <summary>
    /// Error details (only for error cases).
    /// </summary>
    public SetAnalogOutputErrorDetails? ErrorDetails { get; init; }

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
    public static SetAnalogOutputResult Success(
        SetAnalogOutputResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.Success,
            Message = "Analog output set successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    /// <summary>
    /// Creates a partial success result (some outputs failed).
    /// </summary>
    public static SetAnalogOutputResult PartialSuccess(
        SetAnalogOutputResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.PartialSuccess,
            Message = "Some analog outputs failed to set",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static SetAnalogOutputResult Error(
        int status,
        string errorCode,
        string errorMessage,
        long executedAt) => new()
        {
            Status = status,
            Message = errorMessage,
            ErrorDetails = new SetAnalogOutputErrorDetails
            {
                ErrorCode = errorCode,
                Recommendation = GetRecommendation(status)
            },
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    private static string? GetRecommendation(int status) => status switch
    {
        CommandStatusCode.NotFound => "Check device name matches configuration",
        CommandStatusCode.UnsupportedCommand => "Ensure device implements IAnalogOutputControllable",
        CommandStatusCode.HardwareError => "Check device connectivity and Modbus configuration",
        CommandStatusCode.Timeout => "Increase timeout or check device responsiveness",
        _ => null
    };
}

/// <summary>
/// Result data for successful SetAnalogOutput operations.
/// </summary>
public class SetAnalogOutputResultData
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
    public AnalogOutputOperationResult[]? Outputs { get; init; }
}

/// <summary>
/// Result of a single analog output operation.
/// </summary>
public class AnalogOutputOperationResult
{
    /// <summary>
    /// The output name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The requested value.
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }

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
/// Error details for failed SetAnalogOutput operations.
/// </summary>
public class SetAnalogOutputErrorDetails
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

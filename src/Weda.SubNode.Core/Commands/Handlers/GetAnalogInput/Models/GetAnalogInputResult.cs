using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogInput.Models;

/// <summary>
/// Result of the ai.get command execution.
/// </summary>
public class GetAnalogInputResult : IResult
{
    public int Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public GetAnalogInputResultData? ResultData { get; init; }
    public long ExecutedAt { get; init; }
    public long CompletedAt { get; init; }

    // IResult explicit implementation
    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    public static GetAnalogInputResult Success(
        GetAnalogInputResultData resultData,
        long executedAt) => new()
        {
            Status = GetAnalogInputStatusCode.Success,
            Message = "Analog inputs read successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetAnalogInputResult PartialSuccess(
        GetAnalogInputResultData resultData,
        long executedAt) => new()
        {
            Status = GetAnalogInputStatusCode.PartialSuccess,
            Message = "Some analog inputs could not be read",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetAnalogInputResult Error(
        int status,
        string message,
        long executedAt) => new()
        {
            Status = status,
            Message = message,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
}

/// <summary>
/// Result data containing the read analog input values.
/// </summary>
public class GetAnalogInputResultData
{
    /// <summary>
    /// Successfully read analog input values.
    /// </summary>
    [JsonPropertyName("values")]
    public AnalogInputValue[] Values { get; init; } = [];

    /// <summary>
    /// Errors for inputs that could not be read.
    /// </summary>
    [JsonPropertyName("errors")]
    public AnalogInputError[]? Errors { get; init; }
}

/// <summary>
/// Represents a single analog input value.
/// </summary>
public record AnalogInputValue
{
    /// <summary>
    /// The name of the analog input.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The current value.
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; init; }

    /// <summary>
    /// The device name that owns this input.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }
}

/// <summary>
/// Represents an error for an analog input that could not be read.
/// </summary>
public record AnalogInputError
{
    /// <summary>
    /// The name of the analog input that failed.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}

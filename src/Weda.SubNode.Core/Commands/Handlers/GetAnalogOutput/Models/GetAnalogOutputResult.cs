using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogOutput.Models;

/// <summary>
/// Result of the ao.get command execution.
/// </summary>
public class GetAnalogOutputResult : IResult
{
    public int Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public GetAnalogOutputResultData? ResultData { get; init; }
    public long ExecutedAt { get; init; }
    public long CompletedAt { get; init; }

    // IResult explicit implementation
    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    public static GetAnalogOutputResult Success(
        GetAnalogOutputResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.Success,
            Message = "Analog outputs read successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetAnalogOutputResult PartialSuccess(
        GetAnalogOutputResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.PartialSuccess,
            Message = "Some analog outputs could not be read",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetAnalogOutputResult Error(
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
/// Result data containing the read analog output values.
/// </summary>
public class GetAnalogOutputResultData
{
    /// <summary>
    /// Successfully read analog output values.
    /// </summary>
    [JsonPropertyName("values")]
    public AnalogOutputValue[] Values { get; init; } = [];

    /// <summary>
    /// Errors for outputs that could not be read.
    /// </summary>
    [JsonPropertyName("errors")]
    public AnalogOutputError[]? Errors { get; init; }
}

/// <summary>
/// Represents a single analog output value.
/// </summary>
public record AnalogOutputValue
{
    /// <summary>
    /// The name of the analog output.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The current value.
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; init; }

    /// <summary>
    /// The device name that owns this output.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }
}

/// <summary>
/// Represents an error for an analog output that could not be read.
/// </summary>
public record AnalogOutputError
{
    /// <summary>
    /// The name of the analog output that failed.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}

using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalOutput.Models;

/// <summary>
/// Result of the do.get command execution.
/// </summary>
public class GetDigitalOutputResult : IResult
{
    public int Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public GetDigitalOutputResultData? ResultData { get; init; }
    public long ExecutedAt { get; init; }
    public long CompletedAt { get; init; }

    // IResult explicit implementation
    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    public static GetDigitalOutputResult Success(
        GetDigitalOutputResultData resultData,
        long executedAt) => new()
        {
            Status = GetDigitalOutputStatusCode.Success,
            Message = "Digital outputs read successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetDigitalOutputResult PartialSuccess(
        GetDigitalOutputResultData resultData,
        long executedAt) => new()
        {
            Status = GetDigitalOutputStatusCode.PartialSuccess,
            Message = "Some digital outputs could not be read",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetDigitalOutputResult Error(
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
/// Result data containing the read digital output values.
/// </summary>
public class GetDigitalOutputResultData
{
    /// <summary>
    /// Successfully read digital output values.
    /// </summary>
    [JsonPropertyName("values")]
    public DigitalOutputValue[] Values { get; init; } = [];

    /// <summary>
    /// Errors for outputs that could not be read.
    /// </summary>
    [JsonPropertyName("errors")]
    public DigitalOutputError[]? Errors { get; init; }
}

/// <summary>
/// Represents a single digital output value.
/// </summary>
public record DigitalOutputValue
{
    /// <summary>
    /// The name of the digital output.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The current state: true = ON/HIGH, false = OFF/LOW.
    /// </summary>
    [JsonPropertyName("state")]
    public bool State { get; init; }

    /// <summary>
    /// The device name that owns this output.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }
}

/// <summary>
/// Represents an error for a digital output that could not be read.
/// </summary>
public record DigitalOutputError
{
    /// <summary>
    /// The name of the digital output that failed.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}

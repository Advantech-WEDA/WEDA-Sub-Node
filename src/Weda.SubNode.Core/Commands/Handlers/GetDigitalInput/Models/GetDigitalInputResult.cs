using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalInput.Models;

/// <summary>
/// Result of the di.get command execution.
/// </summary>
public class GetDigitalInputResult : IResult
{
    public int Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public GetDigitalInputResultData? ResultData { get; init; }
    public long ExecutedAt { get; init; }
    public long CompletedAt { get; init; }

    // IResult explicit implementation
    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    public static GetDigitalInputResult Success(
        GetDigitalInputResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.Success,
            Message = "Digital inputs read successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetDigitalInputResult PartialSuccess(
        GetDigitalInputResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.PartialSuccess,
            Message = "Some digital inputs could not be read",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    public static GetDigitalInputResult Error(
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
/// Result data containing the read digital input values.
/// </summary>
public class GetDigitalInputResultData
{
    /// <summary>
    /// Successfully read digital input values.
    /// </summary>
    [JsonPropertyName("values")]
    public DigitalInputValue[] Values { get; init; } = [];

    /// <summary>
    /// Errors for inputs that could not be read.
    /// </summary>
    [JsonPropertyName("errors")]
    public DigitalInputError[]? Errors { get; init; }
}

/// <summary>
/// Represents a single digital input value.
/// </summary>
public record DigitalInputValue
{
    /// <summary>
    /// The name of the digital input.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The current state: true = ON/HIGH, false = OFF/LOW.
    /// </summary>
    [JsonPropertyName("state")]
    public bool State { get; init; }

    /// <summary>
    /// The device name that owns this input.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string? DeviceName { get; init; }
}

/// <summary>
/// Represents an error for a digital input that could not be read.
/// </summary>
public record DigitalInputError
{
    /// <summary>
    /// The name of the digital input that failed.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}

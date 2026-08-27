using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace SystemAgentExample.Commands.Gpio.Models;

/// <summary>
/// Result of the gpio.list command execution.
/// </summary>
public class GpioListResult : IResult
{
    public int Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public GpioListResultData? ResultData { get; init; }
    public long ExecutedAt { get; init; }
    public long CompletedAt { get; init; }

    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    public static GpioListResult Success(GpioListResultData resultData, long executedAt) => new()
    {
        Status = CommandStatusCode.Success,
        Message = "GPIO pins listed successfully",
        ResultData = resultData,
        ExecutedAt = executedAt,
        CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    public static GpioListResult Error(int status, string message, long executedAt) => new()
    {
        Status = status,
        Message = message,
        ExecutedAt = executedAt,
        CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
}

/// <summary>
/// Result data containing the GPIO pin inventory per device.
/// </summary>
public class GpioListResultData
{
    /// <summary>
    /// GPIO pins keyed by device name.
    /// </summary>
    [JsonPropertyName("devices")]
    public Dictionary<string, GpioPinEntry[]> Devices { get; init; } = [];
}

/// <summary>
/// A single GPIO pin as reported by gpio.list.
/// </summary>
public record GpioPinEntry
{
    /// <summary>
    /// Hardware pin name, as accepted by do.set / do.get / di.get.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// "input", "output", or null when the direction cannot be determined.
    /// </summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; init; }

    /// <summary>
    /// Current level: true = HIGH, false = LOW, null = unavailable.
    /// </summary>
    [JsonPropertyName("state")]
    public bool? State { get; init; }
}

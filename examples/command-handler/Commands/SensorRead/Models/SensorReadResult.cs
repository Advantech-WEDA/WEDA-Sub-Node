using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace CommandHandlerExample.Commands.SensorRead.Models;

/// <summary>
/// Result of the sensor.read command execution.
/// Serialized as the "data" field in the command response.
/// </summary>
public class SensorReadResult : IResult
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
    /// Result data containing the sensor reading.
    /// </summary>
    public SensorReadResultData? ResultData { get; init; }

    /// <summary>
    /// Timestamp when execution started (Unix ms).
    /// </summary>
    public long ExecutedAt { get; init; }

    /// <summary>
    /// Timestamp when execution completed (Unix ms).
    /// </summary>
    public long CompletedAt { get; init; }

    // IResult explicit implementation
    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    /// <summary>
    /// Creates a successful result carrying the sensor reading.
    /// </summary>
    public static SensorReadResult Success(
        SensorReadResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.Success,
            Message = "Sensor value read successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    /// <summary>
    /// Creates an error result with the given status code and message.
    /// </summary>
    public static SensorReadResult Error(
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
/// Sensor reading returned by the sensor.read command.
/// </summary>
public record SensorReadResultData
{
    /// <summary>
    /// Name of the device the sensor belongs to.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Name of the sensor that was read.
    /// </summary>
    [JsonPropertyName("sensorName")]
    public string SensorName { get; init; } = string.Empty;

    /// <summary>
    /// Numeric sensor value, when the reading is convertible to a number.
    /// </summary>
    [JsonPropertyName("value")]
    public double? Value { get; init; }

    /// <summary>
    /// String representation of the raw reading (always populated).
    /// </summary>
    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of the reading (Unix ms).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// Display unit of the sensor, if configured.
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
}

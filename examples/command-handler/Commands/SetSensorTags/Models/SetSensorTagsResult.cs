using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace CommandHandlerExample.Commands.SetSensorTags.Models;

/// <summary>
/// Result of the tag.set command execution.
/// Serialized as the "data" field in the command response.
/// </summary>
public class SetSensorTagsResult : IResult
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
    /// Result data describing the applied tags.
    /// </summary>
    public SetSensorTagsResultData? ResultData { get; init; }

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
    /// Creates a successful result carrying the applied tags.
    /// </summary>
    public static SetSensorTagsResult Success(
        SetSensorTagsResultData resultData,
        long executedAt) => new()
        {
            Status = CommandStatusCode.Success,
            Message = "Sensor tags applied successfully",
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    /// <summary>
    /// Creates an error result with the given status code and message.
    /// </summary>
    public static SetSensorTagsResult Error(
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
/// Applied-tags summary returned by the tag.set command.
/// </summary>
public record SetSensorTagsResultData
{
    /// <summary>
    /// Name of the device the sensor belongs to.
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>
    /// Name of the sensor that was tagged.
    /// </summary>
    [JsonPropertyName("sensorName")]
    public string SensorName { get; init; } = string.Empty;

    /// <summary>
    /// Number of tags applied by this command.
    /// </summary>
    [JsonPropertyName("appliedCount")]
    public int AppliedCount { get; init; }

    /// <summary>
    /// The tags applied by this command (map echo, also a DTDL Map).
    /// </summary>
    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; init; } = [];

    /// <summary>
    /// Total number of metadata entries on the sensor after the merge.
    /// </summary>
    [JsonPropertyName("totalMetadataCount")]
    public int TotalMetadataCount { get; init; }
}

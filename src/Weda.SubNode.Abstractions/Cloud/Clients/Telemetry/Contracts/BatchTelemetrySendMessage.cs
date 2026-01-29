using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;

/// <summary>
/// Batch telemetry send message for historical data upload.
/// Used to report recorded data that was stored locally during offline periods.
/// </summary>
public class BatchTelemetrySendMessage
{
    /// <summary>
    /// Message timestamp (Unix milliseconds)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Command type (always "telemetryBatch")
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "telemetryBatch";

    /// <summary>
    /// Sequence ID (typically uses timestamp for uniqueness)
    /// </summary>
    [JsonPropertyName("seqId")]
    public long SeqId { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Group ID (SubNode ID)
    /// </summary>
    [JsonPropertyName("groupId")]
    public required string GroupId { get; set; }

    /// <summary>
    /// Device ID (SubNode ID)
    /// </summary>
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; set; }

    /// <summary>
    /// Batch telemetry data payload
    /// </summary>
    [JsonPropertyName("data")]
    public required BatchTelemetryDataDto Data { get; set; }

    /// <summary>
    /// Creates a new batch telemetry message.
    /// </summary>
    public static BatchTelemetrySendMessage Create(string deviceId, List<BatchTelemetryMeasureDto> measures)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new BatchTelemetrySendMessage
        {
            Timestamp = now,
            SeqId = now,
            GroupId = deviceId,
            DeviceId = deviceId,
            Data = new BatchTelemetryDataDto { Measures = measures }
        };
    }
}

/// <summary>
/// Batch telemetry data containing multiple sensor measures.
/// </summary>
public class BatchTelemetryDataDto
{
    /// <summary>
    /// List of batch measures for each sensor.
    /// </summary>
    [JsonPropertyName("measures")]
    public required List<BatchTelemetryMeasureDto> Measures { get; set; }
}

/// <summary>
/// Batch telemetry measure for a single sensor.
/// Contains a time-series array of values starting from a given timestamp.
/// </summary>
public class BatchTelemetryMeasureDto
{
    /// <summary>
    /// Sensor short ID (last 5 characters of ResourceId)
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Data interval in seconds.
    /// Each value in the values array is separated by this interval.
    /// </summary>
    [JsonPropertyName("interval")]
    public int Interval { get; set; }

    /// <summary>
    /// Start timestamp in Unix seconds (not milliseconds).
    /// The first value corresponds to this timestamp.
    /// </summary>
    [JsonPropertyName("startTimeStamp")]
    public long StartTimeStamp { get; set; }

    /// <summary>
    /// Array of sensor values.
    /// Values are ordered by time, null indicates missing data at that timestamp.
    /// </summary>
    [JsonPropertyName("values")]
    public required List<double?> Values { get; set; }
}

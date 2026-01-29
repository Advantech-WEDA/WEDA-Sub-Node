using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Command.Contracts;

/// <summary>
/// Parameters for the REPORT command.
/// Cloud sends this command to request historical data from SubNode.
/// </summary>
public class ReportCommandParameters
{
    /// <summary>
    /// Sensor IDs to query. If empty or null, queries all sensors.
    /// Uses the sensor short ID (last 5 characters of ResourceId).
    /// </summary>
    [JsonPropertyName("sensorIds")]
    public List<string>? SensorIds { get; set; }

    /// <summary>
    /// Start timestamp in Unix milliseconds.
    /// </summary>
    [JsonPropertyName("startTimestamp")]
    public long StartTimestamp { get; set; }

    /// <summary>
    /// End timestamp in Unix milliseconds.
    /// </summary>
    [JsonPropertyName("endTimestamp")]
    public long EndTimestamp { get; set; }

    /// <summary>
    /// Maximum number of data points per batch message.
    /// Cloud uses this to control batch size for network/memory constraints.
    /// Default: 1000
    /// </summary>
    [JsonPropertyName("maxBatchSize")]
    public int MaxBatchSize { get; set; } = 1000;
}

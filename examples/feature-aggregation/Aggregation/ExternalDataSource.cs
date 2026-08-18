namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Defines an external data source that the aggregator subscribes to.
/// Uses DeviceName + SensorName to uniquely identify the source sensor.
/// </summary>
public record ExternalDataSource
{
    /// <summary>
    /// The name of the device to subscribe to (from DeviceRegistry).
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// The name of the sensor within the device.
    /// Combined with DeviceName to form the SourceKey: "{DeviceName}/{SensorName}".
    /// </summary>
    public required string SensorName { get; init; }

    /// <summary>
    /// Optional friendly name for this source (used in logging and debugging).
    /// Defaults to "{DeviceName}/{SensorName}" if not specified.
    /// </summary>
    public string? FriendlyName { get; init; }

    /// <summary>
    /// Gets the unique key for this external data source.
    /// Format: "{DeviceName}/{SensorName}"
    /// Note: This is NOT the system-generated ResourceId (UUID format),
    /// but a configuration-based identifier for matching incoming telemetry.
    /// </summary>
    public string SourceKey => $"{DeviceName}/{SensorName}";

    /// <summary>
    /// Gets the display name for this source.
    /// </summary>
    public string GetDisplayName() => FriendlyName ?? SourceKey;
}

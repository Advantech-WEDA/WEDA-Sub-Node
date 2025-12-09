namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Defines an external data source that the aggregator subscribes to.
/// </summary>
public record ExternalDataSource
{
    /// <summary>
    /// The name of the device to subscribe to (from DeviceRegistry).
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// The ResourceIds to extract from this device's telemetry.
    /// If empty, all telemetry from the device will be forwarded.
    /// </summary>
    public IReadOnlyList<string> ResourceIds { get; init; } = [];

    /// <summary>
    /// Optional friendly name for this source (used in logging and debugging).
    /// Defaults to DeviceName if not specified.
    /// </summary>
    public string? FriendlyName { get; init; }

    /// <summary>
    /// Gets the display name for this source.
    /// </summary>
    public string GetDisplayName() => FriendlyName ?? DeviceName;
}
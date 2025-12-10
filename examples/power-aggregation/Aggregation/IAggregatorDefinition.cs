using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Represents a cached data entry with timestamp and consumption tracking.
/// </summary>
public record AggregatorDataEntry
{
    /// <summary>
    /// The source device name that provided this data.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// The ResourceId of the telemetry measure.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// The value of the telemetry measure.
    /// </summary>
    public required double Value { get; init; }

    /// <summary>
    /// The timestamp when this data was received.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Whether this entry has been consumed in an aggregation calculation.
    /// Used to prevent duplicate calculations.
    /// </summary>
    public bool IsConsumed { get; set; }
}

/// <summary>
/// Defines the aggregation logic for combining data from multiple source devices.
/// Implementations specify:
/// - Which devices to subscribe to (ExternalSources)
/// - How to process incoming data (OnDataReceived)
/// - When aggregation is ready (CanAggregate)
/// - How to calculate the aggregated result (Aggregate)
/// </summary>
public interface IAggregatorDefinition
{
    /// <summary>
    /// Configuration for external data sources that this aggregator depends on.
    /// </summary>
    IReadOnlyList<ExternalDataSource> ExternalSources { get; }

    /// <summary>
    /// Maximum allowed time difference between source timestamps for synchronization.
    /// If timestamps differ more than this, aggregation will wait for synchronized data.
    /// </summary>
    TimeSpan SyncTimeWindow { get; }

    /// <summary>
    /// Called when data is received from any of the external sources.
    /// The implementation should cache the data for later aggregation.
    /// </summary>
    /// <param name="sourceName">The name of the device that sent the data</param>
    /// <param name="data">The telemetry data received</param>
    /// <param name="timestamp">The timestamp of the data</param>
    void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp);

    /// <summary>
    /// Checks if aggregation can be performed with the current cached data.
    /// Returns true if all required data is available, synchronized, and unconsumed.
    /// </summary>
    bool CanAggregate();

    /// <summary>
    /// Performs the aggregation calculation and returns the result.
    /// Should mark the consumed data entries to prevent duplicate calculations.
    /// Returns null if aggregation cannot be performed.
    /// </summary>
    IReadOnlyList<TelemetryMeasure>? Aggregate();

    /// <summary>
    /// Resets all cached data entries.
    /// </summary>
    void Reset();
}

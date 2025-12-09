using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Defines how to aggregate data from multiple source devices.
/// Implementations specify which devices to subscribe to and how to process their data.
/// </summary>
public interface IAggregatorDefinition
{
    /// <summary>
    /// Configuration for external data sources that this aggregator depends on.
    /// </summary>
    IReadOnlyList<ExternalDataSource> ExternalSources { get; }

    /// <summary>
    /// Called when data is received from any of the external sources.
    /// </summary>
    /// <param name="sourceName">The name of the device that sent the data</param>
    /// <param name="data">The telemetry data received</param>
    /// <param name="timestamp">The timestamp of the data</param>
    void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp);

    /// <summary>
    /// Gets the latest aggregated telemetry data.
    /// Returns null if aggregation cannot be performed yet (e.g., missing source data).
    /// </summary>
    IReadOnlyList<TelemetryMeasure>? GetAggregatedData();
}

using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Power aggregator definition that calculates Power = Voltage × Current.
/// Subscribes to voltage and current devices and aggregates their readings.
/// </summary>
public class PowerAggregatorDefinition : IAggregatorDefinition
{
    private readonly ILogger<PowerAggregatorDefinition> _logger;
    private readonly object _lock = new();

    private double? _latestVoltage;
    private double? _latestCurrent;
    private DateTimeOffset _voltageTimestamp;
    private DateTimeOffset _currentTimestamp;

    public IReadOnlyList<ExternalDataSource> ExternalSources { get; }

    /// <summary>
    /// Creates a PowerAggregatorDefinition with configured external sources.
    /// </summary>
    /// <param name="externalSources">List of external data sources to subscribe to</param>
    /// <param name="logger">Logger instance</param>
    public PowerAggregatorDefinition(
        IReadOnlyList<ExternalDataSource> externalSources,
        ILogger<PowerAggregatorDefinition>? logger = null)
    {
        ExternalSources = externalSources ?? throw new ArgumentNullException(nameof(externalSources));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            .CreateLogger<PowerAggregatorDefinition>();

        // Validate that we have exactly 2 sources (voltage and current)
        if (ExternalSources.Count != 2)
        {
            throw new ArgumentException(
                $"PowerAggregatorDefinition requires exactly 2 external sources (voltage and current), but got {ExternalSources.Count}",
                nameof(externalSources));
        }

        _logger.LogInformation("PowerAggregatorDefinition initialized with {SourceCount} external sources: {Sources}",
            ExternalSources.Count,
            string.Join(", ", ExternalSources.Select(s => s.GetDisplayName())));
    }

    public void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp)
    {
        // Find which external source this data is from
        var source = ExternalSources.FirstOrDefault(s => s.DeviceName == sourceName);
        if (source == null)
        {
            _logger.LogWarning("Received data from unknown source: {SourceName}", sourceName);
            return;
        }

        // Extract the specific measures we care about
        foreach (var measure in data)
        {
            // Check if this measure's ResourceId is in the source's ResourceIds filter
            if (source.ResourceIds.Count > 0 && !source.ResourceIds.Contains(measure.ResourceId))
            {
                continue; // Skip measures not in the filter
            }

            // Try to identify if this is voltage or current based on ResourceId pattern
            // This is a simple heuristic - you could make this more sophisticated
            var resourceId = measure.ResourceId.ToLowerInvariant();

            if (resourceId.Contains("voltage") && measure.Value is double voltage)
            {
                lock (_lock)
                {
                    _latestVoltage = voltage;
                    _voltageTimestamp = timestamp;
                }
                _logger.LogDebug("Voltage updated: {Voltage:F2} V from {Source} at {Timestamp}",
                    voltage, source.GetDisplayName(), timestamp);
            }
            else if (resourceId.Contains("current") && measure.Value is double current)
            {
                lock (_lock)
                {
                    _latestCurrent = current;
                    _currentTimestamp = timestamp;
                }
                _logger.LogDebug("Current updated: {Current:F2} A from {Source} at {Timestamp}",
                    current, source.GetDisplayName(), timestamp);
            }
        }
    }

    public IReadOnlyList<TelemetryMeasure>? GetAggregatedData()
    {
        lock (_lock)
        {
            if (!_latestVoltage.HasValue || !_latestCurrent.HasValue)
            {
                _logger.LogDebug("Aggregated data not yet available: Voltage={VoltageAvailable}, Current={CurrentAvailable}",
                    _latestVoltage.HasValue, _latestCurrent.HasValue);
                return null;
            }

            var voltage = _latestVoltage.Value;
            var current = _latestCurrent.Value;
            var power = voltage * current;

            // Return aggregated telemetry measures
            return new List<TelemetryMeasure>
            {
                new() { ResourceId = "agg-voltage-001", Value = voltage, Timestamp = _voltageTimestamp.ToUnixTimeMilliseconds() },
                new() { ResourceId = "agg-current-001", Value = current, Timestamp = _currentTimestamp.ToUnixTimeMilliseconds() },
                new() { ResourceId = "agg-power-001", Value = power, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
            };
        }
    }
}
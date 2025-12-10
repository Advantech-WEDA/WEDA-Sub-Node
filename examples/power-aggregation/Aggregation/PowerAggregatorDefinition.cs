using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Power aggregator definition that calculates Power = Voltage × Current.
/// Subscribes to voltage and current source devices and aggregates their readings.
/// </summary>
public class PowerAggregatorDefinition : IAggregatorDefinition
{
    private readonly ILogger<PowerAggregatorDefinition> _logger;
    private readonly object _lock = new();

    // Source keys for voltage and current
    private const string VoltageKey = "voltage";
    private const string CurrentKey = "current";

    // Cached data entries
    private AggregatorDataEntry? _voltageEntry;
    private AggregatorDataEntry? _currentEntry;

    public IReadOnlyList<ExternalDataSource> ExternalSources { get; }
    public TimeSpan SyncTimeWindow { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates a PowerAggregatorDefinition with configured external sources.
    /// </summary>
    /// <param name="externalSources">
    /// List of external data sources. Must contain exactly 2 sources with SourceKeys "voltage" and "current".
    /// </param>
    /// <param name="logger">Logger instance</param>
    public PowerAggregatorDefinition(
        IReadOnlyList<ExternalDataSource> externalSources,
        ILogger<PowerAggregatorDefinition>? logger = null)
    {
        ExternalSources = externalSources ?? throw new ArgumentNullException(nameof(externalSources));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            .CreateLogger<PowerAggregatorDefinition>();

        // Validate that we have voltage and current sources
        var voltageSource = ExternalSources.FirstOrDefault(s =>
            s.SourceKey.Equals(VoltageKey, StringComparison.OrdinalIgnoreCase));
        var currentSource = ExternalSources.FirstOrDefault(s =>
            s.SourceKey.Equals(CurrentKey, StringComparison.OrdinalIgnoreCase));

        if (voltageSource == null)
        {
            throw new ArgumentException(
                $"PowerAggregatorDefinition requires an external source with SourceKey='{VoltageKey}'",
                nameof(externalSources));
        }

        if (currentSource == null)
        {
            throw new ArgumentException(
                $"PowerAggregatorDefinition requires an external source with SourceKey='{CurrentKey}'",
                nameof(externalSources));
        }

        _logger.LogInformation(
            "PowerAggregatorDefinition initialized with voltage source '{VoltageDevice}' and current source '{CurrentDevice}'",
            voltageSource.DeviceName, currentSource.DeviceName);
    }

    public void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp)
    {
        // Find which source this data is from
        var source = ExternalSources.FirstOrDefault(s => s.DeviceName == sourceName);
        if (source == null)
        {
            _logger.LogWarning("Received data from unknown source: {SourceName}", sourceName);
            return;
        }

        // Process each measure
        foreach (var measure in data)
        {
            // Apply ResourceIds filter if specified
            if (source.ResourceIds.Count > 0 && !source.ResourceIds.Contains(measure.ResourceId))
            {
                continue;
            }

            // Convert to double
            double value;
            try
            {
                value = Convert.ToDouble(measure.Value);
            }
            catch
            {
                _logger.LogWarning(
                    "Cannot convert measure value to double: ResourceId={ResourceId}, Value={Value}",
                    measure.ResourceId, measure.Value);
                continue;
            }

            var entry = new AggregatorDataEntry
            {
                SourceName = sourceName,
                ResourceId = measure.ResourceId,
                Value = value,
                Timestamp = timestamp,
                IsConsumed = false
            };

            lock (_lock)
            {
                if (source.SourceKey.Equals(VoltageKey, StringComparison.OrdinalIgnoreCase))
                {
                    _voltageEntry = entry;
                    _logger.LogDebug("[PowerAggregator] Cached Voltage: {Value:F2} V from {Source}",
                        value, source.GetDisplayName());
                }
                else if (source.SourceKey.Equals(CurrentKey, StringComparison.OrdinalIgnoreCase))
                {
                    _currentEntry = entry;
                    _logger.LogDebug("[PowerAggregator] Cached Current: {Value:F2} A from {Source}",
                        value, source.GetDisplayName());
                }
            }
        }
    }

    public bool CanAggregate()
    {
        lock (_lock)
        {
            // Both must exist and be unconsumed
            if (_voltageEntry == null || _currentEntry == null)
            {
                return false;
            }

            if (_voltageEntry.IsConsumed || _currentEntry.IsConsumed)
            {
                return false;
            }

            // Check if timestamps are within the sync window
            var timeDiff = (_voltageEntry.Timestamp - _currentEntry.Timestamp).Duration();
            if (timeDiff > SyncTimeWindow)
            {
                _logger.LogDebug(
                    "[PowerAggregator] Timestamps not synchronized: VoltageTime={VoltageTime}, CurrentTime={CurrentTime}, Diff={Diff}ms",
                    _voltageEntry.Timestamp, _currentEntry.Timestamp, timeDiff.TotalMilliseconds);
                return false;
            }

            return true;
        }
    }

    public IReadOnlyList<TelemetryMeasure>? Aggregate()
    {
        lock (_lock)
        {
            if (!CanAggregate())
            {
                return null;
            }

            // Mark both entries as consumed
            _voltageEntry!.IsConsumed = true;
            _currentEntry!.IsConsumed = true;

            var voltage = _voltageEntry.Value;
            var current = _currentEntry.Value;
            var power = voltage * current;

            // Use the later timestamp
            var aggregationTimestamp = _voltageEntry.Timestamp > _currentEntry.Timestamp
                ? _voltageEntry.Timestamp
                : _currentEntry.Timestamp;

            _logger.LogInformation(
                "[PowerAggregator] ⚡ POWER CALCULATED: {Voltage:F2} V × {Current:F2} A = {Power:F2} W",
                voltage, current, power);

            return new List<TelemetryMeasure>
            {
                new() { ResourceId = "voltage", Value = voltage, Timestamp = aggregationTimestamp.ToUnixTimeMilliseconds() },
                new() { ResourceId = "current", Value = current, Timestamp = aggregationTimestamp.ToUnixTimeMilliseconds() },
                new() { ResourceId = "power", Value = power, Timestamp = aggregationTimestamp.ToUnixTimeMilliseconds() }
            };
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _voltageEntry = null;
            _currentEntry = null;
        }
        _logger.LogDebug("[PowerAggregator] Cache reset");
    }
}

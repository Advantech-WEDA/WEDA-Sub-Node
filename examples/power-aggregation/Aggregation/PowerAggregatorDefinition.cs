using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Power aggregator definition that calculates Power = Voltage × Current.
/// Reads voltage and current source mappings from Sensor.Parameters:
/// - VoltageSource: "DeviceName/SensorName" format (e.g., "VoltageSensor/voltage001")
/// - CurrentSource: "DeviceName/SensorName" format (e.g., "CurrentSensor/current001")
/// </summary>
public class PowerAggregatorDefinition : IAggregatorDefinition
{
    private readonly ILogger<PowerAggregatorDefinition> _logger;
    private readonly object _lock = new();

    // Parameter keys for source configuration
    private const string VoltageSourceKey = "VoltageSource";
    private const string CurrentSourceKey = "CurrentSource";

    // Hold reference to sensor for lazy ResourceId access
    // (ResourceId is populated after device registration with cloud service)
    private readonly Sensor _sensor;

    // External source keys (parsed from Sensor.Parameters)
    // Format: "DeviceName/SensorName" - NOT system-generated ResourceId
    private readonly string _voltageSourceKey;
    private readonly string _currentSourceKey;

    // Cached data entries
    private AggregatorDataEntry? _voltageEntry;
    private AggregatorDataEntry? _currentEntry;

    /// <summary>
    /// The sensor name (available at construction time).
    /// </summary>
    public string SensorName { get; }

    /// <summary>
    /// The ResourceId for the output power sensor.
    /// Note: This is populated after device registration with cloud service,
    /// so it may be empty during construction but will be available at aggregation time.
    /// </summary>
    public string OutputResourceId => _sensor.ResourceId;

    /// <summary>
    /// The external sources required by this aggregator (derived from Sensor.Parameters).
    /// </summary>
    public IReadOnlyList<ExternalDataSource> ExternalSources { get; }

    public TimeSpan SyncTimeWindow { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates a PowerAggregatorDefinition from a Sensor configuration.
    /// Reads VoltageSource and CurrentSource from Sensor.Parameters.
    /// </summary>
    /// <param name="sensor">
    /// The sensor configuration containing:
    /// - Name: Sensor name (available immediately)
    /// - ResourceId: Output power sensor ResourceId (populated after cloud registration)
    /// - Parameters["VoltageSource"]: "DeviceName/SensorName" for voltage
    /// - Parameters["CurrentSource"]: "DeviceName/SensorName" for current
    /// </param>
    /// <param name="logger">Logger instance</param>
    public PowerAggregatorDefinition(
        Sensor sensor,
        ILogger<PowerAggregatorDefinition>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        _sensor = sensor;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            .CreateLogger<PowerAggregatorDefinition>();

        SensorName = sensor.Name;

        // Parse VoltageSource and CurrentSource from Parameters
        _voltageSourceKey = GetRequiredParameter(sensor, VoltageSourceKey);
        _currentSourceKey = GetRequiredParameter(sensor, CurrentSourceKey);

        // Build ExternalSources list from parsed source keys
        ExternalSources = BuildExternalSources(_voltageSourceKey, _currentSourceKey);

        _logger.LogInformation(
            "PowerAggregatorDefinition initialized: Sensor='{SensorName}', VoltageSource='{VoltageSource}', CurrentSource='{CurrentSource}'",
            SensorName, _voltageSourceKey, _currentSourceKey);
    }

    /// <summary>
    /// Gets a required parameter from Sensor.Parameters.
    /// </summary>
    private static string GetRequiredParameter(Sensor sensor, string key)
    {
        if (sensor.Parameters == null || !sensor.Parameters.TryGetValue(key, out var value))
        {
            var availableKeys = sensor.Parameters?.Keys != null
                ? string.Join(", ", sensor.Parameters.Keys)
                : "(none)";
            throw new InvalidOperationException(
                $"PowerAggregatorDefinition requires '{key}' in Sensor.Parameters. " +
                $"Sensor: {sensor.Name}, Available parameters: [{availableKeys}]");
        }

        var resourceId = value?.ToString();
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new InvalidOperationException(
                $"'{key}' parameter cannot be empty in Sensor.Parameters. Sensor: {sensor.Name}");
        }

        return resourceId;
    }

    /// <summary>
    /// Builds ExternalDataSource list from SourceKey strings.
    /// SourceKey format: "DeviceName/SensorName"
    /// </summary>
    private static List<ExternalDataSource> BuildExternalSources(params string[] sourceKeys)
    {
        var sources = new List<ExternalDataSource>();
        foreach (var sourceKey in sourceKeys)
        {
            var parts = sourceKey.Split('/');
            if (parts.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Invalid source key format: '{sourceKey}'. Expected 'DeviceName/SensorName' format.");
            }

            sources.Add(new ExternalDataSource
            {
                DeviceName = parts[0],
                SensorName = parts[1]
            });
        }
        return sources;
    }

    public void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp)
    {
        // sourceName is the DeviceName from external source
        // We need to check if this device is one of our sources and match by ResourceId
        foreach (var measure in data)
        {
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
                // Match by SourceKey (DeviceName/SensorName format)
                if (measure.ResourceId.Equals(_voltageSourceKey, StringComparison.OrdinalIgnoreCase))
                {
                    _voltageEntry = entry;
                    _logger.LogDebug("[PowerAggregator] Cached Voltage: {Value:F2} V from {Source}",
                        value, _voltageSourceKey);
                }
                else if (measure.ResourceId.Equals(_currentSourceKey, StringComparison.OrdinalIgnoreCase))
                {
                    _currentEntry = entry;
                    _logger.LogDebug("[PowerAggregator] Cached Current: {Value:F2} A from {Source}",
                        value, _currentSourceKey);
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
                "[PowerAggregator] POWER CALCULATED: {Voltage:F2} V x {Current:F2} A = {Power:F2} W",
                voltage, current, power);

            return
            [
                new() { ResourceId = OutputResourceId, Value = power, Timestamp = aggregationTimestamp.ToUnixTimeMilliseconds() }
            ];
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

using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Base class for aggregator definitions that simplifies creating custom aggregators.
/// Handles common functionality:
/// - Parsing SourceKey from Sensor.Parameters
/// - Building ExternalSources list
/// - Caching incoming data with thread-safety
/// - Time synchronization check
/// - Reset logic
///
/// Subclasses only need to:
/// 1. Define required parameter keys in constructor
/// 2. Override Calculate() to implement the aggregation formula
///
/// Example subclass:
/// <code>
/// public class PowerAggregatorDefinition : AggregatorDefinitionBase
/// {
///     public PowerAggregatorDefinition(Sensor sensor, ILogger logger)
///         : base(sensor, logger, "VoltageSource", "CurrentSource")
///     {
///     }
///
///     protected override double Calculate(IReadOnlyDictionary&lt;string, double&gt; values)
///     {
///         var voltage = values["VoltageSource"];
///         var current = values["CurrentSource"];
///         return voltage * current;  // P = V × I
///     }
/// }
/// </code>
/// </summary>
public abstract class AggregatorDefinitionBase : IAggregatorDefinition
{
    /// <summary>
    /// Logger for subclasses to use.
    /// </summary>
    protected readonly ILogger Logger;

    private readonly object _lock = new();

    /// <summary>
    /// The sensor configuration reference.
    /// </summary>
    protected readonly Sensor Sensor;

    /// <summary>
    /// Maps parameter key to SourceKey (e.g., "VoltageSource" → "VoltageSensor/voltage001").
    /// </summary>
    private readonly Dictionary<string, string> _parameterToSourceKey = new();

    /// <summary>
    /// Maps SourceKey to cached data entry.
    /// </summary>
    private readonly Dictionary<string, AggregatorDataEntry> _cachedEntries = new();

    /// <summary>
    /// The parameter keys required by this definition.
    /// </summary>
    protected IReadOnlyList<string> ParameterKeys { get; }

    /// <summary>
    /// The sensor name (available at construction time).
    /// </summary>
    public string SensorName { get; }

    /// <summary>
    /// The ResourceId for the output sensor.
    /// Note: Populated after device registration with cloud service.
    /// </summary>
    public string OutputResourceId => Sensor.ResourceId;

    /// <summary>
    /// The external sources required by this aggregator.
    /// </summary>
    public IReadOnlyList<ExternalDataSource> ExternalSources { get; }

    /// <summary>
    /// Time window for synchronizing data from different sources.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan SyncTimeWindow { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Creates an AggregatorDefinitionBase.
    /// </summary>
    /// <param name="sensor">The sensor configuration with Parameters</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="requiredParameterKeys">
    /// The parameter keys to read from Sensor.Parameters.
    /// Each key's value should be in "DeviceName/SensorName" format.
    /// </param>
    protected AggregatorDefinitionBase(
        Sensor sensor,
        ILogger logger,
        params string[] requiredParameterKeys)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(requiredParameterKeys);

        if (requiredParameterKeys.Length == 0)
        {
            throw new ArgumentException(
                "At least one parameter key is required", nameof(requiredParameterKeys));
        }

        Sensor = sensor;
        Logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        SensorName = sensor.Name;
        ParameterKeys = requiredParameterKeys.ToList();

        // Parse each parameter and build mappings
        var externalSources = new List<ExternalDataSource>();
        foreach (var key in requiredParameterKeys)
        {
            var sourceKey = GetRequiredParameter(sensor, key);
            _parameterToSourceKey[key] = sourceKey;

            var source = ParseSourceKey(sourceKey);
            externalSources.Add(source);

            Logger.LogDebug(
                "[{DefinitionType}] Parameter '{Key}' = '{SourceKey}'",
                GetType().Name, key, sourceKey);
        }

        ExternalSources = externalSources;

        Logger.LogInformation(
            "{DefinitionType} initialized: Sensor='{SensorName}', Sources=[{Sources}]",
            GetType().Name, SensorName,
            string.Join(", ", _parameterToSourceKey.Select(kv => $"{kv.Key}={kv.Value}")));
    }

    /// <summary>
    /// Gets a required parameter from Sensor.Parameters.
    /// </summary>
    protected static string GetRequiredParameter(Sensor sensor, string key)
    {
        if (sensor.Parameters == null || !sensor.Parameters.TryGetValue(key, out var value))
        {
            var availableKeys = sensor.Parameters?.Keys != null
                ? string.Join(", ", sensor.Parameters.Keys)
                : "(none)";
            throw new InvalidOperationException(
                $"AggregatorDefinition requires '{key}' in Sensor.Parameters. " +
                $"Sensor: {sensor.Name}, Available parameters: [{availableKeys}]");
        }

        var sourceKey = value?.ToString();
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            throw new InvalidOperationException(
                $"'{key}' parameter cannot be empty in Sensor.Parameters. Sensor: {sensor.Name}");
        }

        return sourceKey;
    }

    /// <summary>
    /// Parses a SourceKey string into an ExternalDataSource.
    /// SourceKey format: "DeviceName/SensorName"
    /// </summary>
    protected static ExternalDataSource ParseSourceKey(string sourceKey)
    {
        var parts = sourceKey.Split('/');
        if (parts.Length != 2)
        {
            throw new InvalidOperationException(
                $"Invalid source key format: '{sourceKey}'. Expected 'DeviceName/SensorName' format.");
        }

        return new ExternalDataSource
        {
            DeviceName = parts[0],
            SensorName = parts[1]
        };
    }

    /// <summary>
    /// Gets the SourceKey for a parameter key.
    /// </summary>
    protected string GetSourceKey(string parameterKey)
    {
        if (!_parameterToSourceKey.TryGetValue(parameterKey, out var sourceKey))
        {
            throw new InvalidOperationException(
                $"Parameter key '{parameterKey}' not found. Available keys: [{string.Join(", ", _parameterToSourceKey.Keys)}]");
        }
        return sourceKey;
    }

    /// <inheritdoc />
    public virtual void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp)
    {
        foreach (var measure in data)
        {
            // Try to match against any of our source keys
            var matchedKey = _parameterToSourceKey
                .FirstOrDefault(kv => kv.Value.Equals(measure.ResourceId, StringComparison.OrdinalIgnoreCase))
                .Key;

            if (matchedKey == null)
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
                Logger.LogWarning(
                    "[{DefinitionType}] Cannot convert value to double: SourceKey={SourceKey}, Value={Value}",
                    GetType().Name, measure.ResourceId, measure.Value);
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
                _cachedEntries[matchedKey] = entry;
                Logger.LogDebug(
                    "[{DefinitionType}] Cached {ParameterKey}: {Value:F2} from {SourceKey}",
                    GetType().Name, matchedKey, value, measure.ResourceId);
            }
        }
    }

    /// <inheritdoc />
    public virtual bool CanAggregate()
    {
        lock (_lock)
        {
            // All sources must have data
            if (_cachedEntries.Count < ParameterKeys.Count)
            {
                return false;
            }

            // All entries must be unconsumed
            if (_cachedEntries.Values.Any(e => e.IsConsumed))
            {
                return false;
            }

            // Check time synchronization
            var timestamps = _cachedEntries.Values.Select(e => e.Timestamp).ToList();
            var minTime = timestamps.Min();
            var maxTime = timestamps.Max();
            var timeDiff = maxTime - minTime;

            if (timeDiff > SyncTimeWindow)
            {
                Logger.LogDebug(
                    "[{DefinitionType}] Timestamps not synchronized: MinTime={MinTime}, MaxTime={MaxTime}, Diff={Diff}ms",
                    GetType().Name, minTime, maxTime, timeDiff.TotalMilliseconds);
                return false;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public virtual IReadOnlyList<TelemetryMeasure>? Aggregate()
    {
        lock (_lock)
        {
            if (!CanAggregate())
            {
                return null;
            }

            // Mark all entries as consumed
            foreach (var entry in _cachedEntries.Values)
            {
                entry.IsConsumed = true;
            }

            // Build values dictionary for Calculate()
            var values = _cachedEntries.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Value);

            // Call subclass calculation
            var result = Calculate(values);

            // Use the latest timestamp
            var aggregationTimestamp = _cachedEntries.Values
                .Max(e => e.Timestamp);

            LogCalculation(values, result);

            return
            [
                new TelemetryMeasure
                {
                    ResourceId = OutputResourceId,
                    Value = result,
                    Timestamp = aggregationTimestamp.ToUnixTimeMilliseconds()
                }
            ];
        }
    }

    /// <summary>
    /// Performs the aggregation calculation.
    /// Override this method to implement your custom formula.
    /// </summary>
    /// <param name="values">Dictionary of parameter key to value (e.g., "VoltageSource" → 230.0)</param>
    /// <returns>The calculated result</returns>
    protected abstract double Calculate(IReadOnlyDictionary<string, double> values);

    /// <summary>
    /// Logs the calculation result. Override to customize logging.
    /// </summary>
    protected virtual void LogCalculation(IReadOnlyDictionary<string, double> values, double result)
    {
        var inputStr = string.Join(", ", values.Select(kv => $"{kv.Key}={kv.Value:F2}"));
        Logger.LogInformation(
            "[{DefinitionType}] Calculated: [{Inputs}] => {Result:F2}",
            GetType().Name, inputStr, result);
    }

    /// <inheritdoc />
    public virtual void Reset()
    {
        lock (_lock)
        {
            _cachedEntries.Clear();
        }
        Logger.LogDebug("[{DefinitionType}] Cache reset", GetType().Name);
    }
}

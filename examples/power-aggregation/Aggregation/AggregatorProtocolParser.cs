using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Aggregation;

/// <summary>
/// Protocol parser for aggregator devices.
/// Manages IAggregatorDefinition instances per Sensor and routes data to appropriate definitions.
///
/// Architecture:
/// - Communication layer (AggregatorCommunication) subscribes to external devices
/// - Parser layer (AggregatorProtocolParser) reads Sensor.Parameters and manages Definitions
/// - Definition layer (IAggregatorDefinition) performs pure calculation logic
///
/// This class:
/// 1. Creates IAggregatorDefinition for each sensor based on Sensor.Parameters
/// 2. Aggregates all ExternalSources from all Definitions for Communication layer
/// 3. Routes incoming data to appropriate Definitions
/// 4. Collects aggregated results from all Definitions
/// </summary>
public class AggregatorProtocolParser
{
    private readonly ILogger<AggregatorProtocolParser> _logger;
    private readonly Dictionary<string, IAggregatorDefinition> _definitions = new();
    private readonly List<ExternalDataSource> _allExternalSources = new();

    /// <summary>
    /// Factory delegate for creating IAggregatorDefinition from a Sensor.
    /// </summary>
    public delegate IAggregatorDefinition DefinitionFactory(Sensor sensor);

    /// <summary>
    /// The factory used to create IAggregatorDefinition instances.
    /// </summary>
    private readonly DefinitionFactory _definitionFactory;

    /// <summary>
    /// All external sources aggregated from all definitions.
    /// Used by AggregatorCommunication to know which devices to subscribe to.
    /// </summary>
    public IReadOnlyList<ExternalDataSource> AllExternalSources => _allExternalSources;

    /// <summary>
    /// Event raised when aggregated telemetry data is ready from any definition.
    /// </summary>
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Creates an AggregatorProtocolParser with a definition factory.
    /// </summary>
    /// <param name="definitionFactory">Factory to create IAggregatorDefinition from Sensor</param>
    /// <param name="logger">Logger instance</param>
    public AggregatorProtocolParser(
        DefinitionFactory definitionFactory,
        ILogger<AggregatorProtocolParser>? logger = null)
    {
        _definitionFactory = definitionFactory ?? throw new ArgumentNullException(nameof(definitionFactory));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            .CreateLogger<AggregatorProtocolParser>();
    }

    /// <summary>
    /// Registers a sensor and creates its corresponding IAggregatorDefinition.
    /// Uses Sensor.Name as the key since ResourceId is not available until after cloud registration.
    /// </summary>
    /// <param name="sensor">The sensor configuration with Parameters defining source mappings</param>
    public void RegisterSensor(Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        // Use Sensor.Name as key since ResourceId is not available at construction time
        // (ResourceId is populated after device registration with cloud service)
        if (_definitions.ContainsKey(sensor.Name))
        {
            _logger.LogWarning(
                "Sensor '{SensorName}' already registered, skipping",
                sensor.Name);
            return;
        }

        try
        {
            var definition = _definitionFactory(sensor);
            _definitions[sensor.Name] = definition;

            // Aggregate external sources (avoid duplicates)
            foreach (var source in definition.ExternalSources)
            {
                if (!_allExternalSources.Any(s => s.SourceKey.Equals(source.SourceKey, StringComparison.OrdinalIgnoreCase)))
                {
                    _allExternalSources.Add(source);
                }
            }

            _logger.LogInformation(
                "Registered sensor '{SensorName}' with {SourceCount} external sources",
                sensor.Name, definition.ExternalSources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create IAggregatorDefinition for sensor '{SensorName}'",
                sensor.Name);
            throw;
        }
    }

    /// <summary>
    /// Called when data is received from an external source device.
    /// Routes the data to all registered definitions.
    /// </summary>
    /// <param name="sourceName">The device name that sent the data</param>
    /// <param name="data">The telemetry data received</param>
    /// <param name="timestamp">The timestamp of the data</param>
    public void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp)
    {
        _logger.LogDebug(
            "[AggregatorParser] Received data from '{SourceName}': {Count} measures",
            sourceName, data.Count);

        // Forward to all definitions - each will filter by its own sources
        foreach (var (_, definition) in _definitions)
        {
            definition.OnDataReceived(sourceName, data, timestamp);
        }

        // Try to aggregate from all definitions
        TryRaiseAggregatedTelemetry();
    }

    /// <summary>
    /// Tries to aggregate from all definitions and raises OnTelemetryReceived if any are ready.
    /// </summary>
    private void TryRaiseAggregatedTelemetry()
    {
        var allResults = new List<TelemetryMeasure>();

        foreach (var (_, definition) in _definitions)
        {
            if (definition.CanAggregate())
            {
                var result = definition.Aggregate();
                if (result != null)
                {
                    allResults.AddRange(result);
                }
            }
        }

        if (allResults.Count > 0)
        {
            OnTelemetryReceived?.Invoke(allResults);
        }
    }

    /// <summary>
    /// Resets all registered definitions.
    /// </summary>
    public void Reset()
    {
        foreach (var (_, definition) in _definitions)
        {
            definition.Reset();
        }
        _logger.LogDebug("[AggregatorParser] All definitions reset");
    }

    /// <summary>
    /// Gets the number of registered definitions.
    /// </summary>
    public int DefinitionCount => _definitions.Count;
}

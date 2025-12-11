using ErrorOr;
using Microsoft.Extensions.Logging;
using PowerAggregationExample.Aggregation;
using PowerAggregationExample.Communication;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Protocols;

/// <summary>
/// PubSub protocol parser for aggregator devices.
/// Implements IPubSubProtocolParser to work with PubSubDeviceBase.
///
/// This class combines:
/// - IPubSubProtocolParser implementation for PubSubDeviceBase
/// - Definition management (creating IAggregatorDefinition for each sensor)
/// - Data routing to definitions
/// - Communication management (AggregatorCommunication)
///
/// Architecture: AggregatorDevice -> AggregatorProtocolParser -> AggregatorCommunication -> IAggregatorDefinition
/// </summary>
public class AggregatorProtocolParser : IPubSubProtocolParser
{
    private readonly ILogger<AggregatorProtocolParser> _logger;
    private readonly AggregatorCommunication _communication;

    // Definition management
    private readonly Dictionary<string, IAggregatorDefinition> _definitions = new();
    private readonly List<ExternalDataSource> _allExternalSources = new();

    /// <summary>
    /// Factory delegate for creating IAggregatorDefinition from a Sensor.
    /// </summary>
    public delegate IAggregatorDefinition DefinitionFactory(Sensor sensor);

    public ICommunication Communication => _communication;
    public string ProtocolName => "Aggregator";
    public IReadOnlyList<string> SupportedDataTypes => ["double", "int", "float"];
    public bool SupportsBidirectional => false;

    /// <summary>
    /// All external sources aggregated from all definitions.
    /// Used by AggregatorCommunication to know which devices to subscribe to.
    /// </summary>
    public IReadOnlyList<ExternalDataSource> AllExternalSources => _allExternalSources;

    /// <summary>
    /// Event raised when telemetry data is received from the aggregator.
    /// PubSubDeviceBase subscribes to this and pushes data into SensorCache.
    /// </summary>
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Creates an AggregatorProtocolParser.
    /// </summary>
    /// <param name="communication">The aggregator communication instance</param>
    /// <param name="loggerFactory">Logger factory</param>
    public AggregatorProtocolParser(
        AggregatorCommunication communication,
        ILoggerFactory? loggerFactory = null)
    {
        var factory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = factory.CreateLogger<AggregatorProtocolParser>();

        // Subscribe to communication's data events
        _communication.OnDataReceived += OnDataReceived;
    }

    /// <summary>
    /// Gets the number of registered definitions.
    /// </summary>
    public int DefinitionCount => _definitions.Count;

    /// <summary>
    /// Registers a sensor and creates its corresponding IAggregatorDefinition.
    /// Uses Sensor.Name as the key since ResourceId is not available until after cloud registration.
    /// </summary>
    /// <param name="sensor">The sensor configuration with Parameters defining source mappings</param>
    /// <param name="definitionFactory">Factory to create IAggregatorDefinition from Sensor</param>
    public void RegisterSensor(Sensor sensor, DefinitionFactory definitionFactory)
    {
        ArgumentNullException.ThrowIfNull(sensor);
        ArgumentNullException.ThrowIfNull(definitionFactory);

        // Use Sensor.Name as key since ResourceId is not available at construction time
        if (_definitions.ContainsKey(sensor.Name))
        {
            _logger.LogWarning(
                "Sensor '{SensorName}' already registered, skipping",
                sensor.Name);
            return;
        }

        try
        {
            var definition = definitionFactory(sensor);
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
    /// Called when data is received from AggregatorCommunication.
    /// Routes the data to all registered definitions.
    /// </summary>
    private void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp)
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
            _logger.LogDebug("Forwarding {Count} aggregated measures to PubSubDeviceBase", allResults.Count);
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
    /// Starts the aggregator subscription by connecting to source devices.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting aggregator parser subscription");
        var connected = await _communication.ConnectAsync(cancellationToken);
        if (!connected)
        {
            _logger.LogError("Failed to start aggregator parser - could not connect to source devices");
            throw new InvalidOperationException("Failed to connect to source devices");
        }
        _logger.LogInformation("Aggregator parser subscription started");
    }

    /// <summary>
    /// Stops the aggregator subscription.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Stopping aggregator parser subscription");
        await _communication.DisconnectAsync(cancellationToken);
        _logger.LogInformation("Aggregator parser subscription stopped");
    }

    /// <summary>
    /// Execute command (not supported by aggregator).
    /// </summary>
    public Task<ErrorOr<object>> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AggregatorProtocolParser received command: {Command}", command.DeviceCmd);
        return Task.FromResult<ErrorOr<object>>(Error.Failure("Aggregator does not support command execution"));
    }

    public void Dispose()
    {
        _communication.OnDataReceived -= OnDataReceived;
        _communication.Dispose();
        GC.SuppressFinalize(this);
    }
}

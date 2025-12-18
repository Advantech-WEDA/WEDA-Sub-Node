using Microsoft.Extensions.Logging;
using PowerAggregationExample.Aggregation;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Communication;

/// <summary>
/// PubSub-style aggregator communication that subscribes to multiple external devices
/// and forwards data to AggregatorProtocolParser via OnDataReceived event.
///
/// Architecture:
/// - Communication layer (AggregatorCommunication): Subscribes to external devices via DeviceRegistry
/// - Parser layer (AggregatorProtocolParser): Routes data to Definitions, manages aggregation
/// - Definition layer (IAggregatorDefinition): Pure calculation logic (e.g., P = V × I)
///
/// This communication:
/// 1. Subscribes to DataProcessed events from source devices
/// 2. Builds ResourceId → SourceKey mapping for data routing
/// 3. Transforms incoming data (ResourceId → SourceKey)
/// 4. Raises OnDataReceived event for AggregatorProtocolParser to consume
/// </summary>
public class AggregatorCommunication : ICommunication
{
    private readonly ILogger<AggregatorCommunication> _logger;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly Dictionary<string, IDevice> _sourceDevices = new();

    /// <summary>
    /// External sources to subscribe to.
    /// </summary>
    private readonly List<ExternalDataSource> _externalSources = new();

    /// <summary>
    /// Maps ResourceId (UUID) to SourceKey (DeviceName/SensorName).
    /// Built during ConnectAsync when subscribing to source devices.
    /// </summary>
    private readonly Dictionary<string, string> _resourceIdToSourceKey = new();

    public CommunicationState State { get; private set; } = CommunicationState.Disconnected;
    public bool IsConnected => State == CommunicationState.Connected;
    public ConnectionSettings Settings { get; } = new ConnectionSettings();
    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    /// <summary>
    /// Event raised when data is received from source devices.
    /// AggregatorProtocolParser subscribes to this event.
    /// Parameters: sourceName, data (with SourceKey as ResourceId), timestamp
    /// </summary>
    public event Action<string, IReadOnlyList<TelemetryMeasure>, DateTimeOffset>? OnDataReceived;

    /// <summary>
    /// Creates an AggregatorCommunication.
    /// </summary>
    /// <param name="deviceRegistry">Device registry for discovering source devices</param>
    /// <param name="logger">Logger instance</param>
    public AggregatorCommunication(
        IDeviceRegistry deviceRegistry,
        ILogger<AggregatorCommunication>? logger = null)
    {
        _deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            .CreateLogger<AggregatorCommunication>();
    }

    /// <summary>
    /// Adds an external source to subscribe to.
    /// Call this before ConnectAsync.
    /// </summary>
    public void AddExternalSource(ExternalDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!_externalSources.Any(s => s.SourceKey.Equals(source.SourceKey, StringComparison.OrdinalIgnoreCase)))
        {
            _externalSources.Add(source);
            _logger.LogDebug(
                "[AggregatorCommunication] Added external source: {SourceKey}",
                source.SourceKey);
        }
    }

    /// <summary>
    /// Gets all registered external sources.
    /// </summary>
    public IReadOnlyList<ExternalDataSource> ExternalSources => _externalSources;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation(
                "[AggregatorCommunication] ConnectAsync starting with {Count} external sources",
                _externalSources.Count);

            // Subscribe to all external sources
            foreach (var source in _externalSources)
            {
                // Skip if already subscribed (deduplicated by DeviceName)
                if (_sourceDevices.ContainsKey(source.DeviceName))
                {
                    _logger.LogDebug(
                        "[AggregatorCommunication] Already subscribed to device '{DeviceName}', adding sensor mapping only",
                        source.DeviceName);

                    // Still need to add the sensor mapping for this source
                    AddSensorMapping(_sourceDevices[source.DeviceName], source);
                    continue;
                }

                _logger.LogInformation(
                    "[AggregatorCommunication] Looking for device '{DeviceName}' (Sensor={SensorName}) in registry",
                    source.DeviceName, source.SensorName);

                var device = _deviceRegistry.GetDevice(source.DeviceName);
                if (device == null)
                {
                    _logger.LogError("External source device '{DeviceName}' not found in registry", source.DeviceName);
                    return false;
                }

                _logger.LogInformation(
                    "[AggregatorCommunication] Found device '{DeviceName}', state={State}",
                    source.DeviceName, device.ConnectionState);

                // Wait for device to be connected (up to 30 seconds)
                var waitTime = TimeSpan.FromSeconds(30);
                var startTime = DateTime.UtcNow;
                while (device.ConnectionState != CommunicationState.Connected)
                {
                    if (DateTime.UtcNow - startTime > waitTime)
                    {
                        _logger.LogError(
                            "External source device '{DeviceName}' not connected after {WaitTime}s. Current state: {State}",
                            source.DeviceName, waitTime.TotalSeconds, device.ConnectionState);
                        return false;
                    }

                    _logger.LogDebug(
                        "Waiting for external source '{DeviceName}' to connect. Current state: {State}",
                        source.DeviceName, device.ConnectionState);

                    await Task.Delay(500, ct);
                }

                _sourceDevices[source.DeviceName] = device;

                // Build ResourceId → SourceKey mapping for this source
                AddSensorMapping(device, source);

                // Enable DataProcessed tracking and subscribe to the event
                device.EnableDataProcessedTracking = true;
                device.DataProcessed += OnSourceDataProcessed;

                _logger.LogInformation(
                    "[AggregatorCommunication] Subscribed to '{DeviceName}' DataProcessed event, EnableDataProcessedTracking={Enabled}",
                    source.DeviceName, device.EnableDataProcessedTracking);
            }

            _logger.LogInformation(
                "[AggregatorCommunication] Connected: subscribed to {SourceCount} external sources: {Sources}",
                _sourceDevices.Count,
                string.Join(", ", _externalSources.Select(s => s.SourceKey)));

            if (_resourceIdToSourceKey.Count > 0)
            {
                _logger.LogInformation(
                    "[AggregatorCommunication] ResourceId mappings: {Mappings}",
                    string.Join(", ", _resourceIdToSourceKey.Select(kvp => $"{kvp.Value}={kvp.Key[..Math.Min(8, kvp.Key.Length)]}...")));
            }

            State = CommunicationState.Connected;
            StateChanged?.Invoke(this, new ConnectionStateChangedEvent(
                DeviceId: "aggregator",
                SubNodeType: SubNodeType.CustomDevice,
                PreviousState: CommunicationState.Disconnected,
                CurrentState: CommunicationState.Connected,
                Timestamp: DateTimeOffset.UtcNow));

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AggregatorCommunication connection cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect AggregatorCommunication");
            return false;
        }
    }

    /// <summary>
    /// Adds a ResourceId → SourceKey mapping for a sensor.
    /// </summary>
    private void AddSensorMapping(IDevice device, ExternalDataSource source)
    {
        var sensor = device.FindSensor(source.SensorName);
        if (sensor == null)
        {
            _logger.LogWarning(
                "Sensor '{SensorName}' not found in device '{DeviceName}'",
                source.SensorName, source.DeviceName);
            return;
        }

        if (string.IsNullOrEmpty(sensor.ResourceId))
        {
            _logger.LogWarning(
                "Sensor '{SensorName}' in device '{DeviceName}' has no ResourceId yet",
                source.SensorName, source.DeviceName);
            return;
        }

        _resourceIdToSourceKey[sensor.ResourceId] = source.SourceKey;

        _logger.LogDebug(
            "[AggregatorCommunication] Mapped ResourceId '{ResourceId}' to SourceKey '{SourceKey}'",
            sensor.ResourceId, source.SourceKey);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        // Unsubscribe from all source devices
        foreach (var (deviceName, device) in _sourceDevices)
        {
            device.DataProcessed -= OnSourceDataProcessed;
            device.EnableDataProcessedTracking = false;
            _logger.LogDebug("Unsubscribed from external source: {DeviceName}", deviceName);
        }

        _sourceDevices.Clear();
        _resourceIdToSourceKey.Clear();

        var previousState = State;
        State = CommunicationState.Disconnected;

        StateChanged?.Invoke(this, new ConnectionStateChangedEvent(
            DeviceId: "aggregator",
            SubNodeType: SubNodeType.CustomDevice,
            PreviousState: previousState,
            CurrentState: CommunicationState.Disconnected,
            Timestamp: DateTimeOffset.UtcNow));

        _logger.LogInformation("AggregatorCommunication disconnected");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles DataProcessed events from source devices.
    /// Transforms ResourceId to SourceKey and raises OnDataReceived event.
    /// </summary>
    private void OnSourceDataProcessed(object? sender, DataProcessedEvent e)
    {
        _logger.LogDebug(
            "[Aggregator] Received DataProcessed event: DeviceId={DeviceId}, DataCount={Count}",
            e.DeviceId, e.Data.Count);

        // Find which device sent this data
        var sourceDevice = _sourceDevices.FirstOrDefault(kvp => ReferenceEquals(kvp.Value, sender));
        if (sourceDevice.Key == null)
        {
            _logger.LogWarning("Received data from unknown device");
            return;
        }

        var sourceName = sourceDevice.Key;

        // Transform data: replace ResourceId with SourceKey for matching
        var transformedData = new List<TelemetryMeasure>();
        foreach (var measure in e.Data)
        {
            if (_resourceIdToSourceKey.TryGetValue(measure.ResourceId, out var sourceKey))
            {
                // Create a new measure with SourceKey as ResourceId for matching
                transformedData.Add(new TelemetryMeasure
                {
                    ResourceId = sourceKey,
                    Value = measure.Value,
                    Timestamp = measure.Timestamp,
                    Metadata = measure.Metadata
                });

                _logger.LogDebug(
                    "[Aggregator] Transformed ResourceId '{ResourceId}' to SourceKey '{SourceKey}'",
                    measure.ResourceId, sourceKey);
            }
            else
            {
                _logger.LogWarning(
                    "[Aggregator] No mapping found for ResourceId '{ResourceId}' from device '{DeviceName}'",
                    measure.ResourceId, sourceName);
            }
        }

        if (transformedData.Count > 0)
        {
            // Raise OnDataReceived event for AggregatorProtocolParser
            OnDataReceived?.Invoke(sourceName, transformedData, e.Timestamp);
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}

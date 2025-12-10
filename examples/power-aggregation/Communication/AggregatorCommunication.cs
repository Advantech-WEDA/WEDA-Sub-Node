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
/// and delegates aggregation logic to an IAggregatorDefinition.
///
/// This communication pattern:
/// 1. Subscribes to DataProcessed events from external source devices
/// 2. Forwards received data to the IAggregatorDefinition for caching
/// 3. Triggers aggregation and raises OnTelemetryReceived when data is ready
///
/// Architecture:
/// - Source devices emit DataProcessed after transform/filter pipeline processing
/// - AggregatorCommunication listens to these events and forwards to IAggregatorDefinition
/// - IAggregatorDefinition handles caching, synchronization, and aggregation calculation
/// - When aggregation is ready, OnTelemetryReceived is raised for PubSubDeviceBase
/// </summary>
public class AggregatorCommunication : ICommunication
{
    private readonly ILogger<AggregatorCommunication> _logger;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IAggregatorDefinition _aggregatorDefinition;
    private readonly Dictionary<string, IDevice> _sourceDevices = new();

    public CommunicationState State { get; private set; } = CommunicationState.Disconnected;
    public bool IsConnected => State == CommunicationState.Connected;
    public ConnectionSettings Settings { get; } = new ConnectionSettings();
    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    /// <summary>
    /// Event raised when aggregated telemetry data is ready.
    /// This mimics the IPubSubProtocolParser.OnTelemetryReceived pattern.
    /// </summary>
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Creates an AggregatorCommunication with an aggregator definition.
    /// </summary>
    /// <param name="deviceRegistry">Device registry for discovering source devices</param>
    /// <param name="aggregatorDefinition">The aggregation definition that handles caching and calculation</param>
    /// <param name="logger">Logger instance</param>
    public AggregatorCommunication(
        IDeviceRegistry deviceRegistry,
        IAggregatorDefinition aggregatorDefinition,
        ILogger<AggregatorCommunication>? logger = null)
    {
        _deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        _aggregatorDefinition = aggregatorDefinition ?? throw new ArgumentNullException(nameof(aggregatorDefinition));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            .CreateLogger<AggregatorCommunication>();
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var externalSources = _aggregatorDefinition.ExternalSources;

            _logger.LogInformation(
                "[AggregatorCommunication] ConnectAsync starting with {Count} external sources",
                externalSources.Count);

            // Subscribe to all external sources
            foreach (var source in externalSources)
            {
                _logger.LogInformation(
                    "[AggregatorCommunication] Looking for device '{DeviceName}' (SourceKey={SourceKey}) in registry",
                    source.DeviceName, source.SourceKey);

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
                string.Join(", ", externalSources.Select(s => $"{s.GetDisplayName()}[{s.SourceKey}]")));

            State = CommunicationState.Connected;
            StateChanged?.Invoke(this, new ConnectionStateChangedEvent(
                DeviceId: "aggregator",
                DeviceType: DeviceType.CustomDevice,
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
        _aggregatorDefinition.Reset();

        var previousState = State;
        State = CommunicationState.Disconnected;

        StateChanged?.Invoke(this, new ConnectionStateChangedEvent(
            DeviceId: "aggregator",
            DeviceType: DeviceType.CustomDevice,
            PreviousState: previousState,
            CurrentState: CommunicationState.Disconnected,
            Timestamp: DateTimeOffset.UtcNow));

        _logger.LogInformation("AggregatorCommunication disconnected");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles DataProcessed events from source devices.
    /// Forwards data to IAggregatorDefinition and triggers aggregation.
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

        // Forward data to the aggregator definition
        _aggregatorDefinition.OnDataReceived(sourceName, e.Data, e.Timestamp);

        // Try to aggregate and raise event if successful
        TryRaiseAggregatedTelemetry();
    }

    /// <summary>
    /// Tries to aggregate and raise OnTelemetryReceived if successful.
    /// </summary>
    private void TryRaiseAggregatedTelemetry()
    {
        if (!_aggregatorDefinition.CanAggregate())
        {
            return;
        }

        var aggregatedData = _aggregatorDefinition.Aggregate();
        if (aggregatedData != null)
        {
            OnTelemetryReceived?.Invoke(aggregatedData.ToList());
        }
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}

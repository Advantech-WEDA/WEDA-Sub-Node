using Microsoft.Extensions.Logging;
using PowerAggregationExample.Aggregation;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Communication;

/// <summary>
/// In-process aggregator communication that subscribes to multiple external devices
/// and aggregates their data using a pluggable IAggregatorDefinition.
///
/// This communication pattern:
/// 1. Subscribes to DataReceived events from external source devices
/// 2. Forwards received data to the IAggregatorDefinition for processing
/// 3. Provides aggregated telemetry when requested
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
    /// Creates an AggregatorCommunication with an IAggregatorDefinition.
    /// </summary>
    /// <param name="deviceRegistry">Device registry for discovering source devices</param>
    /// <param name="aggregatorDefinition">The aggregator definition that specifies external sources and aggregation logic</param>
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

    public Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            // Subscribe to all external sources defined in the aggregator definition
            foreach (var source in _aggregatorDefinition.ExternalSources)
            {
                var device = _deviceRegistry.GetDevice(source.DeviceName);
                if (device == null)
                {
                    _logger.LogError("External source device '{DeviceName}' not found in registry", source.DeviceName);
                    return Task.FromResult(false);
                }

                _sourceDevices[source.DeviceName] = device;
                device.DataReceived += OnDataReceived;

                _logger.LogDebug("Subscribed to external source: {SourceName}", source.GetDisplayName());
            }

            _logger.LogInformation(
                "AggregatorCommunication connected: subscribed to {SourceCount} external sources: {Sources}",
                _sourceDevices.Count,
                string.Join(", ", _aggregatorDefinition.ExternalSources.Select(s => s.GetDisplayName())));

            State = CommunicationState.Connected;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect AggregatorCommunication");
            return Task.FromResult(false);
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        // Unsubscribe from all source devices
        foreach (var (deviceName, device) in _sourceDevices)
        {
            device.DataReceived -= OnDataReceived;
            _logger.LogDebug("Unsubscribed from external source: {DeviceName}", deviceName);
        }

        _sourceDevices.Clear();
        State = CommunicationState.Disconnected;

        _logger.LogInformation("AggregatorCommunication disconnected");
        return Task.CompletedTask;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Find which device sent this data
        var sourceDevice = _sourceDevices.FirstOrDefault(kvp => ReferenceEquals(kvp.Value, sender));
        if (sourceDevice.Key == null)
        {
            _logger.LogWarning("Received data from unknown device");
            return;
        }

        // Forward to aggregator definition for processing
        _aggregatorDefinition.OnDataReceived(sourceDevice.Key, e.Data, e.Timestamp);
    }

    /// <summary>
    /// Gets the latest aggregated telemetry data from the aggregator definition.
    /// Returns null if aggregation cannot be performed yet.
    /// </summary>
    public IReadOnlyList<TelemetryMeasure>? GetAggregatedData()
    {
        return _aggregatorDefinition.GetAggregatedData();
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}

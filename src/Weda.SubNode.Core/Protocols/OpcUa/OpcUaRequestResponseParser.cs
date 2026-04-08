using ErrorOr;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication.OpcUa;

namespace Weda.SubNode.Core.Protocols.OpcUa;

/// <summary>
/// OPC-UA hybrid protocol parser implementing both Request-Response and Pub/Sub patterns.
///
/// - IRequestResponseProtocolParser: batch ReadNodesAsync for polling / fallback
/// - IPubSubProtocolParser: OPC-UA Subscription for server-push notifications
///
/// Both patterns share the same OPC-UA Session via OpcUaCommunication.
///
/// Architecture:
/// Device -> Parser -> Communication (OpcUaCommunication wrapping OPC-UA Session)
///   PubSub path:  StartAsync() creates Subscription → MonitoredItem callback → OnTelemetryReceived
///   ReqResp path: ReadTelemetryAsync() → batch ReadNodesAsync (used for fallback)
/// </summary>
public class OpcUaHybridParser : IRequestResponseProtocolParser, IPubSubProtocolParser
{
    private readonly OpcUaCommunication _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly int _defaultCommandTimeoutMs;
    private Dictionary<string, OpcUaNodeMapping> _sensorMetadata;

    // Subscription state
    private Subscription? _subscription;
    private readonly int _subscriptionPublishingIntervalMs;
    private readonly int _monitoredItemSamplingIntervalMs;

    public OpcUaHybridParser(
        DeviceConfiguration configuration,
        OpcUaCommunication communication,
        ILogger logger,
        int defaultCommandTimeoutMs = 30000,
        int subscriptionPublishingIntervalMs = 1000,
        int monitoredItemSamplingIntervalMs = 500)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultCommandTimeoutMs = defaultCommandTimeoutMs;
        _subscriptionPublishingIntervalMs = subscriptionPublishingIntervalMs;
        _monitoredItemSamplingIntervalMs = monitoredItemSamplingIntervalMs;

        _sensorMetadata = configuration.Sensors
            .Select(s => s.ToOpcUaNodeMapping())
            .ToDictionary(n => n.Name, n => n);

        _logger.LogDebug(
            "OPC-UA hybrid parser initialized with {SensorCount} sensors",
            _sensorMetadata.Count);
    }

    // ===== IProtocolParserCore Implementation =====

    public ICommunication Communication => _communication;

    public void RefreshSensorMetadata()
    {
        _sensorMetadata = _configuration.Sensors
            .Select(s => s.ToOpcUaNodeMapping())
            .ToDictionary(n => n.Name, n => n);

        _logger.LogDebug(
            "Refreshed OPC-UA sensor metadata: {SensorCount} sensors",
            _sensorMetadata.Count);
    }

    // ===== IPubSubProtocolParser Implementation =====

    /// <inheritdoc />
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Start OPC-UA Subscription for all enabled sensors.
    /// Creates MonitoredItems that push value changes via OnTelemetryReceived.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var enabledSensors = _configuration.Sensors
            .Where(s => s.IsEffectivelyEnabled)
            .ToList();

        if (enabledSensors.Count == 0)
        {
            _logger.LogInformation("No enabled sensors for OPC-UA subscription");
            return;
        }

        // Build resourceId lookup
        var resourceIdLookup = enabledSensors.ToDictionary(s => s.Name, s => s.ResourceId);

        // Create subscription
        _subscription = await _communication.CreateSubscriptionAsync(
            _subscriptionPublishingIntervalMs, cancellationToken);

        // Add monitored items for each sensor
        foreach (var sensor in enabledSensors)
        {
            if (!_sensorMetadata.TryGetValue(sensor.Name, out var mapping))
                continue;

            var nodeId = mapping.ToNodeId();
            _communication.AddMonitoredItem(
                _subscription,
                nodeId,
                _monitoredItemSamplingIntervalMs,
                (sender, e) => OnMonitoredItemNotification(
                    sender as MonitoredItem, e, sensor.Name, mapping, resourceIdLookup));
        }

        // Apply changes to activate all monitored items
        await _subscription.ApplyChangesAsync(cancellationToken);

        _logger.LogInformation(
            "OPC-UA subscription started with {Count} monitored items (publishing={PublishingMs}ms, sampling={SamplingMs}ms)",
            enabledSensors.Count,
            _subscriptionPublishingIntervalMs,
            _monitoredItemSamplingIntervalMs);
    }

    /// <summary>
    /// Stop OPC-UA Subscription and remove all monitored items.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_subscription != null)
        {
            try
            {
                await _subscription.DeleteAsync(silent: true);
                _subscription = null;
                _logger.LogInformation("OPC-UA subscription stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping OPC-UA subscription");
            }
        }
    }

    private void OnMonitoredItemNotification(
        MonitoredItem? item,
        MonitoredItemNotificationEventArgs e,
        string sensorName,
        OpcUaNodeMapping mapping,
        Dictionary<string, string> resourceIdLookup)
    {
        try
        {
            if (e.NotificationValue is not MonitoredItemNotification notification)
                return;

            var dataValue = notification.Value;
            if (!StatusCode.IsGood(dataValue.StatusCode))
            {
                _logger.LogWarning(
                    "OPC-UA subscription notification bad status for {SensorName}: {StatusCode}",
                    sensorName, dataValue.StatusCode);
                return;
            }

            var convertedValue = ConvertValue(dataValue.Value, mapping);
            if (!resourceIdLookup.TryGetValue(sensorName, out var resourceId))
                return;

            var measure = new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = convertedValue,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _logger.LogDebug(
                "[SUB] {SensorName} = {Value} (NodeId: {NodeId})",
                sensorName, convertedValue, mapping.NodeId);

            OnTelemetryReceived?.Invoke([measure]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing subscription notification for {SensorName}", sensorName);
        }
    }

    // ===== IRequestResponseProtocolParser Implementation =====

    /// <summary>
    /// Read telemetry data via batch OPC-UA Read call.
    /// Used as fallback when Subscription has no data in the cache.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = new List<TelemetryMeasure>();

        var enabledSensors = _configuration.Sensors
            .Where(s => s.IsEffectivelyEnabled)
            .ToList();

        if (enabledSensors.Count == 0)
        {
            _logger.LogTrace("No enabled sensors to read");
            return measures;
        }

        var sensorMappings = enabledSensors
            .Where(s => _sensorMetadata.ContainsKey(s.Name))
            .Select(s => (Sensor: s, Mapping: _sensorMetadata[s.Name]))
            .ToList();

        if (sensorMappings.Count == 0)
        {
            _logger.LogTrace("No sensor metadata found for enabled sensors");
            return measures;
        }

        var nodesToRead = new ReadValueIdCollection();
        foreach (var (_, mapping) in sensorMappings)
        {
            nodesToRead.Add(new ReadValueId
            {
                NodeId = mapping.ToNodeId(),
                AttributeId = Attributes.Value
            });
        }

        try
        {
            var results = await _communication.ReadNodesAsync(nodesToRead, cancellationToken);

            for (int i = 0; i < sensorMappings.Count && i < results.Count; i++)
            {
                var (sensor, mapping) = sensorMappings[i];
                var dataValue = results[i];

                if (StatusCode.IsGood(dataValue.StatusCode))
                {
                    var convertedValue = ConvertValue(dataValue.Value, mapping);

                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = sensor.ResourceId,
                        Value = convertedValue,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });

                    _logger.LogDebug(
                        "[READ] {SensorName} = {Value} (NodeId: {NodeId})",
                        sensor.Name, convertedValue, mapping.NodeId);
                }
                else
                {
                    _logger.LogError(
                        "Failed to read OPC-UA node {NodeId} for sensor {SensorName}: {StatusCode}",
                        mapping.NodeId, sensor.Name, dataValue.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading OPC-UA nodes ({Count} nodes)", nodesToRead.Count);
        }

        return measures;
    }

    /// <summary>
    /// Read telemetry for specific sensors only (used by fallback in OpcUaDevice).
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = sensorResourceIds.ToHashSet();
        var all = await ReadTelemetryAsync(cancellationToken);
        return all.Where(m => requestedIds.Contains(m.ResourceId)).ToList();
    }

    /// <summary>
    /// Execute command (WriteNode). Shared by both patterns.
    /// </summary>
    public async Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing OPC-UA command {CommandName}", command.DeviceCmd);

        var timeoutMs = command.Timeout > 0 ? (int)command.Timeout : _defaultCommandTimeoutMs;
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return command.DeviceCmd switch
            {
                "WriteNode" or "SetValue" => await ExecuteWriteNodeAsync(command, linkedCts.Token),
                _ => Error.Validation(
                    code: "Command.NotSupported",
                    description: $"Command '{command.DeviceCmd}' is not supported by OPC-UA protocol")
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogError("Command {CommandName} timeout after {Timeout}ms", command.DeviceCmd, timeoutMs);
            return Error.Failure("Command.Timeout", $"Command execution exceeded {timeoutMs}ms timeout");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Error.Failure("Command.Cancelled", "Command execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandName}", command.DeviceCmd);
            return Error.Failure("Command.Error", $"Command execution failed: {ex.Message}");
        }
    }

    private async Task<ErrorOr<object>> ExecuteWriteNodeAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        var parameters = command.Parameters;
        if (parameters == null)
            return Error.Validation("Command.MissingParameters", "WriteNode command requires parameters");

        if (!parameters.TryGetValue("nodeId", out var nodeIdObj) || nodeIdObj == null)
            return Error.Validation("Command.MissingNodeId", "WriteNode command requires 'nodeId' parameter");

        if (!parameters.TryGetValue("value", out var valueObj) || valueObj == null)
            return Error.Validation("Command.MissingValue", "WriteNode command requires 'value' parameter");

        var namespaceIndex = parameters.TryGetValue("namespaceIndex", out var nsObj)
            ? Convert.ToUInt16(nsObj) : (ushort)2;

        var nodeIdStr = nodeIdObj.ToString()!;
        var nodeId = nodeIdStr.Contains('=')
            ? NodeId.Parse(nodeIdStr)
            : new NodeId(nodeIdStr, namespaceIndex);

        var writeValue = new WriteValue
        {
            NodeId = nodeId,
            AttributeId = Attributes.Value,
            Value = new DataValue(new Variant(valueObj))
        };

        var results = await _communication.WriteNodesAsync(
            new WriteValueCollection { writeValue },
            cancellationToken);

        if (results.Count > 0 && StatusCode.IsGood(results[0]))
        {
            _logger.LogInformation("WriteNode succeeded: {NodeId} = {Value}", nodeIdStr, valueObj);
            return (object)true;
        }

        var statusCode = results.Count > 0 ? results[0].ToString() : "Unknown";
        _logger.LogWarning("WriteNode failed for {NodeId}: {StatusCode}", nodeIdStr, statusCode);
        return Error.Failure("Command.WriteFailed", $"WriteNode failed with status: {statusCode}");
    }

    /// <summary>
    /// Convert OPC-UA Variant value to C# type, applying scale and offset.
    /// </summary>
    internal static object? ConvertValue(object? rawValue, OpcUaNodeMapping mapping)
    {
        if (rawValue == null) return null;

        double numericValue;

        try
        {
            numericValue = mapping.DataType switch
            {
                OpcUaDataType.Boolean => Convert.ToBoolean(rawValue) ? 1.0 : 0.0,
                OpcUaDataType.SByte => Convert.ToSByte(rawValue),
                OpcUaDataType.Byte => Convert.ToByte(rawValue),
                OpcUaDataType.Int16 => Convert.ToInt16(rawValue),
                OpcUaDataType.UInt16 => Convert.ToUInt16(rawValue),
                OpcUaDataType.Int32 => Convert.ToInt32(rawValue),
                OpcUaDataType.UInt32 => Convert.ToUInt32(rawValue),
                OpcUaDataType.Int64 => Convert.ToInt64(rawValue),
                OpcUaDataType.UInt64 => Convert.ToUInt64(rawValue),
                OpcUaDataType.Float => Convert.ToSingle(rawValue),
                OpcUaDataType.Double => Convert.ToDouble(rawValue),
                OpcUaDataType.String => double.NaN,
                OpcUaDataType.DateTime => double.NaN,
                OpcUaDataType.ByteString => double.NaN,
                _ => Convert.ToDouble(rawValue)
            };
        }
        catch
        {
            return rawValue;
        }

        if (double.IsNaN(numericValue))
            return rawValue;

        if (mapping.Scale != 1.0 || mapping.Offset != 0.0)
            numericValue = numericValue * mapping.Scale + mapping.Offset;

        return numericValue;
    }
}

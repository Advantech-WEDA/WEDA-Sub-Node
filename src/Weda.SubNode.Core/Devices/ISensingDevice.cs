using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.ISensing;
using Weda.SubNode.Core.Protocols.ISensing.Models;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// ISensing protocol device implementation.
/// Handles ISensing JSON protocol parsing and message broker communication.
/// This is the protocol-specific implementation similar to ModbusDevice.
/// </summary>
public class ISensingDevice : DeviceBase, ISensorControl
{
    private readonly IMessageBroker _messageBroker;
    private readonly IProtocolParser _protocolParser;
    private readonly string _dataTopic;
    private readonly string _statusTopic;

    // Cache for latest telemetry data by ResourceId
    private readonly ConcurrentDictionary<string, TelemetryMeasure> _latestData = new();

    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of ISensingDevice with ApplicationContext.
    /// Similar to ModbusDevice, this accepts an IMessageBroker parameter.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing ISensing settings.</param>
    /// <param name="messageBroker">Message broker instance (MQTT, NATS, etc.).</param>
    public ISensingDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IMessageBroker messageBroker)
        : base(context, configuration, messageBroker)
    {
        _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        _protocolParser = new ISensingProtocolParser(messageBroker);

        // Extract MQTT topics from configuration
        // Expected format: "Advantech/{MacAddress}/data" and "Advantech/{MacAddress}/status"
        var macAddress = configuration.Communication.TryGetValue("MacAddress", out var mac)
            ? mac?.ToString() ?? throw new InvalidOperationException("MacAddress not found in communication configuration")
            : throw new InvalidOperationException("MacAddress not found in communication configuration");

        var manufacturer = configuration.Communication.TryGetValue("Manufacturer", out var mfg)
            ? mfg?.ToString() ?? "Advantech"
            : "Advantech";

        _dataTopic = $"{manufacturer}/{macAddress}/data";
        _statusTopic = $"{manufacturer}/{macAddress}/status";
    }

    /// <summary>
    /// Reads telemetry data from the device cache.
    /// Returns the latest received data from MQTT messages.
    /// </summary>
    public override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = _latestData.Values.ToList();

        if (measures.Count > 0)
        {
            _logger.LogDebug("Returning {Count} cached telemetry measures", measures.Count);
            RaiseDataReceived(measures);
        }

        return Task.FromResult(measures);
    }

    /// <summary>
    /// Executes a command on the device (publishes command to message broker topic).
    /// </summary>
    public override Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on ISensing device {DeviceId}", command.DeviceCmd, DeviceId);
        // TODO: Implement command publishing to MQTT topics when command protocol is defined
        return Task.FromResult(true);
    }

    /// <summary>
    /// Starts background tasks for MQTT subscription and health monitoring.
    /// </summary>
    protected override Task StartBackgroundTasksAsync(CancellationToken cancellationToken = default)
    {
        _backgroundTasksCts = new CancellationTokenSource();
        var cts = _backgroundTasksCts.Token;

        // Subscribe to MQTT message events
        _messageBroker.MessageReceived += OnMessageReceived;

        _telemetryTask = Task.Run(async () =>
        {
            try
            {
                // Subscribe to data and status topics
                _logger.LogInformation("Subscribing to ISensing topics: {DataTopic}, {StatusTopic}", _dataTopic, _statusTopic);

                await _messageBroker.SubscribeAsync(_dataTopic, cts);
                await _messageBroker.SubscribeAsync(_statusTopic, cts);

                _logger.LogInformation("Successfully subscribed to ISensing topics");

                // Keep task alive to maintain subscriptions
                await Task.Delay(Timeout.Infinite, cts);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("ISensing telemetry task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ISensing telemetry task");
            }
        }, cts);

        _healthTask = Task.Run(async () =>
        {
            var period = Configuration.Periods.ReportHealth;
            _logger.LogInformation("Starting health reporting task with period {Period}ms", period);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await ReportHealthAsync(cts);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health reporting task for device {DeviceId}", DeviceId);
                }

                await Task.Delay(period, cts);
            }
        }, cts);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles incoming messages from subscribed topics.
    /// Override this method in derived classes to customize message handling.
    /// </summary>
    /// <param name="sender">Event sender (typically the message broker)</param>
    /// <param name="e">Message event arguments containing topic and payload</param>
    protected virtual void OnMessageReceived(object? sender, MessageReceivedEvent<byte[]> e)
    {
        try
        {
            _logger.LogDebug("Received message on topic {Topic}", e.Topic);

            if (e.Topic == _dataTopic || e.Topic.EndsWith("/data"))
            {
                // Parse ISensing data message using JSON
                var jsonPayload = Encoding.UTF8.GetString(e.Payload);
                var sensorData = JsonSerializer.Deserialize<ISensingSensorData>(jsonPayload);

                if (sensorData == null || sensorData.AdditionalData == null || sensorData.AdditionalData.Count == 0)
                {
                    _logger.LogWarning("Failed to parse ISensing data from topic {Topic}", e.Topic);
                    return;
                }

                var measures = new List<TelemetryMeasure>();

                // Process each sensor channel in the AdditionalData
                foreach (var (channelName, channelValue) in sensorData.AdditionalData)
                {
                    // Find matching sensor in configuration by channel name
                    var sensor = Configuration.Sensors.FirstOrDefault(s =>
                        s.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));

                    if (sensor == null)
                    {
                        _logger.LogTrace("No sensor configuration found for channel {Channel}", channelName);
                        continue;
                    }

                    if (!sensor.Config.Enabled)
                    {
                        _logger.LogTrace("Sensor {SensorName} is disabled, skipping", sensor.Name);
                        continue;
                    }

                    // Extract numeric value from JsonElement
                    double value = channelValue.ValueKind switch
                    {
                        JsonValueKind.Number => channelValue.GetDouble(),
                        JsonValueKind.String => double.TryParse(channelValue.GetString(), out var d) ? d : 0,
                        _ => 0
                    };

                    var measure = new TelemetryMeasure
                    {
                        ResourceId = sensor.ResourceId,
                        Value = value,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    // Update cache
                    _latestData.AddOrUpdate(sensor.ResourceId, measure, (_, _) => measure);

                    measures.Add(measure);

                    _logger.LogDebug(
                        "[DATA] ISensing Telemetry: {SensorName} = {Value} (ResourceId: {ResourceId})",
                        sensor.Name,
                        value,
                        sensor.ResourceId);
                }

                if (measures.Count > 0)
                {
                    RaiseDataReceived(measures);

                    // Send telemetry through pipeline (applies transforms/filters and sends to cloud)
                    _ = SendTelemetryAsync(measures);
                }
            }
            else if (e.Topic == _statusTopic || e.Topic.EndsWith("/status"))
            {
                // Handle status messages
                _logger.LogDebug("Received status message: {Status}", Encoding.UTF8.GetString(e.Payload));
                // TODO: Parse and handle device status updates
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from topic {Topic}", e.Topic);
        }
    }

    #region ISensorControl Implementation

    /// <summary>
    /// Sets the state of a digital output
    /// </summary>
    public virtual Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("SetDigitalOutputAsync not yet implemented");
    }

    /// <summary>
    /// Sets the value of an analog output
    /// </summary>
    public virtual Task<bool> SetAnalogOutputAsync(string outputName, double value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("SetAnalogOutputAsync not yet implemented");
    }

    /// <summary>
    /// Requests current device configuration
    /// </summary>
    public virtual Task<Dictionary<string, object>> GetConfigurationAsync(ushort configIndex = 0, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("GetConfigurationAsync not yet implemented");
    }

    /// <summary>
    /// Updates device configuration
    /// </summary>
    public virtual Task<bool> SetConfigurationAsync(ushort configIndex, Dictionary<string, object> configData, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("SetConfigurationAsync not yet implemented");
    }

    /// <summary>
    /// Enables or disables a sensor
    /// </summary>
    public virtual Task<bool> SetSensorEnabledAsync(string sensorName, bool enabled, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("SetSensorEnabledAsync not yet implemented");
    }

    #endregion

    /// <summary>
    /// Disposes resources used by the device.
    /// </summary>
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backgroundTasksCts?.Cancel();
            _backgroundTasksCts?.Dispose();
        }
    }
}

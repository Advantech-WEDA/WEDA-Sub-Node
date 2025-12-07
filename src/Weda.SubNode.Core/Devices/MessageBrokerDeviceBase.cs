using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Base class for Publish-Subscribe (Message Broker) communication pattern devices.
/// Framework handles ReadTelemetryAsync and StartBackgroundTasksAsync internally.
/// User only needs to provide an IPublishSubscribeProtocolParser implementation.
///
/// Use this class for protocols like: MQTT, NATS, AMQP, Kafka
///
/// Architecture:
/// 1. External data (MQTT/NATS) pushes into SensorCache (like Modbus registers)
/// 2. Device samples from SensorCache at configured intervals (SensorConfig.Interval or Periods.ReadTelemetry)
/// 3. Device sends to cloud based on Periods.SendTelemetry:
///    - 0: Realtime mode - send immediately after each sample
///    - >0: Batch mode - collect samples and send at specified interval
///
/// Inheritance hierarchy example:
/// MyFirstISensingDevice -> MqttISensingDevice -> ISensingDevice -> MessageBrokerDeviceBase -> DeviceBase
/// </summary>
public class MessageBrokerDeviceBase : DeviceBase
{
    /// <summary>
    /// The protocol parser for this device. Protected to allow subclass access.
    /// </summary>
    protected readonly IPublishSubscribeProtocolParser _parser;

    /// <summary>
    /// Per-sensor cache for buffering pushed data.
    /// Acts like Modbus registers - stores latest values from external sources.
    /// </summary>
    protected readonly SensorCache _sensorCache = new();

    private readonly ConcurrentQueue<TelemetryMeasure> _telemetryBatch = new();
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _subscriptionTask;
    private Task? _samplingTask;
    private Task? _batchSendTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of MessageBrokerDeviceBase.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration.</param>
    /// <param name="parser">Publish-Subscribe protocol parser (Parser owns Communication).</param>
    public MessageBrokerDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IPublishSubscribeProtocolParser parser)
        : base(context, configuration, parser.Communication)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        _logger.LogDebug(
            "MessageBrokerDeviceBase initialized with {ParserType}",
            _parser.GetType().Name);
    }

    /// <summary>
    /// Reads telemetry from the sensor cache.
    /// Returns the latest cached data that was pushed from external sources.
    /// Framework implementation - samples from SensorCache.
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    public sealed override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // Get enabled sensors from configuration
        var enabledSensors = Configuration.Sensors
            .Where(s => s.Config.Enabled)
            .Select(s => s.ResourceId)
            .ToList();

        // Read from cache for enabled sensors only
        var measures = _sensorCache.Read(enabledSensors);

        if (measures.Count > 0)
        {
            _logger.LogDebug("Sampled {Count} telemetry measures from cache", measures.Count);
            RaiseDataReceived(measures);
        }

        return Task.FromResult(measures);
    }

    /// <summary>
    /// Executes a command on the device using the parser.
    /// </summary>
    public override async Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on device {DeviceId}", command.DeviceCmd, DeviceId);

        var result = await _parser.ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("Command execution failed: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts background tasks for message broker subscription, sampling, and health reporting.
    /// Framework implementation with automatic event handling.
    /// Sealed to prevent subclasses from overriding framework logic.
    ///
    /// Tasks:
    /// 1. Subscription task - maintains connection to message broker, pushes data into SensorCache
    /// 2. Sampling task - reads from SensorCache at configured intervals
    /// 3. Batch send task (if batch mode) - sends collected data at configured intervals
    /// 4. Health task - periodic health reporting
    /// </summary>
    protected sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        _backgroundTasksCts = new CancellationTokenSource();
        var cts = _backgroundTasksCts.Token;

        var readPeriod = Configuration.Periods.ReadTelemetry;
        var sendPeriod = Configuration.Periods.SendTelemetry;
        var isRealtimeMode = sendPeriod <= 0;

        _logger.LogDebug(
            "Starting message broker device: ReadPeriod={ReadPeriod}ms, SendPeriod={SendPeriod}ms, Mode={Mode}",
            readPeriod, sendPeriod, isRealtimeMode ? "Realtime" : "Batch");

        // Subscribe to parser's OnTelemetryReceived event - pushes into SensorCache
        _parser.OnTelemetryReceived += OnTelemetryReceived;

        // 1. Subscription task - maintains message broker connection
        _subscriptionTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting message broker subscription");
                await _parser.StartAsync(cts);
                _logger.LogInformation("Message broker subscription established");

                // Keep task alive to maintain subscription
                await Task.Delay(Timeout.Infinite, cts);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogInformation("Subscription task cancelled for device {DeviceId}", DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in subscription task for device {DeviceId}", DeviceId);
            }
            finally
            {
                try
                {
                    await _parser.StopAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping parser subscription");
                }
            }
        }, cts);

        // 2. Sampling task - reads from SensorCache at configured intervals
        _samplingTask = Task.Run(async () =>
        {
            _logger.LogDebug("Starting sampling task with period {Period}ms", readPeriod);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var measures = await ReadTelemetryAsync(cts);

                    if (measures.Count > 0)
                    {
                        // Record telemetry in health monitor
                        _orchestrator.HealthMonitor.RecordTelemetryReadDuration(TimeSpan.Zero);

                        if (isRealtimeMode)
                        {
                            // Realtime mode: send immediately
                            await SendTelemetryAsync(measures, cts);
                        }
                        else
                        {
                            // Batch mode: add to queue for later sending
                            foreach (var measure in measures)
                            {
                                _telemetryBatch.Enqueue(measure);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in sampling task for device {DeviceId}", DeviceId);
                }

                await Task.Delay(readPeriod, cts);
            }
        }, cts);

        // 3. Batch send task (only in batch mode)
        if (!isRealtimeMode)
        {
            _batchSendTask = Task.Run(async () =>
            {
                _logger.LogDebug("Starting batch send task with period {Period}ms", sendPeriod);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(sendPeriod, cts);

                        var batch = new List<TelemetryMeasure>();
                        while (_telemetryBatch.TryDequeue(out var measure))
                        {
                            batch.Add(measure);
                        }

                        if (batch.Count > 0)
                        {
                            _logger.LogDebug(
                                "Sending telemetry batch: {Count} measures for device {DeviceId}",
                                batch.Count, DeviceId);
                            await SendTelemetryAsync(batch, cts);
                        }
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested)
                    {
                        // Send remaining data before exit
                        var remaining = new List<TelemetryMeasure>();
                        while (_telemetryBatch.TryDequeue(out var measure))
                        {
                            remaining.Add(measure);
                        }
                        if (remaining.Count > 0)
                        {
                            _logger.LogDebug("Sending remaining {Count} measures before shutdown", remaining.Count);
                            await SendTelemetryAsync(remaining, CancellationToken.None);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in batch send task for device {DeviceId}", DeviceId);
                    }
                }
            }, cts);
        }

        // 4. Health reporting task
        _healthTask = Task.Run(async () =>
        {
            var period = Configuration.Periods.ReportHealth;
            _logger.LogDebug("Starting health reporting task with period {Period}ms", period);

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
    /// Handles telemetry data received from parser subscription.
    /// Pushes data into SensorCache (like writing to Modbus registers).
    /// </summary>
    private void OnTelemetryReceived(List<TelemetryMeasure> measures)
    {
        try
        {
            // Push into sensor cache (like Modbus registers)
            _sensorCache.Push(measures);

            _logger.LogDebug(
                "Pushed {Count} telemetry measures into sensor cache",
                measures.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing telemetry into cache for device {DeviceId}", DeviceId);
        }
    }
}

using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Base class for Pub/Sub communication pattern devices.
/// Framework handles ReadTelemetryAsync and StartBackgroundTasksAsync internally.
/// User only needs to provide an IPubSubProtocolParser implementation.
///
/// Use this class for protocols like: MQTT, NATS, AMQP, Kafka
///
/// Architecture:
/// 1. External data (MQTT/NATS) pushes into SensorCache (like Modbus registers)
/// 2. Sensors are grouped by their Config.Interval
/// 3. Each interval group samples from SensorCache and enqueues via EnqueueTelemetryAsync
/// 4. Batch send task (in DeviceBase) sends collected data at CalculatedSendTelemetryPeriod
///
/// Telemetry flow: Pub/Sub → SensorCache → Interval Group Sample → EnqueueTelemetryAsync → Batch Send
///
/// Inheritance hierarchy example:
/// MyFirstISensingDevice -> MqttISensingDevice -> ISensingDevice -> PubSubDeviceBase -> DeviceBase
/// </summary>
public class PubSubDeviceBase : DeviceBase
{
    /// <summary>
    /// The protocol parser for this device. Protected to allow subclass access.
    /// </summary>
    protected readonly IPubSubProtocolParser _parser;

    /// <summary>
    /// Per-sensor cache for buffering pushed data.
    /// Acts like Modbus registers - stores latest values from external sources.
    /// </summary>
    protected readonly SensorCache _sensorCache = new();

    private readonly List<Task> _samplingTasks = new();
    private Task? _subscriptionTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of PubSubDeviceBase.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration.</param>
    /// <param name="parser">Pub/Sub protocol parser (Parser owns Communication).</param>
    public PubSubDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IPubSubProtocolParser parser)
        : base(context, configuration, parser.Communication)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        _logger.LogDebug(
            "PubSubDeviceBase initialized with {ParserType}",
            _parser.GetType().Name);
    }

    /// <summary>
    /// Reads telemetry from the sensor cache for all enabled sensors.
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
    /// Reads telemetry from the sensor cache for specific sensors only.
    /// Used by per-sensor interval scheduling.
    /// </summary>
    /// <param name="sensorResourceIds">ResourceIds of sensors to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of telemetry measures for specified sensors</returns>
    public Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds,
        CancellationToken cancellationToken = default)
    {
        var measures = _sensorCache.Read(sensorResourceIds);

        if (measures.Count > 0)
        {
            _logger.LogDebug("Sampled {Count} telemetry measures from cache for specific sensors", measures.Count);
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
    /// Internal sealed to prevent high-level devices from overriding framework logic.
    /// Batch send task is started by DeviceBase.
    ///
    /// Tasks:
    /// 1. Subscription task - maintains connection to message broker, pushes data into SensorCache
    /// 2. Interval-grouped sampling tasks - sample from SensorCache and enqueue via EnqueueTelemetryAsync
    /// 3. Health task - periodic health reporting
    /// </summary>
    internal sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        // Group sensors by their interval
        var sensorsByInterval = Configuration.Sensors
            .Where(s => s.Config.Enabled)
            .GroupBy(s => (int)s.Config.Interval)
            .ToList();

        _logger.LogDebug(
            "Starting message broker device: Groups={GroupCount}, TotalSensors={SensorCount}",
            sensorsByInterval.Count, Configuration.Sensors.Count(s => s.Config.Enabled));

        // Subscribe to parser's OnTelemetryReceived event - pushes into SensorCache
        _parser.OnTelemetryReceived += OnTelemetryReceived;

        // 1. Subscription task - maintains message broker connection
        _subscriptionTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting message broker subscription");
                await _parser.StartAsync(cancellationToken);
                _logger.LogInformation("Message broker subscription established");

                // Keep task alive to maintain subscription
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
        }, cancellationToken);

        // 2. Create a Task.Delay loop for each interval group (samples from SensorCache)
        foreach (var group in sensorsByInterval)
        {
            var interval = group.Key;
            var sensors = group.ToList();

            var samplingTask = RunIntervalGroupSamplingAsync(sensors, interval, cancellationToken);
            _samplingTasks.Add(samplingTask);

            _logger.LogDebug(
                "Created sampling task for interval {Interval}ms with {SensorCount} sensors: [{SensorNames}]",
                interval, sensors.Count, string.Join(", ", sensors.Select(s => s.Name)));
        }

        // 3. Health reporting task using Task.Delay loop
        var healthPeriod = Configuration.Periods.ReportHealth;

        _healthTask = Task.Run(async () =>
        {
            _logger.LogDebug("Starting health reporting task with period {Period}ms", healthPeriod);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await ReportHealthAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in health reporting task for device {DeviceId}", DeviceId);
                    }

                    await Task.Delay(healthPeriod, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Health reporting task cancelled for device {DeviceId}", DeviceId);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs the sampling loop for an interval group using Task.Delay loop.
    /// Samples from SensorCache, processes through transforms and DSP filters, then enqueues to batch.
    /// Task.Delay loop ensures no overlap - waits for previous iteration to complete before starting next.
    /// </summary>
    private async Task RunIntervalGroupSamplingAsync(
        List<Sensor> sensors,
        int intervalMs,
        CancellationToken cancellationToken)
    {
        // Wait for initial interval to allow message broker data to arrive
        try
        {
            await Task.Delay(intervalMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessIntervalGroupAsync(sensors, cancellationToken);
                await Task.Delay(intervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Sampling task cancelled for interval group with {SensorCount} sensors", sensors.Count);
        }
    }

    /// <summary>
    /// Processes an interval group: samples from cache and enqueues via EnqueueTelemetryAsync.
    /// </summary>
    private async Task ProcessIntervalGroupAsync(List<Sensor> sensors, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            // Step 1: Sample from SensorCache for all sensors in the group
            var sensorResourceIds = sensors.Select(s => s.ResourceId).ToList();
            var measures = _sensorCache.Read(sensorResourceIds);

            if (measures.Count == 0)
            {
                return;
            }

            RaiseDataReceived(measures);
            _orchestrator.HealthMonitor.RecordTelemetryReadDuration(TimeSpan.Zero);

            // Step 2: Transform, Filter, and Enqueue (handled by DeviceBase)
            await EnqueueTelemetryAsync(measures, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sampling interval group with {SensorCount} sensors", sensors.Count);
        }
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

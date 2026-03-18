using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
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
        : base(context, configuration, parser)
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
            .Where(s => s.IsEffectivelyEnabled)
            .Select(s => s.ResourceId)
            .ToList();

        // Read from cache for enabled sensors only
        var measures = _sensorCache.Read(enabledSensors);

        if (measures.Count > 0)
        {
            _logger.LogDebug("Sampled {Count} telemetry measures from cache", measures.Count);
            // RaiseDataReceived fires with RAW data before transform/filter
            // RaiseDataProcessed fires in EnqueueTelemetryAsync after transform/filter
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
            // RaiseDataReceived fires with RAW data before transform/filter
            // RaiseDataProcessed fires in EnqueueTelemetryAsync after transform/filter
            RaiseDataReceived(measures);
        }

        return Task.FromResult(measures);
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
    internal sealed override async Task StopDeviceTasksAsync()
    {
        var tasksToAwait = new List<Task>();

        if (_subscriptionTask is { IsCompleted: false })
            tasksToAwait.Add(_subscriptionTask);

        tasksToAwait.AddRange(_samplingTasks.Where(t => !t.IsCompleted));

        if (tasksToAwait.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasksToAwait).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timed out waiting for subscription/sampling tasks to stop for device {SubNodeId}", SubNodeId);
            }
            catch (OperationCanceledException)
            {
                // Expected when tasks are cancelled
            }
        }

        _subscriptionTask = null;
        _samplingTasks.Clear();
    }

    internal sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        // Clear previous tasks reference (for restart scenarios)
        _samplingTasks.Clear();

        // Unsubscribe first to avoid duplicate subscriptions on restart
        _parser.OnTelemetryReceived -= OnTelemetryReceived;

        // Group sensors by their interval (using helper from DeviceBase)
        var sensorGroups = GroupSensorsByInterval();

        // Skip subscription and sampling if no sensors are effectively enabled
        if (sensorGroups.Count == 0)
        {
            _logger.LogInformation(
                "No effectively enabled sensors, skipping message broker subscription for device {SubNodeId}",
                SubNodeId);
            return Task.CompletedTask;
        }

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
                _logger.LogInformation("Subscription task cancelled for device {SubNodeId}", SubNodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in subscription task for device {SubNodeId}", SubNodeId);
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

        // 2. Create interval loop tasks for each group (using helper from DeviceBase)
        foreach (var (intervalMs, sensors) in sensorGroups)
        {
            // Use RunIntervalLoopAsync with initialDelay=true to allow message broker data to arrive
            var samplingTask = RunIntervalLoopAsync(sensors, intervalMs, initialDelay: true, cancellationToken);
            _samplingTasks.Add(samplingTask);

            _logger.LogDebug(
                "Created sampling task for interval {Interval}ms with {SensorCount} sensors: [{SensorNames}]",
                intervalMs, sensors.Count, string.Join(", ", sensors.Select(s => s.Name)));
        }

        // Health task is now started by DeviceBase.StartAllBackgroundTasks()
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads sensors from the SensorCache for interval group processing.
    /// Implements the abstract method from DeviceBase (Template Method pattern).
    /// </summary>
    protected override Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
        List<string> sensorResourceIds,
        CancellationToken cancellationToken)
    {
        var measures = _sensorCache.Read(sensorResourceIds);
        return Task.FromResult(new IntervalGroupReadResult(measures, TimeSpan.Zero));
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
            _logger.LogError(ex, "Error pushing telemetry into cache for device {SubNodeId}", SubNodeId);
        }
    }
}

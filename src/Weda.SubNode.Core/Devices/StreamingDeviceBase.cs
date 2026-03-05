using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Base class for Streaming communication pattern devices.
/// Framework handles ReadTelemetryAsync and StartBackgroundTasksAsync internally.
/// User only needs to provide an IStreamingProtocolParser implementation.
///
/// Use this class for protocols like: WebSocket, gRPC streaming, SSE
///
/// Architecture:
/// 1. Stream data pushes into SensorCache (like Modbus registers)
/// 2. Sensors are grouped by their Config.Interval
/// 3. Each interval group samples from SensorCache and enqueues via EnqueueTelemetryAsync
/// 4. Batch send task (in DeviceBase) sends collected data at CalculatedSendTelemetryPeriod
///
/// Telemetry flow: Stream → SensorCache → Interval Group Sample → EnqueueTelemetryAsync → Batch Send
///
/// Inheritance hierarchy example:
/// MyStreamingDevice -> WebSocketStreamingDevice -> StreamingDeviceBase -> DeviceBase
/// </summary>
public class StreamingDeviceBase : DeviceBase
{
    /// <summary>
    /// The protocol parser for this device. Protected to allow subclass access.
    /// </summary>
    protected readonly IStreamingProtocolParser _parser;

    /// <summary>
    /// Per-sensor cache for buffering streamed data.
    /// Acts like Modbus registers - stores latest values from stream.
    /// </summary>
    protected readonly SensorCache _sensorCache = new();

    private readonly List<Task> _samplingTasks = new();
    private Task? _streamTask;

    /// <summary>
    /// Initializes a new instance of StreamingDeviceBase.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration.</param>
    /// <param name="parser">Streaming protocol parser (Parser owns Communication).</param>
    public StreamingDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IStreamingProtocolParser parser)
        : base(context, configuration, parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        _logger.LogDebug(
            "StreamingDeviceBase initialized with {ParserType}",
            _parser.GetType().Name);
    }

    /// <summary>
    /// Current stream state.
    /// </summary>
    public StreamState StreamState => _parser.StreamState;

    /// <summary>
    /// Reads telemetry from the sensor cache for all enabled sensors.
    /// Returns the latest cached data that was pushed from the stream.
    /// Framework implementation - samples from SensorCache.
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    public sealed override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // Get enabled sensors from configuration
        var enabledSensors = Configuration.Sensors
            .Where(s => s.Report.Enabled)
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
    /// Starts background tasks for streaming, sampling, and health reporting.
    /// Framework implementation with automatic stream handling.
    /// Batch send task is started by DeviceBase.
    ///
    /// Tasks:
    /// 1. Stream task - maintains stream connection, pushes data into SensorCache
    /// 2. Interval-grouped sampling tasks - sample from SensorCache and enqueue via EnqueueTelemetryAsync
    /// 3. Health task - periodic health reporting
    /// </summary>
    internal sealed override async Task StopDeviceTasksAsync()
    {
        var tasksToAwait = new List<Task>();

        if (_streamTask is { IsCompleted: false })
            tasksToAwait.Add(_streamTask);

        tasksToAwait.AddRange(_samplingTasks.Where(t => !t.IsCompleted));

        if (tasksToAwait.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasksToAwait).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timed out waiting for stream/sampling tasks to stop for device {SubNodeId}", SubNodeId);
            }
            catch (OperationCanceledException)
            {
                // Expected when tasks are cancelled
            }
        }

        _streamTask = null;
        _samplingTasks.Clear();
    }

    internal sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        // Clear previous tasks reference (for restart scenarios)
        _samplingTasks.Clear();

        // Unsubscribe first to avoid duplicate subscriptions on restart
        _parser.OnTelemetryReceived -= OnTelemetryReceived;
        _parser.OnStreamStateChanged -= OnStreamStateChanged;

        // Group sensors by their interval (using helper from DeviceBase)
        var sensorGroups = GroupSensorsByInterval();

        _logger.LogDebug(
            "Starting streaming device: Groups={GroupCount}, TotalSensors={SensorCount}",
            sensorGroups.Count, Configuration.Sensors.Count(s => s.Report.Enabled));

        // Subscribe to parser's events - pushes into SensorCache
        _parser.OnTelemetryReceived += OnTelemetryReceived;
        _parser.OnStreamStateChanged += OnStreamStateChanged;

        // 1. Stream task - maintains stream connection
        _streamTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting stream connection");
                await _parser.StartStreamAsync(cancellationToken);
                _logger.LogInformation("Stream connection established");

                // Keep task alive to maintain stream
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Stream task cancelled for device {SubNodeId}", SubNodeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream task for device {SubNodeId}", SubNodeId);
            }
            finally
            {
                try
                {
                    await _parser.StopStreamAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping stream");
                }
            }
        }, cancellationToken);

        // 2. Create interval loop tasks for each group (using helper from DeviceBase)
        foreach (var (intervalMs, sensors) in sensorGroups)
        {
            // Use RunIntervalLoopAsync with initialDelay=true to allow stream data to arrive
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
    /// Handles telemetry data received from stream.
    /// Pushes data into SensorCache (like writing to Modbus registers).
    /// </summary>
    private void OnTelemetryReceived(List<TelemetryMeasure> measures)
    {
        try
        {
            // Push into sensor cache (like Modbus registers)
            _sensorCache.Push(measures);

            _logger.LogDebug(
                "Pushed {Count} telemetry measures into sensor cache from stream",
                measures.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing telemetry into cache for device {SubNodeId}", SubNodeId);
        }
    }

    /// <summary>
    /// Handles stream state changes.
    /// </summary>
    private void OnStreamStateChanged(StreamState state)
    {
        _logger.LogInformation(
            "Stream state changed to {State} for device {SubNodeId}",
            state, SubNodeId);

        // Could trigger reconnection logic here if needed
        if (state == StreamState.Error || state == StreamState.Disconnected)
        {
            _logger.LogWarning(
                "Stream disconnected for device {SubNodeId}, may need reconnection",
                SubNodeId);
        }
    }
}

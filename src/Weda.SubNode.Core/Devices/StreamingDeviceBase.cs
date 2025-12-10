using Microsoft.Extensions.Logging;
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
    private Task? _healthTask;

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
        : base(context, configuration, parser.Communication)
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
            .Where(s => s.Config.Enabled)
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
    /// Starts background tasks for streaming, sampling, and health reporting.
    /// Framework implementation with automatic stream handling.
    /// Batch send task is started by DeviceBase.
    ///
    /// Tasks:
    /// 1. Stream task - maintains stream connection, pushes data into SensorCache
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
            "Starting streaming device: Groups={GroupCount}, TotalSensors={SensorCount}",
            sensorsByInterval.Count, Configuration.Sensors.Count(s => s.Config.Enabled));

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
                _logger.LogInformation("Stream task cancelled for device {DeviceId}", DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream task for device {DeviceId}", DeviceId);
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
    /// Samples from SensorCache and enqueues via EnqueueTelemetryAsync.
    /// </summary>
    private async Task RunIntervalGroupSamplingAsync(
        List<Sensor> sensors,
        int intervalMs,
        CancellationToken cancellationToken)
    {
        // Wait for initial interval to allow stream data to arrive
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

            _orchestrator.HealthMonitor.RecordTelemetryReadDuration(TimeSpan.Zero);

            // RaiseDataReceived fires with RAW data before transform/filter
            RaiseDataReceived(measures);

            // Step 2: Transform, Filter, and Enqueue (handled by DeviceBase)
            // RaiseDataProcessed fires in EnqueueTelemetryAsync after transform/filter
            await EnqueueTelemetryAsync(measures, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sampling interval group with {SensorCount} sensors", sensors.Count);
        }
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
            _logger.LogError(ex, "Error pushing telemetry into cache for device {DeviceId}", DeviceId);
        }
    }

    /// <summary>
    /// Handles stream state changes.
    /// </summary>
    private void OnStreamStateChanged(StreamState state)
    {
        _logger.LogInformation(
            "Stream state changed to {State} for device {DeviceId}",
            state, DeviceId);

        // Could trigger reconnection logic here if needed
        if (state == StreamState.Error || state == StreamState.Disconnected)
        {
            _logger.LogWarning(
                "Stream disconnected for device {DeviceId}, may need reconnection",
                DeviceId);
        }
    }
}

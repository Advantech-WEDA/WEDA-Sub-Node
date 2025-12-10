using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Polly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Policies;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Base class for Request-Response communication pattern devices.
/// Framework handles ReadTelemetryAsync and StartBackgroundTasksAsync internally.
/// User only needs to provide an IRequestResponseProtocolParser implementation.
///
/// Use this class for protocols like: Modbus TCP/RTU, OPC-UA, REST API, BACnet
///
/// Architecture:
/// 1. Sensors are grouped by their Config.Interval
/// 2. Each interval group shares a single Task.Delay loop for polling
/// 3. When the delay expires, all sensors in that group are read and enqueued via EnqueueTelemetryAsync
/// 4. Batch send task (in DeviceBase) sends collected data at CalculatedSendTelemetryPeriod
///
/// Telemetry flow: Collect (grouped) → EnqueueTelemetryAsync (Transform + Filter + Enqueue) → Batch Send
///
/// Inheritance hierarchy example:
/// MyFirstDevice -> TcpModbusDevice -> ModbusDevice -> RequestResponseDeviceBase -> DeviceBase
/// </summary>
public class RequestResponseDeviceBase : DeviceBase
{
    /// <summary>
    /// The protocol parser for this device. Protected to allow subclass access.
    /// </summary>
    protected readonly IRequestResponseProtocolParser _parser;
    private readonly ResiliencePipeline<bool> _reconnectionPipeline;
    private readonly List<Task> _pollingTasks = new();
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of RequestResponseDeviceBase.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration.</param>
    /// <param name="parser">Request-Response protocol parser (Parser owns Communication).</param>
    public RequestResponseDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IRequestResponseProtocolParser parser)
        : base(context, configuration, parser.Communication)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        // Create Polly reconnection pipeline using ConnectionOptions from context
        var policyOptions = ConnectionPolicyOptions.FromConnectionOptions(context.ConnectionOptions);
        _reconnectionPipeline = ConnectionPolicies.CreateReconnectionPipeline(_logger, policyOptions);

        _logger.LogDebug(
            "RequestResponseDeviceBase initialized with {ParserType}",
            _parser.GetType().Name);
    }

    /// <summary>
    /// Reads telemetry from the device for all enabled sensors using the parser.
    /// Framework implementation - delegates to parser.ReadTelemetryAsync().
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    public sealed override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = await _parser.ReadTelemetryAsync(cancellationToken);
        // RaiseDataReceived fires with RAW data before transform/filter
        // RaiseDataProcessed fires in EnqueueTelemetryAsync after transform/filter
        RaiseDataReceived(measures);
        return measures;
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
    /// Starts background tasks for telemetry polling and health reporting.
    /// Framework implementation with automatic reconnection handling.
    /// Batch send task is started by DeviceBase.
    ///
    /// Tasks:
    /// 1. Interval-grouped polling tasks - read sensors and enqueue via EnqueueTelemetryAsync
    /// 2. Health task - periodic health reporting
    /// </summary>
    internal sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        // Group sensors by their interval
        var sensorsByInterval = Configuration.Sensors
            .Where(s => s.Config.Enabled)
            .GroupBy(s => (int)s.Config.Interval)
            .ToList();

        _logger.LogDebug(
            "Starting request-response device: Groups={GroupCount}, TotalSensors={SensorCount}",
            sensorsByInterval.Count, Configuration.Sensors.Count(s => s.Config.Enabled));

        // Create a Task.Delay loop for each interval group
        foreach (var group in sensorsByInterval)
        {
            var interval = group.Key;
            var sensors = group.ToList();

            // Start polling task for this interval group
            var pollingTask = RunIntervalGroupPollingAsync(sensors, interval, cancellationToken);
            _pollingTasks.Add(pollingTask);

            _logger.LogDebug(
                "Created polling task for interval {Interval}ms with {SensorCount} sensors: [{SensorNames}]",
                interval, sensors.Count, string.Join(", ", sensors.Select(s => s.Name)));
        }

        // Health reporting task using Task.Delay loop
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
    /// Runs the polling loop for an interval group using Task.Delay loop.
    /// Task.Delay loop ensures no overlap - waits for previous iteration to complete before starting next.
    /// </summary>
    private async Task RunIntervalGroupPollingAsync(
        List<Sensor> sensors,
        int intervalMs,
        CancellationToken cancellationToken)
    {
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
            _logger.LogDebug("Polling task cancelled for interval group with {SensorCount} sensors", sensors.Count);
        }
    }

    /// <summary>
    /// Processes an interval group: reads all sensors and enqueues via EnqueueTelemetryAsync.
    /// </summary>
    private async Task ProcessIntervalGroupAsync(List<Sensor> sensors, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            // Check connection state, attempt reconnection if needed
            if (ConnectionState == CommunicationState.Error ||
                ConnectionState == CommunicationState.Disconnected)
            {
                if (_communication is CommunicationBase commBase)
                {
                    var reconnected = await _reconnectionPipeline.ExecuteAsync(
                        async ct => await commBase.ReconnectAsync(ct),
                        cancellationToken);

                    if (!reconnected)
                    {
                        _logger.LogWarning("Skipping interval group read - reconnection failed");
                        return;
                    }
                }
            }

            var stopwatch = Stopwatch.StartNew();

            // Step 1: Collect - Read all sensors in the group
            var sensorResourceIds = sensors.Select(s => s.ResourceId).ToList();
            var measures = await _parser.ReadTelemetryAsync(sensorResourceIds, cancellationToken);

            stopwatch.Stop();
            _orchestrator.HealthMonitor.RecordTelemetryReadDuration(stopwatch.Elapsed);

            if (measures.Count == 0)
            {
                return;
            }

            // RaiseDataReceived fires with RAW data before transform/filter
            RaiseDataReceived(measures);

            // Step 2: Transform, Filter, and Enqueue (handled by DeviceBase)
            // RaiseDataProcessed fires in EnqueueTelemetryAsync after transform/filter
            await EnqueueTelemetryAsync(measures, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading interval group with {SensorCount} sensors", sensors.Count);
        }
    }
}

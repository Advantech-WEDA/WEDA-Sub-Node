using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Polly;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication.Common;
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
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);

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
        : base(context, configuration, parser)
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
    /// Starts background tasks for telemetry polling and health reporting.
    /// Framework implementation with automatic reconnection handling.
    /// Batch send task is started by DeviceBase.
    ///
    /// Tasks:
    /// 1. Interval-grouped polling tasks - read sensors and enqueue via EnqueueTelemetryAsync
    /// 2. Health task - periodic health reporting
    /// </summary>
    internal sealed override async Task StopDeviceTasksAsync()
    {
        if (_pollingTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(_pollingTasks).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timed out waiting for polling tasks to stop for device {SubNodeId}", SubNodeId);
            }
            catch (OperationCanceledException)
            {
                // Expected when tasks are cancelled
            }

            _pollingTasks.Clear();
        }
    }

    internal sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        // Clear previous tasks reference (for restart scenarios)
        _pollingTasks.Clear();

        // Group sensors by their interval (using helper from DeviceBase)
        var sensorGroups = GroupSensorsByInterval();

        // Skip polling if no sensors are effectively enabled
        if (sensorGroups.Count == 0)
        {
            _logger.LogInformation(
                "No effectively enabled sensors, skipping polling for device {SubNodeId}",
                SubNodeId);
            return Task.CompletedTask;
        }

        // Create interval loop tasks for each group (using helper from DeviceBase)
        foreach (var (intervalMs, sensors) in sensorGroups)
        {
            // Use RunIntervalLoopAsync with initialDelay=false (immediate first read for polling)
            var pollingTask = RunIntervalLoopAsync(sensors, intervalMs, initialDelay: false, cancellationToken);
            _pollingTasks.Add(pollingTask);
        }

        // Health task is now started by DeviceBase.StartAllBackgroundTasks()
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads sensors from the physical device for interval group processing.
    /// Implements the abstract method from DeviceBase (Template Method pattern).
    /// Includes reconnection handling and timing measurement.
    /// </summary>
    protected override async Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
        List<string> sensorResourceIds,
        CancellationToken cancellationToken)
    {
        // Treat anything other than Connected (Error / Disconnected / Connecting) as
        // "not ready yet". Connecting in particular signals that another polling task
        // is mid-reconnect; we must wait instead of racing into a half-open transport.
        if (ConnectionState != CommunicationState.Connected)
        {
            // Serialize reconnection attempts across interval groups. Without this lock
            // multiple polling tasks can each enter ReconnectAsync at the same time,
            // creating a window where one task sees state == Connecting (from another
            // task's in-progress connect) and proceeds to read before _stream is set,
            // producing spurious "Not connected" errors.
            await _reconnectLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring the lock: while we were waiting another
                // task may have already reconnected successfully.
                if (ConnectionState != CommunicationState.Connected
                    && _communication is CommunicationBase commBase)
                {
                    var reconnected = await _reconnectionPipeline.ExecuteAsync(
                        async ct => await commBase.ReconnectAsync(ct),
                        cancellationToken);

                    if (!reconnected)
                    {
                        _logger.LogWarning("Skipping interval group read - reconnection failed");
                        return new IntervalGroupReadResult([], TimeSpan.Zero);
                    }
                }
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        var stopwatch = Stopwatch.StartNew();

        // Read sensors from parser
        var measures = await _parser.ReadTelemetryAsync(sensorResourceIds, cancellationToken);

        stopwatch.Stop();

        return new IntervalGroupReadResult(measures, stopwatch.Elapsed);
    }
}

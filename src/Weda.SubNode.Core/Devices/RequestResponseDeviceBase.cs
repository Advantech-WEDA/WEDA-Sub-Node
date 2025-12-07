using System.Collections.Concurrent;
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
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;
    private Task? _batchSendTask;

    /// <summary>
    /// Buffer for batch mode telemetry collection.
    /// Thread-safe collection to allow concurrent reads and batch sends.
    /// </summary>
    private readonly ConcurrentQueue<TelemetryMeasure> _telemetryBatch = new();

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
    /// Reads telemetry from the device using the parser.
    /// Framework implementation - delegates to parser.ReadTelemetryAsync().
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    public sealed override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = await _parser.ReadTelemetryAsync(cancellationToken);

        if (measures.Count > 0)
        {
            RaiseDataReceived(measures);
        }

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
    /// Sealed to prevent subclasses from overriding framework logic.
    ///
    /// Supports two telemetry upload modes based on Configuration.Periods.SendTelemetry:
    /// - Realtime mode (SendTelemetry = 0): Send immediately after each read
    /// - Batch mode (SendTelemetry > 0): Collect data and send at specified interval
    /// </summary>
    protected sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        _backgroundTasksCts = new CancellationTokenSource();
        var cts = _backgroundTasksCts.Token;

        var readPeriod = Configuration.Periods.ReadTelemetry;
        var sendPeriod = Configuration.Periods.SendTelemetry;
        var isRealtimeMode = sendPeriod <= 0;

        _logger.LogDebug(
            "Starting telemetry task: ReadPeriod={ReadPeriod}ms, SendPeriod={SendPeriod}ms, Mode={Mode}",
            readPeriod, sendPeriod, isRealtimeMode ? "Realtime" : "Batch");

        // Telemetry read task
        _telemetryTask = Task.Run(async () =>
        {
            _logger.LogDebug("Starting telemetry polling task with period {Period}ms", readPeriod);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    // Check if communication is in error state, attempt reconnection using Polly
                    if (ConnectionState == CommunicationState.Error ||
                        ConnectionState == CommunicationState.Disconnected)
                    {
                        _logger.LogWarning(
                            "Device {DeviceId} communication in {State} state, attempting reconnection...",
                            DeviceId, ConnectionState);

                        if (_communication is CommunicationBase commBase)
                        {
                            var reconnected = await _reconnectionPipeline.ExecuteAsync(
                                async ct => await commBase.ReconnectAsync(ct),
                                cts);

                            if (reconnected)
                            {
                                _logger.LogInformation("Device {DeviceId} reconnected successfully", DeviceId);
                            }
                            else
                            {
                                _logger.LogError("Failed to reconnect device {DeviceId}", DeviceId);
                                await Task.Delay(readPeriod, cts);
                                continue;
                            }
                        }
                    }

                    // Measure telemetry read duration
                    var readStopwatch = Stopwatch.StartNew();
                    var measures = await ReadTelemetryAsync(cts);
                    readStopwatch.Stop();

                    // Record telemetry read duration in health monitor
                    _orchestrator.HealthMonitor.RecordTelemetryReadDuration(readStopwatch.Elapsed);

                    if (measures.Count > 0)
                    {
                        if (isRealtimeMode)
                        {
                            // Realtime mode: send immediately
                            await SendTelemetryAsync(ToAsyncEnumerable(measures), cts);
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
                    _logger.LogInformation("Telemetry task cancelled for device {DeviceId}", DeviceId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in telemetry task for device {DeviceId}", DeviceId);
                }

                await Task.Delay(readPeriod, cts);
            }
        }, cts);

        // Batch send task (only started in batch mode)
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

                        // Collect all queued telemetry
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

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }
}

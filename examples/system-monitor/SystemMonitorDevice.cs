using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace SystemMonitorExample;

/// <summary>
/// System resource monitoring device that collects CPU, memory, disk, and network metrics.
/// </summary>
public class SystemMonitorDevice : DeviceBase
{
    private readonly SystemResourceCollector _collector;
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of SystemMonitorDevice with ApplicationContext.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="communication">Communication instance (NullCommunication for virtual devices).</param>
    public SystemMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication)
        : base(context, configuration, communication)
    {
        _collector = new SystemResourceCollector(context.GetLogger<SystemResourceCollector>());
    }

    /// <summary>
    /// Reads telemetry from system resources (CPU, memory, disk, network).
    /// </summary>
    public override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = new List<TelemetryMeasure>();

        _logger.LogInformation("Starting telemetry read for system monitor device {DeviceName}", Configuration.DeviceName);
        _logger.LogInformation("Total configured sensors: {TotalSensorCount}", Configuration.Sensors.Count);

        // Filter enabled sensors
        var enabledSensors = Configuration.Sensors
            .Where(s => s.Config.Enabled)
            .ToList();

        _logger.LogInformation("Reading telemetry for {EnabledSensorCount} enabled sensors", enabledSensors.Count);

        if (enabledSensors.Count == 0)
        {
            _logger.LogTrace("No enabled sensors to read");
            _logger.LogWarning("No enabled sensors to read");
            return measures;
        }

        try
        {
            measures.AddRange(await _collector.CollectCpuMetricsAsync(enabledSensors, cancellationToken));
            measures.AddRange(await _collector.CollectMemoryMetricsAsync(enabledSensors, cancellationToken));
            measures.AddRange(await _collector.CollectDiskMetricsAsync(enabledSensors, cancellationToken));
            measures.AddRange(await _collector.CollectNetworkMetricsAsync(enabledSensors, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting system metrics for device {DeviceId}", DeviceId);
        }

        if (measures.Count > 0)
        {
            RaiseDataReceived(measures);
        }

        return measures;
    }

    /// <summary>
    /// System monitor does not support commands.
    /// </summary>
    public override Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on system monitor device {DeviceId}", command.DeviceCmd, DeviceId);
        return Task.FromResult(false);
    }

    /// <summary>
    /// Starts background tasks for telemetry collection and health reporting.
    /// </summary>
    protected override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        _backgroundTasksCts = new CancellationTokenSource();
        var cts = _backgroundTasksCts.Token;

        _telemetryTask = Task.Run(async () =>
        {
            var period = Configuration.Periods.ReadTelemetry;
            _logger.LogDebug("Starting telemetry task with period {Period}ms", period);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    // Measure telemetry read duration
                    var readStopwatch = Stopwatch.StartNew();
                    var measures = await ReadTelemetryAsync(cts);
                    readStopwatch.Stop();

                    // Record telemetry read duration in health monitor
                    _orchestrator.HealthMonitor.RecordTelemetryReadDuration(readStopwatch.Elapsed);

                    if (measures.Count > 0)
                    {
                        await SendTelemetryAsync(ToAsyncEnumerable(measures), cts);
                    }
                    else
                    {
                        _logger.LogInformation("No telemetry measures collected for device {DeviceId}", DeviceId);
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

                await Task.Delay(period, cts);
            }
        }, cts);

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

/// <summary>
/// Null communication implementation for virtual devices that don't communicate with physical hardware.
/// </summary>
internal class NullCommunication : ICommunication
{
    public ConnectionSettings Settings { get; } = new ConnectionSettings();

    public CommunicationState State => CommunicationState.Connected;

    public bool IsConnected => true;

    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    public Task<bool> ConnectAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public void Dispose()
    {
        // Nothing to dispose for null communication
    }
}

using System.Diagnostics;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace SystemMonitorExample;

/// <summary>
/// System resource monitoring device that collects CPU, memory, disk, and network metrics.
/// Architecture: Device -> Parser -> Communication (LocalSystemCommunication)
/// </summary>
public class SystemMonitorDevice : DeviceBase
{
    private readonly IRequestResponseProtocolParser _parser;
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of SystemMonitorDevice with ApplicationContext.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="parser">Protocol parser for system metrics.</param>
    public SystemMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IRequestResponseProtocolParser parser)
        : base(context, configuration, parser.Communication)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        _logger.LogDebug(
            "SystemMonitorDevice initialized with {ParserType} ({SensorCount} sensors)",
            _parser.GetType().Name,
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Reads telemetry from system resources (CPU, memory, disk, network).
    /// </summary>
    public override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // Build sensor mapping from configuration (business logic)
        var sensorMapping = BuildSensorMapping();

        // Delegate to Parser for protocol-level operations
        var measures = await _parser.ReadSensorDataAsync(sensorMapping, cancellationToken);

        if (measures.Count > 0)
        {
            RaiseDataReceived(measures);
        }
        else
        {
            _logger.LogTrace("No telemetry measures collected for device {DeviceId}", DeviceId);
        }

        return measures;
    }

    /// <summary>
    /// Build sensor mapping from device configuration (business logic).
    /// Maps protocol field names to ResourceIds for enabled sensors.
    /// </summary>
    private SensorMapping BuildSensorMapping()
    {
        var mapping = new SensorMapping();

        foreach (var sensor in Configuration.Sensors.Where(s => s.Config.Enabled))
        {
            mapping.FieldToResourceId[sensor.Name] = sensor.ResourceId;

            // Map sensor type based on sensor group
            var sensorType = MapSensorGroupToType(sensor.SensorGroup);
            mapping.FieldToSensorType[sensor.Name] = sensorType;
        }

        return mapping;
    }

    /// <summary>
    /// Map SensorGroup to SensorType enum
    /// </summary>
    private static SensorType MapSensorGroupToType(SensorGroup sensorGroup)
    {
        return sensorGroup switch
        {
            SensorGroup.AI => SensorType.Analog,
            SensorGroup.AO => SensorType.Analog,
            SensorGroup.DI => SensorType.Digital,
            SensorGroup.DO => SensorType.Digital,
            SensorGroup.TEMP => SensorType.Temperature,
            _ => SensorType.Other
        };
    }

    /// <summary>
    /// System monitor does not support commands.
    /// </summary>
    public override async Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on system monitor device {DeviceId}", command.DeviceCmd, DeviceId);

        var result = await _parser.ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("Command execution failed: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        return true;
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
                        _logger.LogDebug("No telemetry measures collected for device {DeviceId}", DeviceId);
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

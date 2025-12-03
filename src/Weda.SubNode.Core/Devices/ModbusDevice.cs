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
using Weda.SubNode.Core.Protocols.Modbus;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Modbus device implementation with real Modbus protocol support.
/// Now uses ModbusRequestResponseParser internally for proper layering.
/// Architecture: Device -> Parser -> Communication
/// </summary>
public class ModbusDevice : DeviceBase
{
    private readonly IRequestResponseProtocolParser _parser;
    private readonly ResiliencePipeline<bool> _reconnectionPipeline;
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of ModbusDevice with ApplicationContext.
    /// Now uses ModbusRequestResponseParser internally for proper layering.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing Modbus settings.</param>
    /// <param name="communication">TCP communication instance for Modbus protocol.</param>
    /// <param name="useBatchOptimization">Enable batch reading optimization (default: true)</param>
    /// <param name="batchOptions">Batch optimization options (optional)</param>
    public ModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IRequestResponseCommunication<byte[], byte[]> communication,
        bool useBatchOptimization = true,
        ModbusBatchOptimizationOptions? batchOptions = null)
        : base(context, configuration, communication)
    {
        if (communication == null)
            throw new ArgumentNullException(nameof(communication));

        // Extract Modbus protocol settings (SlaveId)
        var slaveId = configuration.GetModbusSlaveId();

        // Convert sensors to Modbus registers and create metadata dictionary
        var sensorMetadata = configuration.Sensors
            .Select(s => s.ToModbusRegister())
            .ToDictionary(r => r.Name, r => r);

        // Create ModbusRequestResponseParser (Parser owns Communication)
        _parser = new ModbusRequestResponseParser(
            communication,
            slaveId,
            sensorMetadata,
            _logger,
            useBatchOptimization,
            batchOptions);

        // Create Polly reconnection pipeline using ConnectionOptions from context
        var policyOptions = ConnectionPolicyOptions.FromConnectionOptions(context.ConnectionOptions);
        _reconnectionPipeline = ConnectionPolicies.CreateReconnectionPipeline(_logger, policyOptions);

        _logger.LogDebug(
            "ModbusDevice initialized with {ParserType} ({SensorCount} sensors)",
            _parser.GetType().Name,
            sensorMetadata.Count);
    }

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

    public override async Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on Modbus device {DeviceId}", command.DeviceCmd, DeviceId);

        // Delegate to Parser for command execution
        var result = await _parser.ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogError("Command execution failed: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Scan Modbus registers to discover sensors before configuration
    /// </summary>
    public async Task<List<ModbusScanResult>> ScanRegistersAsync(
        ModbusScanConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Modbus register scan for device {DeviceId}", DeviceId);

        // Get Communication from Parser
        var communication = _parser.Communication as IRequestResponseCommunication<byte[], byte[]>
            ?? throw new InvalidOperationException("Parser Communication is not IRequestResponseCommunication<byte[], byte[]>");

        var slaveId = Configuration.GetModbusSlaveId();
        var scanner = new ModbusScanner(communication, slaveId, _logger);
        var results = await scanner.ScanHoldingRegistersAsync(config, cancellationToken);

        // Print results to console
        scanner.PrintScanResults(results, _logger);

        return results;
    }

    /// <summary>
    /// Generate sensor configuration suggestions based on register scan
    /// </summary>
    public List<SensorSuggestion> GenerateSensorSuggestions(List<ModbusScanResult> scanResults)
    {
        var communication = _parser.Communication as IRequestResponseCommunication<byte[], byte[]>
            ?? throw new InvalidOperationException("Parser Communication is not IRequestResponseCommunication<byte[], byte[]>");

        var slaveId = Configuration.GetModbusSlaveId();
        var scanner = new ModbusScanner(communication, slaveId, _logger);
        return scanner.GenerateSensorSuggestions(scanResults);
    }

    /// <summary>
    /// Generate a Markdown report from scan results
    /// </summary>
    /// <param name="scanResults">Scan results to report</param>
    /// <param name="config">Scan configuration used</param>
    /// <returns>Markdown formatted report</returns>
    public string GenerateScanReport(List<ModbusScanResult> scanResults, ModbusScanConfig config)
    {
        var communication = _parser.Communication as IRequestResponseCommunication<byte[], byte[]>
            ?? throw new InvalidOperationException("Parser Communication is not IRequestResponseCommunication<byte[], byte[]>");

        var slaveId = Configuration.GetModbusSlaveId();
        var scanner = new ModbusScanner(communication, slaveId, _logger);

        // Build device info from configuration
        var deviceInfo = new Dictionary<string, object>
        {
            ["Host"] = Configuration.Communication?.GetValueOrDefault("Host") ?? "Unknown",
            ["Port"] = Configuration.Communication?.GetValueOrDefault("Port") ?? 502
        };

        return scanner.GenerateMarkdownReport(scanResults, config, deviceInfo);
    }

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
                    // Check if communication is in error state, attempt reconnection using Polly
                    if (ConnectionState == CommunicationState.Error ||
                        ConnectionState == CommunicationState.Disconnected)
                    {
                        _logger.LogWarning("Device {DeviceId} communication in {State} state, attempting reconnection with Polly pipeline...",
                            DeviceId, ConnectionState);

                        if (_communication is CommunicationBase commBase)
                        {
                            // Use Polly reconnection pipeline (unlimited retries with exponential backoff)
                            var reconnected = await _reconnectionPipeline.ExecuteAsync(
                                async ct => await commBase.ReconnectAsync(ct),
                                cts);

                            if (reconnected)
                            {
                                _logger.LogInformation("Device {DeviceId} reconnected successfully via Polly pipeline", DeviceId);
                            }
                            else
                            {
                                // This should rarely happen since pipeline has unlimited retries
                                _logger.LogError("Failed to reconnect device {DeviceId} even after Polly retries", DeviceId);
                                await Task.Delay(period, cts);
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
                        await SendTelemetryAsync(ToAsyncEnumerable(measures), cts);
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

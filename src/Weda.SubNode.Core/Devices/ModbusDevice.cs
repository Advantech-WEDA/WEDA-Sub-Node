using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Polly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Policies;
using Weda.SubNode.Core.Protocols.Modbus;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Modbus device implementation with real Modbus protocol support
/// </summary>
public class ModbusDevice : DeviceBase
{
    private readonly byte _slaveId;
    private readonly List<ModbusSensorRegister> _sensorRegisters;
    private readonly IRequestResponseCommunication<byte[], byte[]> _tcpCommunication;
    private readonly ResiliencePipeline<bool> _reconnectionPipeline;
    private readonly ModbusBatchReader _batchReader;
    private readonly bool _useBatchOptimization;
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;
    private ushort _transactionId = 0;

    /// <summary>
    /// Initializes a new instance of ModbusDevice with ApplicationContext.
    /// ModbusDevice directly manages Modbus TCP communication using request-response pattern.
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
        _tcpCommunication = communication ?? throw new ArgumentNullException(nameof(communication));
        _useBatchOptimization = useBatchOptimization;

        // Extract Modbus protocol settings (SlaveId)
        _slaveId = configuration.GetModbusSlaveId();

        // Convert sensors to Modbus registers
        _sensorRegisters = configuration.Sensors
            .Select(s => s.ToModbusRegister())
            .ToList();

        // Create batch reader for optimized multi-sensor reading
        _batchReader = new ModbusBatchReader(
            _tcpCommunication,
            _slaveId,
            _logger,
            batchOptions);

        // Create Polly reconnection pipeline for background task resilience
        _reconnectionPipeline = ConnectionPolicies.CreateReconnectionPipeline(_logger);

        if (_useBatchOptimization)
        {
            _logger.LogInformation(
                "Modbus batch optimization ENABLED for device {DeviceId} ({SensorCount} sensors)",
                DeviceId,
                _sensorRegisters.Count);
        }
        else
        {
            _logger.LogInformation(
                "Modbus batch optimization DISABLED for device {DeviceId} (using legacy single-point reading)",
                DeviceId);
        }
    }

    public override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = new List<TelemetryMeasure>();

        // Filter enabled sensors
        var enabledSensors = _sensorRegisters
            .Where(r => Configuration.Sensors.First(s => s.Name == r.Name).Config.Enabled)
            .ToList();

        if (enabledSensors.Count == 0)
        {
            _logger.LogTrace("No enabled sensors to read");
            return measures;
        }

        if (_useBatchOptimization)
        {
            // Use batch reader for optimized multi-sensor reading
            var results = await _batchReader.ReadSensorsAsync(enabledSensors, cancellationToken);

            foreach (var (sensorName, result) in results)
            {
                if (result.Success && result.Value != null)
                {
                    var sensor = Configuration.Sensors.First(s => s.Name == sensorName);
                    var measure = new TelemetryMeasure
                    {
                        ResourceId = sensor.ResourceId,
                        Value = result.Value,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    measures.Add(measure);

                    _logger.LogDebug(
                        "[DATA] Telemetry: {SensorName} = {Value} (ResourceId: {ResourceId}) | Raw={Raw}",
                        sensorName,
                        result.Value,
                        sensor.ResourceId,
                        result.RawRegisters != null ? string.Join(",", result.RawRegisters) : "N/A");
                }
                else
                {
                    _logger.LogError(
                        "Failed to read sensor {SensorName}: {Error}",
                        sensorName,
                        result.ErrorMessage ?? "Unknown error");
                }
            }
        }
        else
        {
            // Legacy single-point reading mode
            foreach (var register in enabledSensors)
            {
                try
                {
                    var sensor = Configuration.Sensors.First(s => s.Name == register.Name);

                    _logger.LogDebug(
                        "Reading Modbus register: Address={Address}, Count={Count}, Type={Type}",
                        register.RegisterAddress,
                        register.RegisterCount,
                        register.RegisterType);

                    // Step 1: Read raw data from hardware via communication
                    var rawData = await ReadModbusRegistersAsync(
                        register.RegisterType,
                        register.RegisterAddress,
                        register.RegisterCount,
                        cancellationToken);

                    // Step 2: Protocol parser - convert registers to C# type
                    var parser = new ModbusProtocolParser(register.DataType);
                    var parsedValue = parser.Parse(rawData);

                    // Step 3: Create telemetry measure with raw parsed value
                    // Transforms and DSP filters will be applied by TelemetryPipeline
                    var measure = new TelemetryMeasure
                    {
                        ResourceId = sensor.ResourceId,
                        Value = parsedValue,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    measures.Add(measure);

                    // Detailed debug log
                    _logger.LogDebug(
                        "[DATA] Telemetry: {SensorName} = {Value} (ResourceId: {ResourceId}) | Raw={Raw}",
                        register.Name,
                        parsedValue,
                        register.ResourceId,
                        string.Join(",", rawData));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error reading sensor {SensorName} at address {Address} from device {DeviceId}",
                        register.Name,
                        register.RegisterAddress,
                        DeviceId);
                    // Continue reading other sensors even if one fails
                }
            }
        }

        if (measures.Count > 0)
        {
            RaiseDataReceived(measures);
        }

        return measures;
    }

    private async Task<ushort[]> ReadModbusRegistersAsync(
        ModbusRegisterType registerType,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken)
    {
        var request = BuildModbusRequest(0x03, startAddress, count);
        var response = await _tcpCommunication.RequestAsync(request, cancellationToken);
        return ParseModbusResponse(response, count);
    }

    private byte[] BuildModbusRequest(byte functionCode, ushort startAddress, ushort count)
    {
        var transactionId = ++_transactionId;

        return new byte[]
        {
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),
            0x00, 0x00,
            0x00, 0x06,
            _slaveId,
            functionCode,
            (byte)(startAddress >> 8), (byte)(startAddress & 0xFF),
            (byte)(count >> 8), (byte)(count & 0xFF)
        };
    }

    private ushort[] ParseModbusResponse(byte[] response, ushort expectedCount)
    {
        if (response.Length < 9)
            throw new InvalidOperationException($"Invalid Modbus response length: {response.Length}");

        var byteCount = response[8];
        var expectedByteCount = expectedCount * 2;

        if (byteCount != expectedByteCount)
            throw new InvalidOperationException($"Unexpected byte count: {byteCount}, expected: {expectedByteCount}");

        var registers = new ushort[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            var offset = 9 + (i * 2);
            registers[i] = (ushort)((response[offset] << 8) | response[offset + 1]);
        }

        return registers;
    }

    public override Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on Modbus device {DeviceId}", command.DeviceCmd, DeviceId);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Scan Modbus registers to discover sensors before configuration
    /// </summary>
    public async Task<List<ModbusScanResult>> ScanRegistersAsync(
        ModbusScanConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Modbus register scan for device {DeviceId}", DeviceId);

        var scanner = new ModbusScanner(_tcpCommunication, _slaveId, _logger);
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
        var scanner = new ModbusScanner(_tcpCommunication, _slaveId, _logger);
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
        var scanner = new ModbusScanner(_tcpCommunication, _slaveId, _logger);

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
            _logger.LogInformation("Starting telemetry task with period {Period}ms", period);

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
            _logger.LogInformation("Starting health reporting task with period {Period}ms", period);

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

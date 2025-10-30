using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Modbus;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Modbus device implementation with real Modbus protocol support
/// </summary>
public class ModbusDevice : DeviceBase
{
    private readonly byte _slaveId;
    private readonly List<ModbusSensorRegister> _sensorRegisters;
    private readonly ModbusProtocolParserFactory _parserFactory;
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _telemetryTask;
    private Task? _healthTask;
    private ushort _transactionId = 0;

    /// <summary>
    /// Initializes a new instance of ModbusDevice with ApplicationContext.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing Modbus settings.</param>
    /// <param name="communication">Communication instance for Modbus protocol.</param>
    public ModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication)
        : base(context, configuration, communication)
    {
        // Extract Modbus protocol settings (SlaveId)
        _slaveId = configuration.GetModbusSlaveId();

        // Convert sensors to Modbus registers
        _sensorRegisters = configuration.Sensors
            .Select(s => s.ToModbusRegister())
            .ToList();

        // Initialize parser factory
        _parserFactory = new ModbusProtocolParserFactory();
    }

    public override async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = new List<TelemetryMeasure>();

        foreach (var register in _sensorRegisters)
        {
            try
            {
                // Get sensor configuration
                var sensor = Configuration.Sensors.First(s => s.Name == register.Name);

                // Skip if sensor is disabled
                if (!sensor.Config.Enabled)
                {
                    _logger.LogTrace("Sensor {SensorName} is disabled, skipping", sensor.Name);
                    continue;
                }

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
                var parser = _parserFactory.CreateParser(register);
                var parsedValue = parser.Parse(rawData);

                // Step 3: Apply DSP filter pipeline (transforms and calibration are applied later in TelemetryPipeline)
                var finalValue = await ApplyDspPipelineAsync(sensor, Convert.ToDouble(parsedValue), cancellationToken);

                // Step 4: Create telemetry measure (transforms will be applied by TelemetryPipeline)
                var measure = new TelemetryMeasure
                {
                    ResourceId = sensor.ResourceId,
                    Value = finalValue,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                measures.Add(measure);

                // Detailed debug log with all stages of data processing
                _logger.LogDebug(
                    "[DATA] Telemetry: {SensorName} = {Value} (ResourceId: {ResourceId}) | Raw={Raw}, Parsed={Parsed}, AfterDsp={AfterDsp}",
                    register.Name,
                    finalValue,
                    register.ResourceId,
                    string.Join(",", rawData),
                    parsedValue,
                    finalValue);
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
        await _communication.WriteAsync(request, cancellationToken);

        var response = await _communication.ReadAsync(cancellationToken);
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

    /// <summary>
    /// Apply sensor-specific DSP filter pipeline
    /// Filters are applied in order based on their Order property
    /// </summary>
    private async Task<double> ApplyDspPipelineAsync(Sensor sensor, double value, CancellationToken cancellationToken)
    {
        // Get enabled filters sorted by order
        var enabledFilters = sensor.Config.DspPipeline
            .Where(f => f.Enabled)
            .OrderBy(f => f.Order)
            .ToList();

        if (enabledFilters.Count == 0)
            return value; // No filters, return calibrated value as-is

        // TODO: Implement actual DSP filter application
        // For now, just return the calibrated value
        // In future, instantiate filters based on Type and Parameters
        _logger.LogDebug("Sensor {SensorName}: {FilterCount} DSP filters configured", sensor.Name, enabledFilters.Count);

        return await Task.FromResult(value);
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

        var scanner = new ModbusScanner(_communication, _slaveId, _logger);
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
        var scanner = new ModbusScanner(_communication, _slaveId, _logger);
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
        var scanner = new ModbusScanner(_communication, _slaveId, _logger);

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
                    // Check if communication is in error state, attempt reconnection
                    if (ConnectionState == CommunicationState.Error ||
                        ConnectionState == CommunicationState.Disconnected)
                    {
                        _logger.LogWarning("Device {DeviceId} communication in {State} state, attempting reconnection...",
                            DeviceId, ConnectionState);

                        if (_communication is CommunicationBase commBase)
                        {
                            var reconnected = await commBase.ReconnectAsync(cts);
                            if (!reconnected)
                            {
                                _logger.LogError("Failed to reconnect device {DeviceId}", DeviceId);
                                await Task.Delay(period, cts);
                                continue;
                            }
                            _logger.LogInformation("Device {DeviceId} reconnected successfully", DeviceId);
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

                        // Reset reconnection counter on successful operation
                        if (_communication is CommunicationBase commBase)
                        {
                            commBase.ResetReconnectAttempts();
                        }
                    }
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

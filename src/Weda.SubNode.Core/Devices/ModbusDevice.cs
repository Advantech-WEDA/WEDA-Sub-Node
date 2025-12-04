using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Protocols.Modbus;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Modbus device implementation with real Modbus protocol support.
/// Inherits from RequestResponseDeviceBase for Request/Response communication pattern.
/// Adds Modbus-specific functionality like register scanning and sensor discovery.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: MyFirstDevice -> TcpModbusDevice -> ModbusDevice -> RequestResponseDeviceBase -> DeviceBase
/// </summary>
public class ModbusDevice : RequestResponseDeviceBase
{
    /// <summary>
    /// Initializes a new instance of ModbusDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing Modbus settings.</param>
    /// <param name="communication">Communication instance for Modbus protocol.</param>
    /// <param name="useBatchOptimization">Enable batch reading optimization (default: true)</param>
    /// <param name="batchOptions">Batch optimization options (optional)</param>
    public ModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IRequestResponseCommunication<byte[], byte[]> communication,
        bool useBatchOptimization = true,
        ModbusBatchOptimizationOptions? batchOptions = null)
        : base(context, configuration, CreateModbusParser(context, configuration, communication, useBatchOptimization, batchOptions))
    {
        _logger.LogDebug(
            "ModbusDevice initialized ({SensorCount} sensors, BatchOptimization={BatchOptimization})",
            configuration.Sensors.Count,
            useBatchOptimization);
    }

    /// <summary>
    /// Creates the ModbusRequestResponseParser for this device.
    /// </summary>
    private static IRequestResponseProtocolParser CreateModbusParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IRequestResponseCommunication<byte[], byte[]> communication,
        bool useBatchOptimization,
        ModbusBatchOptimizationOptions? batchOptions)
    {
        var logger = context.LoggerFactory.CreateLogger<ModbusRequestResponseParser>();
        return new ModbusRequestResponseParser(
            configuration,
            communication,
            logger,
            useBatchOptimization,
            batchOptions);
    }

    #region Modbus-Specific Methods

    /// <summary>
    /// Scan Modbus registers to discover sensors before configuration.
    /// </summary>
    public async Task<List<ModbusScanResult>> ScanRegistersAsync(
        ModbusScanConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Modbus register scan for device {DeviceId}", DeviceId);

        var communication = _parser.Communication as IRequestResponseCommunication<byte[], byte[]>
            ?? throw new InvalidOperationException("Parser Communication is not IRequestResponseCommunication<byte[], byte[]>");

        var slaveId = Configuration.GetModbusSlaveId();
        var scanner = new ModbusScanner(communication, slaveId, _logger);
        var results = await scanner.ScanHoldingRegistersAsync(config, cancellationToken);

        scanner.PrintScanResults(results, _logger);

        return results;
    }

    /// <summary>
    /// Generate sensor configuration suggestions based on register scan.
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
    /// Generate a Markdown report from scan results.
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

        var deviceInfo = new Dictionary<string, object>
        {
            ["Host"] = Configuration.Communication?.GetValueOrDefault("Host") ?? "Unknown",
            ["Port"] = Configuration.Communication?.GetValueOrDefault("Port") ?? 502
        };

        return scanner.GenerateMarkdownReport(scanResults, config, deviceInfo);
    }

    #endregion
}

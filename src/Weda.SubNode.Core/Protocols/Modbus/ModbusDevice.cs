using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Utilities;
using Weda.SubNode.Core.Devices;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus device implementation with real Modbus protocol support.
/// Inherits from RequestResponseDeviceBase for Request/Response communication pattern.
/// Adds Modbus-specific functionality like register scanning, sensor discovery, and output control.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: MyFirstDevice -> TcpModbusDevice -> ModbusDevice -> RequestResponseDeviceBase -> DeviceBase
/// </summary>
public class ModbusDevice : RequestResponseDeviceBase,
    IDigitalOutputControllable, IAnalogOutputControllable,
    IDigitalOutputReadable, IAnalogOutputReadable,
    IDigitalInputReadable, IAnalogInputReadable
{
    /// <summary>
    /// Initializes a new instance of ModbusDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing Modbus settings.</param>
    /// <param name="communication">Communication instance for Modbus protocol.</param>
    /// <param name="useBatchOptimization">Enable batch reading optimization (default: true)</param>
    /// <param name="batchOptions">Batch optimization options (optional)</param>
    /// <param name="slaveId">Modbus slave ID (default: 1)</param>
    /// <param name="byteOrder">Byte order for multi-register data types (default: BigEndian)</param>
    public ModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IRequestResponseCommunication<byte[], byte[]> communication,
        bool useBatchOptimization = true,
        ModbusBatchOptimizationOptions? batchOptions = null,
        byte slaveId = 1,
        ModbusByteOrder byteOrder = ModbusByteOrder.BigEndian)
        : base(context, configuration, CreateModbusParser(context, configuration, communication, useBatchOptimization, batchOptions, slaveId, byteOrder))
    {
        _slaveId = slaveId;
        _logger.LogDebug(
            "ModbusDevice initialized ({SensorCount} sensors, BatchOptimization={BatchOptimization}, SlaveId={SlaveId})",
            configuration.Sensors.Count,
            useBatchOptimization,
            slaveId);
    }

    private readonly byte _slaveId;

    /// <summary>
    /// Gets the Modbus slave ID for this device.
    /// </summary>
    protected byte SlaveId => _slaveId;

    /// <summary>
    /// Creates the ModbusRequestResponseParser for this device.
    /// </summary>
    private static IRequestResponseProtocolParser CreateModbusParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IRequestResponseCommunication<byte[], byte[]> communication,
        bool useBatchOptimization,
        ModbusBatchOptimizationOptions? batchOptions,
        byte slaveId,
        ModbusByteOrder byteOrder)
    {
        var logger = context.LoggerFactory.CreateLogger<ModbusRequestResponseParser>();
        return new ModbusRequestResponseParser(
            configuration,
            communication,
            logger,
            useBatchOptimization,
            batchOptions,
            slaveId: slaveId,
            byteOrder: byteOrder);
    }

    #region Modbus-Specific Methods

    /// <summary>
    /// Scan Modbus registers to discover sensors before configuration.
    /// </summary>
    public async Task<List<ModbusScanResult>> ScanRegistersAsync(
        ModbusScanConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Modbus register scan for device {SubNodeId}", SubNodeId);

        var communication = _parser.Communication as IRequestResponseCommunication<byte[], byte[]>
            ?? throw new InvalidOperationException("Parser Communication is not IRequestResponseCommunication<byte[], byte[]>");

        var scanner = new ModbusScanner(communication, _slaveId, _logger);
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

        var scanner = new ModbusScanner(communication, _slaveId, _logger);
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

        var scanner = new ModbusScanner(communication, _slaveId, _logger);

        var deviceInfo = new Dictionary<string, object>
        {
            ["Host"] = Configuration.DeviceCommunication?.GetValueOrDefault("Host") ?? "Unknown",
            ["Port"] = Configuration.DeviceCommunication?.GetValueOrDefault("Port") ?? 502
        };

        return scanner.GenerateMarkdownReport(scanResults, config, deviceInfo);
    }

    #endregion

    #region IDigitalOutputControllable Implementation

    /// <summary>
    /// Sets the state of a digital output using Modbus FC05 (Write Single Coil).
    /// </summary>
    /// <param name="outputName">The name of the digital output (must match a Coil sensor in configuration)</param>
    /// <param name="state">The desired state: true = ON, false = OFF</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SetDigitalOutputAsync: {OutputName}={State}", outputName, state);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetDO",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = outputName,
                ["state"] = state
            }
        };

        var result = await ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("SetDigitalOutputAsync failed for {OutputName}: {Error}",
                outputName, result.FirstError.Description);
            return false;
        }

        _logger.LogInformation("SetDigitalOutputAsync succeeded: {OutputName}={State}", outputName, state);
        return true;
    }

    /// <summary>
    /// Executes a Modbus command via the underlying parser.
    /// </summary>
    /// <param name="command">The device command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the command execution</returns>
    protected Task<ErrorOr.ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        var modbusParser = (ModbusRequestResponseParser)_parser;
        return modbusParser.ExecuteCommandAsync(command, cancellationToken);
    }

    #endregion

    #region IAnalogOutputControllable Implementation

    /// <summary>
    /// Sets the value of an analog output using Modbus FC06 (Write Single Register).
    /// </summary>
    /// <param name="outputName">The name of the analog output (must match a HoldingRegister sensor in configuration)</param>
    /// <param name="value">The desired value (converted to ushort for single register)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> SetAnalogOutputAsync(string outputName, object value, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SetAnalogOutputAsync: {OutputName}={Value}", outputName, value);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetAO",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = outputName,
                ["value"] = value
            }
        };

        var result = await ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("SetAnalogOutputAsync failed for {OutputName}: {Error}",
                outputName, result.FirstError.Description);
            return false;
        }

        _logger.LogInformation("SetAnalogOutputAsync succeeded: {OutputName}={Value}", outputName, value);
        return true;
    }

    #endregion

    #region IDigitalOutputReadable Implementation

    /// <summary>
    /// Reads the current state of a digital output using Modbus FC01 (Read Coils).
    /// </summary>
    /// <param name="outputName">The name of the digital output (must match a Coil sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current state: true = ON, false = OFF, null if not found</returns>
    public async Task<bool?> GetDigitalOutputAsync(string outputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetDigitalOutputAsync: {OutputName}", outputName);

        var command = new DeviceCommand
        {
            DeviceCmd = "GetDO",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = outputName
            }
        };

        var result = await ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("GetDigitalOutputAsync failed for {OutputName}: {Error}",
                outputName, result.FirstError.Description);
            return null;
        }

        var state = Convert.ToBoolean(result.Value);
        _logger.LogDebug("GetDigitalOutputAsync succeeded: {OutputName}={State}", outputName, state);
        return state;
    }

    #endregion

    #region IAnalogOutputReadable Implementation

    /// <summary>
    /// Reads the current value of an analog output using Modbus FC03 (Read Holding Registers).
    /// </summary>
    /// <param name="outputName">The name of the analog output (must match a HoldingRegister sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current value, or null if not found</returns>
    public async Task<double?> GetAnalogOutputAsync(string outputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetAnalogOutputAsync: {OutputName}", outputName);

        var command = new DeviceCommand
        {
            DeviceCmd = "GetAO",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = outputName
            }
        };

        var result = await ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("GetAnalogOutputAsync failed for {OutputName}: {Error}",
                outputName, result.FirstError.Description);
            return null;
        }

        var value = Convert.ToDouble(result.Value);
        _logger.LogDebug("GetAnalogOutputAsync succeeded: {OutputName}={Value}", outputName, value);
        return value;
    }

    #endregion

    #region IDigitalInputReadable Implementation

    /// <summary>
    /// Reads the current state of a digital input using Modbus FC02 (Read Discrete Inputs).
    /// </summary>
    /// <param name="inputName">The name of the digital input (must match a DiscreteInput sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current state: true = ON, false = OFF, null if not found</returns>
    public async Task<bool?> GetDigitalInputAsync(string inputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetDigitalInputAsync: {InputName}", inputName);

        var command = new DeviceCommand
        {
            DeviceCmd = "GetDI",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = inputName
            }
        };

        var result = await ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("GetDigitalInputAsync failed for {InputName}: {Error}",
                inputName, result.FirstError.Description);
            return null;
        }

        var state = Convert.ToBoolean(result.Value);
        _logger.LogDebug("GetDigitalInputAsync succeeded: {InputName}={State}", inputName, state);
        return state;
    }

    #endregion

    #region IAnalogInputReadable Implementation

    /// <summary>
    /// Reads the current value of an analog input using Modbus FC04 (Read Input Registers).
    /// </summary>
    /// <param name="inputName">The name of the analog input (must match an InputRegister sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current value, or null if not found</returns>
    public async Task<double?> GetAnalogInputAsync(string inputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetAnalogInputAsync: {InputName}", inputName);

        var command = new DeviceCommand
        {
            DeviceCmd = "GetAI",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = inputName
            }
        };

        var result = await ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("GetAnalogInputAsync failed for {InputName}: {Error}",
                inputName, result.FirstError.Description);
            return null;
        }

        var value = Convert.ToDouble(result.Value);
        _logger.LogDebug("GetAnalogInputAsync succeeded: {InputName}={Value}", inputName, value);
        return value;
    }

    #endregion

    protected static byte GetSlaveId(DeviceConfiguration configuration)
    {
        // Check Properties first (recommended), then DeviceCommunication for backwards compatibility
        return configuration.Properties.TryGetValue("SlaveId", out _)
            ? (byte)configuration.Properties.GetInt32("SlaveId", 1)
            : (byte)configuration.DeviceCommunication.GetInt32("SlaveId", 1);
    }

    protected static ModbusByteOrder GetByteOrder(DeviceConfiguration configuration)
    {
        // Check Properties first (recommended), then DeviceCommunication for backwards compatibility
        return configuration.Properties.TryGetValue("ByteOrder", out _)
            ? configuration.Properties.GetEnum("ByteOrder", ModbusByteOrder.BigEndian)
            : configuration.DeviceCommunication.GetEnum("ByteOrder", ModbusByteOrder.BigEndian);
    }
}

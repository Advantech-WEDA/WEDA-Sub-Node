using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus protocol parser implementation for Request-Response pattern.
/// This parser owns Communication and provides high-level methods for reading/writing sensor data.
///
/// Responsibilities:
/// - Protocol frame building (MBAP header + PDU)
/// - Communication (owns IRequestResponseCommunication)
/// - Response parsing (MBAP + register extraction)
/// - Batch optimization (via ModbusBatchReader)
/// - Type conversion (ushort[] -> C# primitives)
/// - Sensor data mapping (protocol fields -> TelemetryMeasure)
///
/// Architecture:
/// Device -> Parser -> Communication
/// ModbusDevice only calls high-level methods like ReadSensorDataAsync()
/// </summary>
public class ModbusRequestResponseParser : IRequestResponseProtocolParser
{
    private readonly IRequestResponseCommunication<byte[], byte[]> _communication;
    private readonly byte _slaveId;
    private readonly ILogger _logger;
    private readonly ModbusBatchReader _batchReader;
    private readonly bool _useBatchOptimization;
    private readonly Dictionary<string, ModbusSensorRegister> _sensorMetadata;
    private ushort _transactionId = 0;

    /// <summary>
    /// Initializes a new instance of ModbusRequestResponseParser
    /// </summary>
    /// <param name="communication">TCP communication instance for Modbus protocol</param>
    /// <param name="slaveId">Modbus slave ID (device address)</param>
    /// <param name="sensorMetadata">Sensor metadata mapping (field name -> register info)</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="useBatchOptimization">Enable batch reading optimization (default: true)</param>
    /// <param name="batchOptions">Batch optimization options (optional)</param>
    public ModbusRequestResponseParser(
        IRequestResponseCommunication<byte[], byte[]> communication,
        byte slaveId,
        Dictionary<string, ModbusSensorRegister> sensorMetadata,
        ILogger logger,
        bool useBatchOptimization = true,
        ModbusBatchOptimizationOptions? batchOptions = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _slaveId = slaveId;
        _sensorMetadata = sensorMetadata ?? throw new ArgumentNullException(nameof(sensorMetadata));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _useBatchOptimization = useBatchOptimization;

        // Create batch reader for optimized multi-sensor reading
        _batchReader = new ModbusBatchReader(
            _communication,
            _slaveId,
            _logger,
            batchOptions);

        if (_useBatchOptimization)
        {
            _logger.LogDebug(
                "Modbus batch optimization ENABLED for {SensorCount} sensors",
                _sensorMetadata.Count);
        }
        else
        {
            _logger.LogDebug(
                "Modbus batch optimization DISABLED (using legacy single-point reading)");
        }
    }

    // ===== IProtocolParserCore Implementation =====

    /// <summary>
    /// Parser owns Communication (correct layering)
    /// </summary>
    public ICommunication Communication => _communication;

    public string ProtocolName => "Modbus TCP";

    public IReadOnlyList<string> SupportedDataTypes => new[]
    {
        "Int16", "UInt16", "Int32", "UInt32", "Float32", "Float64", "Boolean", "String16"
    };

    public bool SupportsBidirectional => true;

    // ===== IRequestResponseProtocolParser Implementation =====

    /// <summary>
    /// Read sensor data synchronously (Request-Response pattern).
    /// This is the high-level method that Device calls.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadSensorDataAsync(
        SensorMapping sensorMapping,
        CancellationToken cancellationToken = default)
    {
        var measures = new List<TelemetryMeasure>();

        if (sensorMapping == null || sensorMapping.FieldToResourceId.Count == 0)
        {
            _logger.LogTrace("No sensor mapping provided");
            return measures;
        }

        // Filter sensors based on mapping and get enabled sensors
        var enabledSensors = _sensorMetadata
            .Where(kvp => sensorMapping.FieldToResourceId.ContainsKey(kvp.Key))
            .Select(kvp => kvp.Value)
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
                    var resourceId = sensorMapping.FieldToResourceId[sensorName];
                    var measure = new TelemetryMeasure
                    {
                        ResourceId = resourceId,
                        Value = result.Value,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    measures.Add(measure);

                    _logger.LogDebug(
                        "[DATA] Telemetry: {SensorName} = {Value} (ResourceId: {ResourceId}) | Raw={Raw}",
                        sensorName,
                        result.Value,
                        resourceId,
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
                    _logger.LogDebug(
                        "Reading Modbus register: Address={Address}, Count={Count}, Type={Type}",
                        register.RegisterAddress,
                        register.RegisterCount,
                        register.RegisterType);

                    // Read raw data from hardware via communication
                    var rawData = await ReadModbusRegistersAsync(
                        register.RegisterType,
                        register.RegisterAddress,
                        register.RegisterCount,
                        cancellationToken);

                    // Parse registers to C# type
                    var parsedValue = ParseValue(rawData, register.DataType);

                    // Create telemetry measure
                    var resourceId = sensorMapping.FieldToResourceId[register.Name];
                    var measure = new TelemetryMeasure
                    {
                        ResourceId = resourceId,
                        Value = parsedValue,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    measures.Add(measure);

                    _logger.LogDebug(
                        "[DATA] Telemetry: {SensorName} = {Value} (ResourceId: {ResourceId}) | Raw={Raw}",
                        register.Name,
                        parsedValue,
                        resourceId,
                        string.Join(",", rawData));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error reading sensor {SensorName} at address {Address}",
                        register.Name,
                        register.RegisterAddress);
                }
            }
        }

        return measures;
    }

    /// <summary>
    /// Execute command synchronously (Request-Response pattern)
    /// </summary>
    public async Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Modbus command {CommandName}", command.DeviceCmd);

        try
        {
            // TODO: Parse command parameters to extract:
            // - Function code (FC 05: Write Single Coil, FC 06: Write Single Register,
            //                  FC 15: Write Multiple Coils, FC 16: Write Multiple Registers)
            // - Register address
            // - Value(s) to write

            // Example implementation for FC 06 (Write Single Register):
            // var functionCode = 0x06;
            // var registerAddress = ExtractRegisterAddress(command);
            // var value = ExtractValue(command);
            // var request = BuildModbusWriteRequest(functionCode, registerAddress, value);
            // var response = await _communication.RequestAsync(request, cancellationToken);
            // ValidateWriteResponse(response);

            await Task.CompletedTask; // Remove when implementation is added
            return "Command execution not yet implemented";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandName}", command.DeviceCmd);
            return Error.Failure(
                code: "Command.ExecutionFailed",
                description: ex.Message);
        }
    }

    /// <summary>
    /// Write sensor data to device (if protocol supports write operations)
    /// </summary>
    public async Task<bool> WriteSensorDataAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Implement Modbus write operations
            // For each measure:
            // 1. Look up sensor metadata to get register address
            // 2. Encode value to ushort[]
            // 3. Build Modbus write request (FC 16: Write Multiple Registers)
            // 4. Send request and validate response

            _logger.LogWarning("WriteSensorDataAsync not yet implemented for Modbus");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing sensor data");
            return false;
        }
    }

    // ===== Private Protocol Methods =====

    /// <summary>
    /// Read Modbus registers (low-level communication)
    /// </summary>
    private async Task<ushort[]> ReadModbusRegistersAsync(
        ModbusRegisterType registerType,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken)
    {
        // Function code based on register type
        byte functionCode = registerType switch
        {
            ModbusRegisterType.HoldingRegister => 0x03,
            ModbusRegisterType.InputRegister => 0x04,
            _ => throw new NotSupportedException($"Register type {registerType} not supported")
        };

        var request = BuildModbusRequest(functionCode, startAddress, count);
        var response = await _communication.RequestAsync(request, cancellationToken);
        return ParseModbusResponse(response, count);
    }

    /// <summary>
    /// Build Modbus TCP request (MBAP header + PDU)
    /// </summary>
    private byte[] BuildModbusRequest(byte functionCode, ushort startAddress, ushort count)
    {
        var transactionId = ++_transactionId;

        return new byte[]
        {
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),  // Transaction ID
            0x00, 0x00,                                                 // Protocol ID
            0x00, 0x06,                                                 // Length
            _slaveId,                                                   // Unit ID
            functionCode,                                               // Function code
            (byte)(startAddress >> 8), (byte)(startAddress & 0xFF),    // Start address
            (byte)(count >> 8), (byte)(count & 0xFF)                   // Register count
        };
    }

    /// <summary>
    /// Parse Modbus TCP response (MBAP header + register data)
    /// </summary>
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
    /// Parse Modbus registers (ushort[]) to C# primitive type
    /// </summary>
    private object ParseValue(ushort[] rawData, ModbusDataType dataType)
    {
        if (rawData == null || rawData.Length == 0)
            throw new ArgumentException("Raw data cannot be null or empty", nameof(rawData));

        // Use the existing ModbusProtocolParser for type conversion
        var parser = new ModbusProtocolParser(dataType);
        return parser.Parse(rawData);
    }
}

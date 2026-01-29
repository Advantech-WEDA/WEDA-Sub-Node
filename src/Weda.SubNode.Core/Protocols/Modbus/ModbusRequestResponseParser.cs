using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus protocol parser implementation for Request-Response pattern.
/// This parser owns Communication and DeviceConfiguration, handling all mapping logic internally.
///
/// Responsibilities:
/// - Protocol frame building (MBAP header + PDU)
/// - Communication (owns IRequestResponseCommunication)
/// - Response parsing (MBAP + register extraction)
/// - Batch optimization (via ModbusBatchReader)
/// - Type conversion (ushort[] -> C# primitives)
/// - Sensor data mapping (protocol fields -> TelemetryMeasure using DeviceConfiguration)
///
/// Architecture:
/// Device -> Parser -> Communication
/// Device only calls ReadTelemetryAsync() - Parser handles all mapping internally
/// </summary>
public class ModbusRequestResponseParser : IRequestResponseProtocolParser
{
    private readonly IRequestResponseCommunication<byte[], byte[]> _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly byte _slaveId;
    private readonly ModbusByteOrder _byteOrder;
    private readonly ILogger _logger;
    private readonly ModbusBatchReader _batchReader;
    private readonly bool _useBatchOptimization;
    private readonly Dictionary<string, ModbusSensorRegister> _sensorMetadata;
    private readonly int _defaultCommandTimeoutMs;
    private ushort _transactionId = 0;

    /// <summary>
    /// Initializes a new instance of ModbusRequestResponseParser
    /// </summary>
    /// <param name="configuration">Device configuration containing sensor settings</param>
    /// <param name="communication">TCP communication instance for Modbus protocol</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="useBatchOptimization">Enable batch reading optimization (default: true)</param>
    /// <param name="batchOptions">Batch optimization options (optional)</param>
    /// <param name="defaultCommandTimeoutMs">Default command execution timeout in milliseconds (default: 30000)</param>
    /// <param name="slaveId">Modbus slave ID (default: 1)</param>
    /// <param name="byteOrder">Byte order for multi-register data types (default: BigEndian)</param>
    public ModbusRequestResponseParser(
        DeviceConfiguration configuration,
        IRequestResponseCommunication<byte[], byte[]> communication,
        ILogger logger,
        bool useBatchOptimization = true,
        ModbusBatchOptimizationOptions? batchOptions = null,
        int defaultCommandTimeoutMs = 30000,
        byte slaveId = 1,
        ModbusByteOrder byteOrder = ModbusByteOrder.BigEndian)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _useBatchOptimization = useBatchOptimization;
        _defaultCommandTimeoutMs = defaultCommandTimeoutMs;

        // Use provided protocol settings
        _slaveId = slaveId;
        _byteOrder = byteOrder;

        // Convert sensors to Modbus registers and create metadata dictionary
        _sensorMetadata = configuration.Sensors
            .Select(s => s.ToModbusRegister())
            .ToDictionary(r => r.Name, r => r);

        // Create batch reader for optimized multi-sensor reading
        _batchReader = new ModbusBatchReader(
            _communication,
            _slaveId,
            _byteOrder,
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

    /// <summary>
    /// Refresh sensor metadata from current configuration.
    /// Must be called after sensors are added/removed/updated at runtime.
    /// </summary>
    public void RefreshSensorMetadata()
    {
        _sensorMetadata.Clear();

        foreach (var sensor in _configuration.Sensors)
        {
            var register = sensor.ToModbusRegister();
            _sensorMetadata[register.Name] = register;
        }

        _logger.LogDebug(
            "Refreshed Modbus sensor metadata: {SensorCount} sensors",
            _sensorMetadata.Count);
    }

    // ===== IRequestResponseProtocolParser Implementation =====

    /// <summary>
    /// Read telemetry data synchronously (Request-Response pattern).
    /// Parser internally handles all mapping logic using DeviceConfiguration.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = new List<TelemetryMeasure>();

        // Get enabled sensors from configuration
        var enabledSensors = _configuration.Sensors
            .Where(s => s.Report.Enabled)
            .ToList();

        if (enabledSensors.Count == 0)
        {
            _logger.LogTrace("No enabled sensors to read");
            return measures;
        }

        // Get sensor registers for enabled sensors
        var sensorRegisters = enabledSensors
            .Where(s => _sensorMetadata.ContainsKey(s.Name))
            .Select(s => _sensorMetadata[s.Name])
            .ToList();

        if (sensorRegisters.Count == 0)
        {
            _logger.LogTrace("No sensor metadata found for enabled sensors");
            return measures;
        }

        // Build ResourceId lookup from configuration
        var resourceIdLookup = enabledSensors.ToDictionary(s => s.Name, s => s.ResourceId);

        if (_useBatchOptimization)
        {
            // Use batch reader for optimized multi-sensor reading
            var results = await _batchReader.ReadSensorsAsync(sensorRegisters, cancellationToken);

            foreach (var (sensorName, result) in results)
            {
                if (result.Success && result.Value != null)
                {
                    var resourceId = resourceIdLookup[sensorName];
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
            foreach (var register in sensorRegisters)
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

                    // Parse registers to C# type with device-level byte order
                    var parsedValue = ParseValue(rawData, register.DataType);

                    // Create telemetry measure
                    var resourceId = resourceIdLookup[register.Name];
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
    /// Enforces timeout from DeviceOptions (default 30 seconds as per UC9884 specification).
    /// </summary>
    public async Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing Modbus command {CommandName}", command.DeviceCmd);

        // Determine timeout from command or use default from DeviceOptions
        var timeoutMs = command.Timeout > 0 ? (int)command.Timeout : _defaultCommandTimeoutMs;
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return command.DeviceCmd switch
            {
                "SetDO" or "SetDigitalOutput" => await ExecuteSetDOAsync(command, linkedCts.Token),
                _ => Error.Validation(
                    code: "Command.NotSupported",
                    description: $"Command '{command.DeviceCmd}' is not supported by Modbus protocol")
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogError(
                "Command {CommandName} execution timeout after {Timeout}ms",
                command.DeviceCmd, timeoutMs);
            return Error.Failure(
                code: "Command.Timeout",
                description: $"Command execution exceeded {timeoutMs}ms timeout");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Command {CommandName} execution cancelled by caller", command.DeviceCmd);
            return Error.Failure(
                code: "Command.Cancelled",
                description: "Command execution was cancelled");
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
    /// Execute SetDO command (FC 05: Write Single Coil)
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteSetDOAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Extract output name from parameters (supports multiple aliases)
        var name = ExtractOutputName(command.Parameters);
        if (string.IsNullOrEmpty(name))
        {
            return Error.Validation(
                code: "SetDO.MissingName",
                description: "Missing 'name', 'do', or 'outputName' parameter");
        }

        // 2. Extract state parameter
        if (!command.Parameters.TryGetValue("state", out var stateObj))
        {
            return Error.Validation(
                code: "SetDO.MissingState",
                description: "Missing 'state' parameter");
        }

        bool state;
        try
        {
            state = ConvertToBoolean(stateObj);
        }
        catch (Exception)
        {
            return Error.Validation(
                code: "SetDO.InvalidState",
                description: $"Invalid 'state' value: {stateObj}. Expected boolean.");
        }

        // 3. Find sensor in metadata (case-insensitive)
        var sensorEntry = _sensorMetadata
            .FirstOrDefault(kvp => kvp.Key.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (sensorEntry.Value == null)
        {
            return Error.NotFound(
                code: "SetDO.SensorNotFound",
                description: $"Sensor '{name}' not found in device configuration");
        }

        var sensor = sensorEntry.Value;

        // 4. Validate register type is Coil
        if (sensor.RegisterType != ModbusRegisterType.Coil)
        {
            return Error.Validation(
                code: "SetDO.InvalidRegisterType",
                description: $"Sensor '{name}' is not a Coil register (actual: {sensor.RegisterType}). " +
                             "SetDO command requires RegisterType=Coil.");
        }

        // 5. Build and send Modbus FC 05 request
        _logger.LogDebug(
            "Writing Coil: Address={Address}, State={State}",
            sensor.RegisterAddress, state);

        var request = BuildWriteSingleCoilRequest(sensor.RegisterAddress, state);
        var response = await _communication.RequestAsync(request, cancellationToken);

        // 6. Validate response
        ValidateWriteCoilResponse(response, sensor.RegisterAddress, state);

        _logger.LogInformation(
            "SetDO succeeded: {Name} (Address={Address}) = {State}",
            name, sensor.RegisterAddress, state);

        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["name"] = name,
            ["address"] = sensor.RegisterAddress,
            ["state"] = state
        };
    }

    /// <summary>
    /// Extract output name from command parameters (supports multiple aliases)
    /// </summary>
    private static string? ExtractOutputName(Dictionary<string, object> parameters)
    {
        string[] aliases = ["name", "do", "outputName"];

        foreach (var alias in aliases)
        {
            if (parameters.TryGetValue(alias, out var value) && value != null)
            {
                // Handle JsonElement from JSON deserialization
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    return jsonElement.GetString();
                }
                return value.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Convert object to boolean, handling JsonElement from JSON deserialization
    /// </summary>
    private static bool ConvertToBoolean(object value)
    {
        // Handle JsonElement from JSON deserialization
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.Number => jsonElement.GetInt32() != 0,
                System.Text.Json.JsonValueKind.String => bool.Parse(jsonElement.GetString()!),
                _ => throw new InvalidOperationException($"Cannot convert JsonElement of kind {jsonElement.ValueKind} to boolean")
            };
        }

        // Handle other types
        return Convert.ToBoolean(value);
    }

    /// <summary>
    /// Build Modbus FC 05 (Write Single Coil) request
    /// </summary>
    private byte[] BuildWriteSingleCoilRequest(ushort coilAddress, bool state)
    {
        var transactionId = ++_transactionId;
        // Modbus coil value: 0xFF00 = ON, 0x0000 = OFF
        var value = state ? (ushort)0xFF00 : (ushort)0x0000;

        return
        [
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),  // Transaction ID
            0x00, 0x00,                                                 // Protocol ID
            0x00, 0x06,                                                 // Length (6 bytes follow)
            _slaveId,                                                   // Unit ID
            0x05,                                                       // Function Code (Write Single Coil)
            (byte)(coilAddress >> 8), (byte)(coilAddress & 0xFF),      // Coil Address
            (byte)(value >> 8), (byte)(value & 0xFF)                   // Value (0xFF00 or 0x0000)
        ];
    }

    /// <summary>
    /// Validate Modbus FC 05 response (echo of request on success)
    /// </summary>
    private static void ValidateWriteCoilResponse(byte[] response, ushort expectedAddress, bool expectedState)
    {
        // Minimum response length: MBAP header (7) + FC (1) + Address (2) + Value (2) = 12 bytes
        if (response.Length < 12)
        {
            throw new InvalidOperationException(
                $"Invalid Modbus response length: {response.Length}, expected at least 12 bytes");
        }

        // Check for error response (function code has high bit set)
        var functionCode = response[7];
        if ((functionCode & 0x80) != 0)
        {
            var errorCode = response[8];
            throw new InvalidOperationException(
                $"Modbus error response: {GetModbusErrorDescription(errorCode)} (0x{errorCode:X2})");
        }

        // Verify function code is FC 05
        if (functionCode != 0x05)
        {
            throw new InvalidOperationException(
                $"Unexpected function code in response: 0x{functionCode:X2}, expected 0x05");
        }

        // Verify echoed address matches
        var actualAddress = (ushort)((response[8] << 8) | response[9]);
        if (actualAddress != expectedAddress)
        {
            throw new InvalidOperationException(
                $"Address mismatch in response: expected {expectedAddress}, got {actualAddress}");
        }

        // Verify echoed value matches
        var actualValue = (ushort)((response[10] << 8) | response[11]);
        var expectedValue = expectedState ? (ushort)0xFF00 : (ushort)0x0000;

        if (actualValue != expectedValue)
        {
            throw new InvalidOperationException(
                $"Value mismatch in response: expected 0x{expectedValue:X4}, got 0x{actualValue:X4}");
        }
    }

    /// <summary>
    /// Get human-readable Modbus error description
    /// </summary>
    private static string GetModbusErrorDescription(byte errorCode) => errorCode switch
    {
        0x01 => "Illegal Function",
        0x02 => "Illegal Data Address",
        0x03 => "Illegal Data Value",
        0x04 => "Slave Device Failure",
        0x05 => "Acknowledge",
        0x06 => "Slave Device Busy",
        0x08 => "Memory Parity Error",
        0x0A => "Gateway Path Unavailable",
        0x0B => "Gateway Target Device Failed to Respond",
        _ => "Unknown Error"
    };

    /// <summary>
    /// Write sensor data to device (if protocol supports write operations)
    /// </summary>
    public Task<bool> WriteSensorDataAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement Modbus write operations
        // For each measure:
        // 1. Look up sensor metadata to get register address
        // 2. Encode value to ushort[]
        // 3. Build Modbus write request (FC 16: Write Multiple Registers)
        // 4. Send request and validate response

        _logger.LogWarning("WriteSensorDataAsync not yet implemented for Modbus");
        return Task.FromResult(false);
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
    /// Parse Modbus registers (ushort[]) to C# primitive type using device-level byte order
    /// </summary>
    private object ParseValue(ushort[] rawData, ModbusDataType dataType)
    {
        if (rawData == null || rawData.Length == 0)
            throw new ArgumentException("Raw data cannot be null or empty", nameof(rawData));

        // Use the existing ModbusProtocolParser for type conversion with device-level byte order
        var parser = new ModbusProtocolParser(dataType, _byteOrder);
        return parser.Parse(rawData);
    }
}

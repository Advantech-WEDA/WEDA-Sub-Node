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
/// - Protocol PDU building [SlaveId, FC, Data...]
/// - Communication (owns IRequestResponseCommunication)
/// - Response parsing (PDU + register extraction)
/// - Batch optimization (via ModbusBatchReader)
/// - Type conversion (ushort[] -> C# primitives)
/// - Sensor data mapping (protocol fields -> TelemetryMeasure using DeviceConfiguration)
///
/// Architecture:
/// Device -> Parser -> Communication (ModbusTcpCommunication or ModbusRtuCommunication)
/// Communication wrapper handles frame encapsulation (MBAP for TCP, CRC for RTU)
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
            .Where(s => s.IsEffectivelyEnabled)
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
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Caller cancelled (config update / shutdown) - stop reading remaining sensors
                    throw;
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
                // Read commands
                // FC 01: Read Coils (Digital Outputs)
                "GetDO" or "GetDigitalOutput" or "ReadCoil" => await ExecuteGetDOAsync(command, linkedCts.Token),
                // FC 02: Read Discrete Inputs (Digital Inputs)
                "GetDI" or "GetDigitalInput" or "ReadDiscreteInput" => await ExecuteGetDIAsync(command, linkedCts.Token),
                // FC 03: Read Holding Registers (Analog Outputs)
                "GetAO" or "GetAnalogOutput" or "ReadHoldingRegister" => await ExecuteGetAOAsync(command, linkedCts.Token),
                // FC 04: Read Input Registers (Analog Inputs)
                "GetAI" or "GetAnalogInput" or "ReadInputRegister" => await ExecuteGetAIAsync(command, linkedCts.Token),

                // Write commands
                // FC 05: Write Single Coil
                "SetDO" or "SetDigitalOutput" => await ExecuteSetDOAsync(command, linkedCts.Token),
                // FC 06: Write Single Register
                "SetAO" or "SetAnalogOutput" or "WriteRegister" => await ExecuteSetAOAsync(command, linkedCts.Token),
                // FC 15: Write Multiple Coils
                "SetMultipleDO" or "WriteCoils" => await ExecuteWriteMultipleCoilsAsync(command, linkedCts.Token),
                // FC 16: Write Multiple Registers
                "SetMultipleAO" or "WriteRegisters" => await ExecuteWriteMultipleRegistersAsync(command, linkedCts.Token),
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

    #region Read Commands (FC 01-04)

    /// <summary>
    /// Execute GetDO command (FC 01: Read Single Coil)
    /// Reads the current state of a digital output (coil).
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteGetDOAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        var name = ExtractOutputName(command.Parameters);
        if (string.IsNullOrEmpty(name))
        {
            return Error.Validation(
                code: "GetDO.MissingName",
                description: "Missing 'name', 'do', or 'outputName' parameter");
        }

        // Find sensor metadata
        if (!_sensorMetadata.TryGetValue(name, out var register))
        {
            return Error.NotFound(
                code: "GetDO.SensorNotFound",
                description: $"Sensor '{name}' not found in configuration");
        }

        // Verify it's a Coil type
        if (register.RegisterType != ModbusRegisterType.Coil)
        {
            return Error.Validation(
                code: "GetDO.InvalidSensorType",
                description: $"Sensor '{name}' is not a Coil type (found: {register.RegisterType})");
        }

        // Read the coil using batch reader
        var result = await _batchReader.ReadSingleSensorAsync(register, cancellationToken);

        if (!result.Success)
        {
            return Error.Failure(
                code: "GetDO.ReadFailed",
                description: result.ErrorMessage ?? "Failed to read coil");
        }

        _logger.LogDebug("GetDO: {Name} = {Value}", name, result.Value);
        return result.Value!;
    }

    /// <summary>
    /// Execute GetDI command (FC 02: Read Single Discrete Input)
    /// Reads the current state of a digital input.
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteGetDIAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        var name = ExtractInputName(command.Parameters);
        if (string.IsNullOrEmpty(name))
        {
            return Error.Validation(
                code: "GetDI.MissingName",
                description: "Missing 'name', 'di', or 'inputName' parameter");
        }

        // Find sensor metadata
        if (!_sensorMetadata.TryGetValue(name, out var register))
        {
            return Error.NotFound(
                code: "GetDI.SensorNotFound",
                description: $"Sensor '{name}' not found in configuration");
        }

        // Verify it's a DiscreteInput type
        if (register.RegisterType != ModbusRegisterType.DiscreteInput)
        {
            return Error.Validation(
                code: "GetDI.InvalidSensorType",
                description: $"Sensor '{name}' is not a DiscreteInput type (found: {register.RegisterType})");
        }

        // Read the discrete input using batch reader
        var result = await _batchReader.ReadSingleSensorAsync(register, cancellationToken);

        if (!result.Success)
        {
            return Error.Failure(
                code: "GetDI.ReadFailed",
                description: result.ErrorMessage ?? "Failed to read discrete input");
        }

        _logger.LogDebug("GetDI: {Name} = {Value}", name, result.Value);
        return result.Value!;
    }

    /// <summary>
    /// Execute GetAO command (FC 03: Read Holding Register)
    /// Reads the current value of an analog output (holding register).
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteGetAOAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        var name = ExtractOutputName(command.Parameters);
        if (string.IsNullOrEmpty(name))
        {
            return Error.Validation(
                code: "GetAO.MissingName",
                description: "Missing 'name', 'ao', or 'outputName' parameter");
        }

        // Find sensor metadata
        if (!_sensorMetadata.TryGetValue(name, out var register))
        {
            return Error.NotFound(
                code: "GetAO.SensorNotFound",
                description: $"Sensor '{name}' not found in configuration");
        }

        // Verify it's a HoldingRegister type
        if (register.RegisterType != ModbusRegisterType.HoldingRegister)
        {
            return Error.Validation(
                code: "GetAO.InvalidSensorType",
                description: $"Sensor '{name}' is not a HoldingRegister type (found: {register.RegisterType})");
        }

        // Read the holding register using batch reader
        var result = await _batchReader.ReadSingleSensorAsync(register, cancellationToken);

        if (!result.Success)
        {
            return Error.Failure(
                code: "GetAO.ReadFailed",
                description: result.ErrorMessage ?? "Failed to read holding register");
        }

        _logger.LogDebug("GetAO: {Name} = {Value}", name, result.Value);
        return result.Value!;
    }

    /// <summary>
    /// Execute GetAI command (FC 04: Read Input Register)
    /// Reads the current value of an analog input (input register).
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteGetAIAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        var name = ExtractInputName(command.Parameters);
        if (string.IsNullOrEmpty(name))
        {
            return Error.Validation(
                code: "GetAI.MissingName",
                description: "Missing 'name', 'ai', or 'inputName' parameter");
        }

        // Find sensor metadata
        if (!_sensorMetadata.TryGetValue(name, out var register))
        {
            return Error.NotFound(
                code: "GetAI.SensorNotFound",
                description: $"Sensor '{name}' not found in configuration");
        }

        // Verify it's an InputRegister type
        if (register.RegisterType != ModbusRegisterType.InputRegister)
        {
            return Error.Validation(
                code: "GetAI.InvalidSensorType",
                description: $"Sensor '{name}' is not an InputRegister type (found: {register.RegisterType})");
        }

        // Read the input register using batch reader
        var result = await _batchReader.ReadSingleSensorAsync(register, cancellationToken);

        if (!result.Success)
        {
            return Error.Failure(
                code: "GetAI.ReadFailed",
                description: result.ErrorMessage ?? "Failed to read input register");
        }

        _logger.LogDebug("GetAI: {Name} = {Value}", name, result.Value);
        return result.Value!;
    }

    /// <summary>
    /// Extracts input name from command parameters.
    /// Supports aliases: 'name', 'di', 'ai', 'inputName'
    /// </summary>
    private static string? ExtractInputName(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("name", out var nameObj))
            return nameObj?.ToString();
        if (parameters.TryGetValue("di", out var diObj))
            return diObj?.ToString();
        if (parameters.TryGetValue("ai", out var aiObj))
            return aiObj?.ToString();
        if (parameters.TryGetValue("inputName", out var inputNameObj))
            return inputNameObj?.ToString();
        return null;
    }

    #endregion

    #region Write Commands (FC 05-16)

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
    /// Execute SetAO command (FC 06: Write Single Register)
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteSetAOAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Extract output name from parameters
        var name = ExtractOutputName(command.Parameters);
        if (string.IsNullOrEmpty(name))
        {
            return Error.Validation(
                code: "SetAO.MissingName",
                description: "Missing 'name', 'ao', or 'outputName' parameter");
        }

        // 2. Extract value parameter
        if (!command.Parameters.TryGetValue("value", out var valueObj))
        {
            return Error.Validation(
                code: "SetAO.MissingValue",
                description: "Missing 'value' parameter");
        }

        ushort value;
        try
        {
            value = ConvertToUInt16(valueObj);
        }
        catch (Exception)
        {
            return Error.Validation(
                code: "SetAO.InvalidValue",
                description: $"Invalid 'value': {valueObj}. Expected integer 0-65535.");
        }

        // 3. Find sensor in metadata (case-insensitive)
        var sensorEntry = _sensorMetadata
            .FirstOrDefault(kvp => kvp.Key.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (sensorEntry.Value == null)
        {
            return Error.NotFound(
                code: "SetAO.SensorNotFound",
                description: $"Sensor '{name}' not found in device configuration");
        }

        var sensor = sensorEntry.Value;

        // 4. Validate register type is HoldingRegister
        if (sensor.RegisterType != ModbusRegisterType.HoldingRegister)
        {
            return Error.Validation(
                code: "SetAO.InvalidRegisterType",
                description: $"Sensor '{name}' is not a HoldingRegister (actual: {sensor.RegisterType}). " +
                             "SetAO command requires RegisterType=HoldingRegister.");
        }

        // 5. Build and send Modbus FC 06 request
        _logger.LogDebug(
            "Writing Register: Address={Address}, Value={Value}",
            sensor.RegisterAddress, value);

        var request = BuildWriteSingleRegisterRequest(sensor.RegisterAddress, value);
        var response = await _communication.RequestAsync(request, cancellationToken);

        // 6. Validate response
        ValidateWriteRegisterResponse(response, sensor.RegisterAddress, value);

        _logger.LogInformation(
            "SetAO succeeded: {Name} (Address={Address}) = {Value}",
            name, sensor.RegisterAddress, value);

        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["name"] = name,
            ["address"] = sensor.RegisterAddress,
            ["value"] = value
        };
    }

    /// <summary>
    /// Execute WriteMultipleCoils command (FC 15: Write Multiple Coils)
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteWriteMultipleCoilsAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Extract address (required for multi-write)
        if (!command.Parameters.TryGetValue("address", out var addressObj))
        {
            return Error.Validation(
                code: "WriteCoils.MissingAddress",
                description: "Missing 'address' parameter");
        }

        ushort address;
        try
        {
            address = ConvertToUInt16(addressObj);
        }
        catch (Exception)
        {
            return Error.Validation(
                code: "WriteCoils.InvalidAddress",
                description: $"Invalid 'address': {addressObj}. Expected integer 0-65535.");
        }

        // 2. Extract states array
        if (!command.Parameters.TryGetValue("states", out var statesObj))
        {
            return Error.Validation(
                code: "WriteCoils.MissingStates",
                description: "Missing 'states' parameter (array of booleans)");
        }

        bool[] states;
        try
        {
            states = ConvertToBooleanArray(statesObj);
        }
        catch (Exception ex)
        {
            return Error.Validation(
                code: "WriteCoils.InvalidStates",
                description: $"Invalid 'states': {ex.Message}. Expected array of booleans.");
        }

        if (states.Length == 0)
        {
            return Error.Validation(
                code: "WriteCoils.EmptyStates",
                description: "States array cannot be empty");
        }

        // 3. Build and send Modbus FC 15 request
        _logger.LogDebug(
            "Writing Multiple Coils: Address={Address}, Count={Count}",
            address, states.Length);

        var request = BuildWriteMultipleCoilsRequest(address, states);
        var response = await _communication.RequestAsync(request, cancellationToken);

        // 4. Validate response
        ValidateWriteMultipleCoilsResponse(response, address, (ushort)states.Length);

        _logger.LogInformation(
            "WriteMultipleCoils succeeded: Address={Address}, Count={Count}",
            address, states.Length);

        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["address"] = address,
            ["count"] = states.Length
        };
    }

    /// <summary>
    /// Execute WriteMultipleRegisters command (FC 16: Write Multiple Registers)
    /// </summary>
    private async Task<ErrorOr<object>> ExecuteWriteMultipleRegistersAsync(
        DeviceCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Extract address (required for multi-write)
        if (!command.Parameters.TryGetValue("address", out var addressObj))
        {
            return Error.Validation(
                code: "WriteRegisters.MissingAddress",
                description: "Missing 'address' parameter");
        }

        ushort address;
        try
        {
            address = ConvertToUInt16(addressObj);
        }
        catch (Exception)
        {
            return Error.Validation(
                code: "WriteRegisters.InvalidAddress",
                description: $"Invalid 'address': {addressObj}. Expected integer 0-65535.");
        }

        // 2. Extract values array
        if (!command.Parameters.TryGetValue("values", out var valuesObj))
        {
            return Error.Validation(
                code: "WriteRegisters.MissingValues",
                description: "Missing 'values' parameter (array of integers)");
        }

        ushort[] values;
        try
        {
            values = ConvertToUInt16Array(valuesObj);
        }
        catch (Exception ex)
        {
            return Error.Validation(
                code: "WriteRegisters.InvalidValues",
                description: $"Invalid 'values': {ex.Message}. Expected array of integers 0-65535.");
        }

        if (values.Length == 0)
        {
            return Error.Validation(
                code: "WriteRegisters.EmptyValues",
                description: "Values array cannot be empty");
        }

        // 3. Build and send Modbus FC 16 request
        _logger.LogDebug(
            "Writing Multiple Registers: Address={Address}, Count={Count}",
            address, values.Length);

        var request = BuildWriteMultipleRegistersRequest(address, values);
        var response = await _communication.RequestAsync(request, cancellationToken);

        // 4. Validate response
        ValidateWriteMultipleRegistersResponse(response, address, (ushort)values.Length);

        _logger.LogInformation(
            "WriteMultipleRegisters succeeded: Address={Address}, Count={Count}",
            address, values.Length);

        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["address"] = address,
            ["count"] = values.Length
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
    /// Convert object to ushort, handling JsonElement from JSON deserialization
    /// </summary>
    private static ushort ConvertToUInt16(object value)
    {
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => (ushort)jsonElement.GetInt32(),
                System.Text.Json.JsonValueKind.String => ushort.Parse(jsonElement.GetString()!),
                _ => throw new InvalidOperationException($"Cannot convert JsonElement of kind {jsonElement.ValueKind} to ushort")
            };
        }

        return Convert.ToUInt16(value);
    }

    /// <summary>
    /// Convert object to boolean array, handling JsonElement from JSON deserialization
    /// </summary>
    private static bool[] ConvertToBooleanArray(object value)
    {
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                throw new InvalidOperationException("Expected array of booleans");

            return jsonElement.EnumerateArray()
                .Select(e => e.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    System.Text.Json.JsonValueKind.Number => e.GetInt32() != 0,
                    _ => throw new InvalidOperationException($"Invalid boolean value: {e}")
                })
                .ToArray();
        }

        if (value is IEnumerable<bool> boolEnumerable)
            return boolEnumerable.ToArray();

        if (value is IEnumerable<object> objEnumerable)
            return objEnumerable.Select(o => Convert.ToBoolean(o)).ToArray();

        throw new InvalidOperationException("Cannot convert value to boolean array");
    }

    /// <summary>
    /// Convert object to ushort array, handling JsonElement from JSON deserialization
    /// </summary>
    private static ushort[] ConvertToUInt16Array(object value)
    {
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                throw new InvalidOperationException("Expected array of integers");

            return jsonElement.EnumerateArray()
                .Select(e => e.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number => (ushort)e.GetInt32(),
                    System.Text.Json.JsonValueKind.String => ushort.Parse(e.GetString()!),
                    _ => throw new InvalidOperationException($"Invalid integer value: {e}")
                })
                .ToArray();
        }

        if (value is IEnumerable<ushort> ushortEnumerable)
            return ushortEnumerable.ToArray();

        if (value is IEnumerable<int> intEnumerable)
            return intEnumerable.Select(i => (ushort)i).ToArray();

        if (value is IEnumerable<object> objEnumerable)
            return objEnumerable.Select(o => Convert.ToUInt16(o)).ToArray();

        throw new InvalidOperationException("Cannot convert value to ushort array");
    }

    /// <summary>
    /// Build Modbus FC 05 (Write Single Coil) PDU
    /// </summary>
    private byte[] BuildWriteSingleCoilRequest(ushort coilAddress, bool state)
    {
        // Modbus coil value: 0xFF00 = ON, 0x0000 = OFF
        var value = state ? (ushort)0xFF00 : (ushort)0x0000;

        return
        [
            _slaveId,                                                   // Slave ID
            0x05,                                                       // Function Code (Write Single Coil)
            (byte)(coilAddress >> 8), (byte)(coilAddress & 0xFF),      // Coil Address
            (byte)(value >> 8), (byte)(value & 0xFF)                   // Value (0xFF00 or 0x0000)
        ];
    }

    /// <summary>
    /// Validate Modbus FC 05 response (echo of request on success)
    /// PDU format: [SlaveId, FC, AddrHi, AddrLo, ValueHi, ValueLo]
    /// </summary>
    private static void ValidateWriteCoilResponse(byte[] response, ushort expectedAddress, bool expectedState)
    {
        // Minimum PDU length: SlaveId (1) + FC (1) = 2 bytes
        // Error response: 3 bytes (SlaveId + FC|0x80 + ErrorCode)
        // Success response: 6 bytes (SlaveId + FC + Address 2 + Value 2)
        if (response.Length < 2)
        {
            throw new InvalidOperationException(
                $"Invalid Modbus response length: {response.Length}, expected at least 2 bytes");
        }

        // Check for error response (function code has high bit set)
        var functionCode = response[1];
        if ((functionCode & 0x80) != 0)
        {
            var errorCode = response.Length > 2 ? response[2] : (byte)0xFF;
            throw new InvalidOperationException(
                $"Modbus error response: {GetModbusErrorDescription(errorCode)} (0x{errorCode:X2})");
        }

        // Verify function code is FC 05
        if (functionCode != 0x05)
        {
            throw new InvalidOperationException(
                $"Unexpected function code in response: 0x{functionCode:X2}, expected 0x05");
        }

        // For success response, we need at least 6 bytes
        if (response.Length < 6)
        {
            throw new InvalidOperationException(
                $"Incomplete Modbus response: {response.Length} bytes, expected 6 bytes for FC05 success response");
        }

        // Verify echoed address matches
        var actualAddress = (ushort)((response[2] << 8) | response[3]);
        if (actualAddress != expectedAddress)
        {
            throw new InvalidOperationException(
                $"Address mismatch in response: expected {expectedAddress}, got {actualAddress}");
        }

        // Verify echoed value matches
        var actualValue = (ushort)((response[4] << 8) | response[5]);
        var expectedValue = expectedState ? (ushort)0xFF00 : (ushort)0x0000;

        if (actualValue != expectedValue)
        {
            throw new InvalidOperationException(
                $"Value mismatch in response: expected 0x{expectedValue:X4}, got 0x{actualValue:X4}");
        }
    }

    /// <summary>
    /// Build Modbus FC 06 (Write Single Register) PDU
    /// </summary>
    private byte[] BuildWriteSingleRegisterRequest(ushort registerAddress, ushort value)
    {
        return
        [
            _slaveId,                                                       // Slave ID
            0x06,                                                           // Function Code (Write Single Register)
            (byte)(registerAddress >> 8), (byte)(registerAddress & 0xFF),  // Register Address
            (byte)(value >> 8), (byte)(value & 0xFF)                       // Value
        ];
    }

    /// <summary>
    /// Validate Modbus FC 06 response (echo of request on success)
    /// PDU format: [SlaveId, FC, AddrHi, AddrLo, ValueHi, ValueLo]
    /// </summary>
    private static void ValidateWriteRegisterResponse(byte[] response, ushort expectedAddress, ushort expectedValue)
    {
        if (response.Length < 6)
        {
            throw new InvalidOperationException(
                $"Invalid Modbus response length: {response.Length}, expected at least 6 bytes");
        }

        var functionCode = response[1];
        if ((functionCode & 0x80) != 0)
        {
            var errorCode = response[2];
            throw new InvalidOperationException(
                $"Modbus error response: {GetModbusErrorDescription(errorCode)} (0x{errorCode:X2})");
        }

        if (functionCode != 0x06)
        {
            throw new InvalidOperationException(
                $"Unexpected function code in response: 0x{functionCode:X2}, expected 0x06");
        }

        var actualAddress = (ushort)((response[2] << 8) | response[3]);
        if (actualAddress != expectedAddress)
        {
            throw new InvalidOperationException(
                $"Address mismatch in response: expected {expectedAddress}, got {actualAddress}");
        }

        var actualValue = (ushort)((response[4] << 8) | response[5]);
        if (actualValue != expectedValue)
        {
            throw new InvalidOperationException(
                $"Value mismatch in response: expected {expectedValue}, got {actualValue}");
        }
    }

    /// <summary>
    /// Build Modbus FC 15 (Write Multiple Coils) PDU
    /// Coils are bit-packed: 8 coils per byte, LSB first
    /// </summary>
    private byte[] BuildWriteMultipleCoilsRequest(ushort startAddress, bool[] states)
    {
        var coilCount = (ushort)states.Length;
        var byteCount = (byte)((coilCount + 7) / 8); // Ceiling division

        // Pack coils into bytes (LSB first)
        var coilBytes = new byte[byteCount];
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i])
            {
                var byteIndex = i / 8;
                var bitIndex = i % 8;
                coilBytes[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        // Build PDU: SlaveId + FC + Address(2) + Count(2) + ByteCount(1) + Data
        var request = new byte[7 + byteCount];

        request[0] = _slaveId;                              // Slave ID
        request[1] = 0x0F;                                  // Function Code (Write Multiple Coils)
        request[2] = (byte)(startAddress >> 8);             // Start Address (high)
        request[3] = (byte)(startAddress & 0xFF);           // Start Address (low)
        request[4] = (byte)(coilCount >> 8);                // Quantity of Coils (high)
        request[5] = (byte)(coilCount & 0xFF);              // Quantity of Coils (low)
        request[6] = byteCount;                             // Byte Count

        // Coil data
        Array.Copy(coilBytes, 0, request, 7, byteCount);

        return request;
    }

    /// <summary>
    /// Validate Modbus FC 15 response
    /// PDU format: [SlaveId, FC, AddrHi, AddrLo, CountHi, CountLo]
    /// </summary>
    private static void ValidateWriteMultipleCoilsResponse(byte[] response, ushort expectedAddress, ushort expectedCount)
    {
        if (response.Length < 6)
        {
            throw new InvalidOperationException(
                $"Invalid Modbus response length: {response.Length}, expected at least 6 bytes");
        }

        var functionCode = response[1];
        if ((functionCode & 0x80) != 0)
        {
            var errorCode = response[2];
            throw new InvalidOperationException(
                $"Modbus error response: {GetModbusErrorDescription(errorCode)} (0x{errorCode:X2})");
        }

        if (functionCode != 0x0F)
        {
            throw new InvalidOperationException(
                $"Unexpected function code in response: 0x{functionCode:X2}, expected 0x0F");
        }

        var actualAddress = (ushort)((response[2] << 8) | response[3]);
        if (actualAddress != expectedAddress)
        {
            throw new InvalidOperationException(
                $"Address mismatch in response: expected {expectedAddress}, got {actualAddress}");
        }

        var actualCount = (ushort)((response[4] << 8) | response[5]);
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"Count mismatch in response: expected {expectedCount}, got {actualCount}");
        }
    }

    /// <summary>
    /// Build Modbus FC 16 (Write Multiple Registers) PDU
    /// </summary>
    private byte[] BuildWriteMultipleRegistersRequest(ushort startAddress, ushort[] values)
    {
        var registerCount = (ushort)values.Length;
        var byteCount = (byte)(registerCount * 2);

        // Build PDU: SlaveId + FC + Address(2) + Count(2) + ByteCount(1) + Data
        var request = new byte[7 + byteCount];

        request[0] = _slaveId;                              // Slave ID
        request[1] = 0x10;                                  // Function Code (Write Multiple Registers)
        request[2] = (byte)(startAddress >> 8);             // Start Address (high)
        request[3] = (byte)(startAddress & 0xFF);           // Start Address (low)
        request[4] = (byte)(registerCount >> 8);            // Quantity of Registers (high)
        request[5] = (byte)(registerCount & 0xFF);          // Quantity of Registers (low)
        request[6] = byteCount;                             // Byte Count

        // Register data (big-endian)
        for (int i = 0; i < values.Length; i++)
        {
            request[7 + (i * 2)] = (byte)(values[i] >> 8);
            request[8 + (i * 2)] = (byte)(values[i] & 0xFF);
        }

        return request;
    }

    /// <summary>
    /// Validate Modbus FC 16 response
    /// PDU format: [SlaveId, FC, AddrHi, AddrLo, CountHi, CountLo]
    /// </summary>
    private static void ValidateWriteMultipleRegistersResponse(byte[] response, ushort expectedAddress, ushort expectedCount)
    {
        if (response.Length < 6)
        {
            throw new InvalidOperationException(
                $"Invalid Modbus response length: {response.Length}, expected at least 6 bytes");
        }

        var functionCode = response[1];
        if ((functionCode & 0x80) != 0)
        {
            var errorCode = response[2];
            throw new InvalidOperationException(
                $"Modbus error response: {GetModbusErrorDescription(errorCode)} (0x{errorCode:X2})");
        }

        if (functionCode != 0x10)
        {
            throw new InvalidOperationException(
                $"Unexpected function code in response: 0x{functionCode:X2}, expected 0x10");
        }

        var actualAddress = (ushort)((response[2] << 8) | response[3]);
        if (actualAddress != expectedAddress)
        {
            throw new InvalidOperationException(
                $"Address mismatch in response: expected {expectedAddress}, got {actualAddress}");
        }

        var actualCount = (ushort)((response[4] << 8) | response[5]);
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"Count mismatch in response: expected {expectedCount}, got {actualCount}");
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

    #endregion

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
    /// Build Modbus read request PDU
    /// PDU format: [SlaveId, FC, AddrHi, AddrLo, CountHi, CountLo]
    /// </summary>
    private byte[] BuildModbusRequest(byte functionCode, ushort startAddress, ushort count)
    {
        return
        [
            _slaveId,                                                   // Slave ID
            functionCode,                                               // Function code
            (byte)(startAddress >> 8), (byte)(startAddress & 0xFF),    // Start address
            (byte)(count >> 8), (byte)(count & 0xFF)                   // Register count
        ];
    }

    /// <summary>
    /// Parse Modbus read response PDU
    /// PDU format: [SlaveId, FC, ByteCount, Data...]
    /// </summary>
    private static ushort[] ParseModbusResponse(byte[] response, ushort expectedCount)
    {
        if (response.Length < 3)
            throw new InvalidOperationException($"Invalid Modbus response length: {response.Length}");

        var byteCount = response[2];
        var expectedByteCount = expectedCount * 2;

        if (byteCount != expectedByteCount)
            throw new InvalidOperationException($"Unexpected byte count: {byteCount}, expected: {expectedByteCount}");

        var registers = new ushort[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            var offset = 3 + (i * 2);
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

using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Optimizes Modbus register reading by batching consecutive register reads.
/// Instead of reading each sensor individually (N Modbus round-trips),
/// this reader merges consecutive registers into batch reads (fewer round-trips).
///
/// Example:
/// Before: Read(0), Read(1), Read(2) → 3 Modbus requests
/// After:  ReadBatch(0, count:3)     → 1 Modbus request
/// </summary>
public class ModbusBatchReader
{
    private readonly IRequestResponseCommunication<byte[], byte[]> _communication;
    private readonly byte _slaveId;
    private readonly ModbusByteOrder _byteOrder;
    private readonly ILogger _logger;
    private readonly ModbusBatchOptimizationOptions _options;
    private ushort _transactionId = 0;

    public ModbusBatchReader(
        IRequestResponseCommunication<byte[], byte[]> communication,
        byte slaveId,
        ModbusByteOrder byteOrder,
        ILogger logger,
        ModbusBatchOptimizationOptions? options = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _slaveId = slaveId;
        _byteOrder = byteOrder;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? ModbusBatchOptimizationOptions.Default;
    }

    /// <summary>
    /// Reads multiple sensors using batch optimization.
    /// Returns a dictionary mapping sensor names to their parsed values.
    /// </summary>
    public async Task<Dictionary<string, SensorReadResult>> ReadSensorsAsync(
        IEnumerable<ModbusSensorRegister> sensors,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, SensorReadResult>();
        var sensorList = sensors.ToList();

        if (sensorList.Count == 0)
            return results;

        // Group sensors by register type (HoldingRegister, InputRegister, etc.)
        var groupedByType = sensorList.GroupBy(s => s.RegisterType);

        foreach (var typeGroup in groupedByType)
        {
            var registerType = typeGroup.Key;
            _logger.LogDebug("Processing {Count} sensors for register type {Type}",
                typeGroup.Count(), registerType);

            // Create batches for this register type
            var batches = CreateOptimizedBatches(typeGroup.ToList());

            _logger.LogDebug(
                "Optimized {SensorCount} sensors into {BatchCount} batch(es) for {RegisterType}",
                typeGroup.Count(),
                batches.Count,
                registerType);

            // Execute each batch
            foreach (var batch in batches)
            {
                try
                {
                    await ExecuteBatchAsync(batch, results, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error reading batch: StartAddress={Start}, Count={Count}",
                        batch.StartAddress,
                        batch.RegisterCount);

                    // Mark all sensors in this batch as failed
                    foreach (var sensor in batch.Sensors)
                    {
                        results[sensor.Name] = new SensorReadResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Creates optimized batches from a list of sensors.
    /// Sensors must be of the same register type.
    /// </summary>
    private List<RegisterBatch> CreateOptimizedBatches(List<ModbusSensorRegister> sensors)
    {
        var batches = new List<RegisterBatch>();

        if (sensors.Count == 0)
            return batches;

        // Sort by register address
        var sortedSensors = sensors.OrderBy(s => s.RegisterAddress).ToList();

        var currentBatch = new RegisterBatch
        {
            RegisterType = sortedSensors[0].RegisterType,
            StartAddress = sortedSensors[0].RegisterAddress,
            Sensors = new List<ModbusSensorRegister> { sortedSensors[0] }
        };

        for (int i = 1; i < sortedSensors.Count; i++)
        {
            var sensor = sortedSensors[i];
            var previousSensor = sortedSensors[i - 1];

            var previousEndAddress = previousSensor.RegisterAddress + previousSensor.RegisterCount;
            var gap = sensor.RegisterAddress - previousEndAddress;

            // Calculate what the batch size would be if we merge this sensor
            var potentialEndAddress = sensor.RegisterAddress + sensor.RegisterCount;
            var potentialBatchSize = potentialEndAddress - currentBatch.StartAddress;

            // Decide whether to merge or create new batch
            bool shouldMerge = gap <= _options.MaxGapSize &&
                               potentialBatchSize <= _options.MaxBatchSize;

            if (shouldMerge)
            {
                // Merge into current batch
                currentBatch.Sensors.Add(sensor);
                _logger.LogTrace(
                    "Merging sensor {Name} into batch (gap={Gap}, batchSize={Size})",
                    sensor.Name, gap, potentialBatchSize);
            }
            else
            {
                // Finalize current batch and start new one
                currentBatch.RegisterCount = CalculateBatchRegisterCount(currentBatch);
                batches.Add(currentBatch);

                _logger.LogDebug(
                    "Finalized batch: StartAddress={Start}, Count={Count}, Sensors={SensorCount} (reason: {Reason})",
                    currentBatch.StartAddress,
                    currentBatch.RegisterCount,
                    currentBatch.Sensors.Count,
                    gap > _options.MaxGapSize ? $"gap too large ({gap})" : $"batch too large ({potentialBatchSize})");

                currentBatch = new RegisterBatch
                {
                    RegisterType = sensor.RegisterType,
                    StartAddress = sensor.RegisterAddress,
                    Sensors = new List<ModbusSensorRegister> { sensor }
                };
            }
        }

        // Add the last batch
        currentBatch.RegisterCount = CalculateBatchRegisterCount(currentBatch);
        batches.Add(currentBatch);

        return batches;
    }

    /// <summary>
    /// Calculates the total register count needed for a batch.
    /// This spans from the first sensor's address to the last sensor's end address.
    /// </summary>
    private ushort CalculateBatchRegisterCount(RegisterBatch batch)
    {
        if (batch.Sensors.Count == 0)
            return 0;

        var firstSensor = batch.Sensors.MinBy(s => s.RegisterAddress)!;
        var lastSensor = batch.Sensors.MaxBy(s => s.RegisterAddress + s.RegisterCount)!;

        var startAddress = firstSensor.RegisterAddress;
        var endAddress = lastSensor.RegisterAddress + lastSensor.RegisterCount;

        return (ushort)(endAddress - startAddress);
    }

    /// <summary>
    /// Executes a single batch read and distributes results to sensors
    /// </summary>
    private async Task ExecuteBatchAsync(
        RegisterBatch batch,
        Dictionary<string, SensorReadResult> results,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Executing batch read: StartAddress={Start}, Count={Count}, Sensors=[{Sensors}]",
            batch.StartAddress,
            batch.RegisterCount,
            string.Join(", ", batch.Sensors.Select(s => s.Name)));

        // Handle Coil/DiscreteInput reads differently from Register reads
        // FC 01 (Coil) and FC 02 (DiscreteInput) return bit-packed data
        if (batch.RegisterType is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput)
        {
            await ExecuteBitBatchAsync(batch, results, cancellationToken);
            return;
        }

        // Read all registers in this batch (FC 03/04)
        var registers = await ReadModbusRegistersAsync(
            batch.RegisterType,
            batch.StartAddress,
            batch.RegisterCount,
            cancellationToken);

        // Distribute results to individual sensors
        foreach (var sensor in batch.Sensors)
        {
            try
            {
                // Calculate offset within the batch
                var offset = sensor.RegisterAddress - batch.StartAddress;

                // Extract the registers for this sensor
                var sensorRegisters = new ushort[sensor.RegisterCount];
                Array.Copy(registers, offset, sensorRegisters, 0, sensor.RegisterCount);

                // Parse the value using protocol parser with device-level byte order
                var parser = new ModbusProtocolParser(sensor.DataType, _byteOrder);
                var parsedValue = parser.Parse(sensorRegisters);

                results[sensor.Name] = new SensorReadResult
                {
                    Success = true,
                    Value = parsedValue,
                    RawRegisters = sensorRegisters
                };

                _logger.LogTrace(
                    "Sensor {Name}: Value={Value}, Raw=[{Raw}]",
                    sensor.Name,
                    parsedValue,
                    string.Join(",", sensorRegisters));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing sensor {Name}", sensor.Name);
                results[sensor.Name] = new SensorReadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    /// <summary>
    /// Executes a batch read for bit-type registers (Coils and Discrete Inputs).
    /// FC 01: Read Coils - for reading DO (Digital Output) status
    /// FC 02: Read Discrete Inputs - for reading DI (Digital Input) status
    /// Both return bit-packed data: 8 bits per byte, LSB first.
    /// </summary>
    private async Task ExecuteBitBatchAsync(
        RegisterBatch batch,
        Dictionary<string, SensorReadResult> results,
        CancellationToken cancellationToken)
    {
        var fcName = batch.RegisterType == ModbusRegisterType.Coil ? "Coils (FC01)" : "Discrete Inputs (FC02)";
        _logger.LogDebug(
            "Executing {FcName} batch read: StartAddress={Start}, Count={Count}",
            fcName,
            batch.StartAddress,
            batch.RegisterCount);

        // Read bits using FC 01 or FC 02
        var bitStates = await ReadModbusBitsAsync(
            batch.RegisterType,
            batch.StartAddress,
            batch.RegisterCount,
            cancellationToken);

        // Distribute results to individual sensors
        foreach (var sensor in batch.Sensors)
        {
            try
            {
                // Calculate offset within the batch
                var offset = sensor.RegisterAddress - batch.StartAddress;

                // For bit registers, each sensor typically reads 1 bit (boolean)
                // But we support reading multiple consecutive bits as well
                if (sensor.RegisterCount == 1)
                {
                    // Single bit - return as boolean
                    var bitState = bitStates[offset];
                    results[sensor.Name] = new SensorReadResult
                    {
                        Success = true,
                        Value = bitState,
                        RawRegisters = [(ushort)(bitState ? 1 : 0)]
                    };

                    _logger.LogTrace("{FcName} {Name}: Value={Value}", fcName, sensor.Name, bitState);
                }
                else
                {
                    // Multiple bits - return as ushort bitmask
                    ushort bitmask = 0;
                    for (int i = 0; i < sensor.RegisterCount && (offset + i) < bitStates.Length; i++)
                    {
                        if (bitStates[offset + i])
                            bitmask |= (ushort)(1 << i);
                    }

                    results[sensor.Name] = new SensorReadResult
                    {
                        Success = true,
                        Value = bitmask,
                        RawRegisters = [bitmask]
                    };

                    _logger.LogTrace("{FcName} {Name}: Bitmask=0x{Value:X4}", fcName, sensor.Name, bitmask);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing {FcName} sensor {Name}", fcName, sensor.Name);
                results[sensor.Name] = new SensorReadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    /// <summary>
    /// Reads Modbus bit-type registers (FC 01: Read Coils, FC 02: Read Discrete Inputs)
    /// </summary>
    private async Task<bool[]> ReadModbusBitsAsync(
        ModbusRegisterType registerType,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken)
    {
        // Function code based on register type
        byte functionCode = registerType switch
        {
            ModbusRegisterType.Coil => 0x01,          // FC 01: Read Coils
            ModbusRegisterType.DiscreteInput => 0x02, // FC 02: Read Discrete Inputs
            _ => throw new NotSupportedException($"Register type {registerType} is not a bit type")
        };

        var request = BuildModbusRequest(functionCode, startAddress, count);
        var response = await _communication.RequestAsync(request, cancellationToken);
        return ParseBitResponse(response, count);
    }

    /// <summary>
    /// Parses the Modbus FC 01/02 response.
    /// Bits are packed: 8 bits per byte, LSB first.
    /// Example: Reading coils 17-18, response byte 0x03 means:
    ///   - bit 0 (coil 17) = 1 (ON)
    ///   - bit 1 (coil 18) = 1 (ON)
    /// </summary>
    private static bool[] ParseBitResponse(byte[] response, ushort expectedCount)
    {
        // Modbus TCP response format:
        // [0-1] Transaction ID
        // [2-3] Protocol ID (0x0000)
        // [4-5] Length
        // [6]   Unit ID
        // [7]   Function Code
        // [8]   Byte Count
        // [9+]  Data bytes (bit-packed)

        if (response.Length < 9)
            throw new InvalidOperationException($"Invalid Modbus bit response length: {response.Length}");

        var byteCount = response[8];
        var expectedByteCount = (expectedCount + 7) / 8; // Ceiling division: 8 bits per byte

        if (byteCount != expectedByteCount)
            throw new InvalidOperationException($"Unexpected byte count: {byteCount}, expected: {expectedByteCount}");

        var bits = new bool[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = i % 8;
            var dataByte = response[9 + byteIndex];
            bits[i] = (dataByte & (1 << bitIndex)) != 0;
        }

        return bits;
    }

    /// <summary>
    /// Reads Modbus registers (low-level communication)
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
            _ => throw new NotSupportedException($"Register type {registerType} not supported for batch reading")
        };

        var request = BuildModbusRequest(functionCode, startAddress, count);
        var response = await _communication.RequestAsync(request, cancellationToken);
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
}

/// <summary>
/// Represents a batch of sensors that can be read together
/// </summary>
public class RegisterBatch
{
    public required ModbusRegisterType RegisterType { get; set; }
    public required ushort StartAddress { get; set; }
    public ushort RegisterCount { get; set; }
    public required List<ModbusSensorRegister> Sensors { get; set; }
}

/// <summary>
/// Result of reading a single sensor
/// </summary>
public class SensorReadResult
{
    public bool Success { get; set; }
    public object? Value { get; set; }
    public ushort[]? RawRegisters { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Options for batch optimization
/// </summary>
public class ModbusBatchOptimizationOptions
{
    /// <summary>
    /// Maximum gap (in registers) allowed between sensors to still merge them into one batch.
    /// Default: 2 (if gap between sensors is less than or equal to 2 registers, merge them)
    /// </summary>
    public int MaxGapSize { get; set; } = 2;

    /// <summary>
    /// Maximum number of registers to read in a single batch.
    /// Default: 125 (Modbus protocol limit is typically 125 registers per request)
    /// </summary>
    public int MaxBatchSize { get; set; } = 125;

    public static ModbusBatchOptimizationOptions Default => new();

    /// <summary>
    /// Conservative settings: smaller batches, no gap tolerance
    /// </summary>
    public static ModbusBatchOptimizationOptions Conservative => new()
    {
        MaxGapSize = 0,
        MaxBatchSize = 50
    };

    /// <summary>
    /// Aggressive settings: larger batches, higher gap tolerance
    /// </summary>
    public static ModbusBatchOptimizationOptions Aggressive => new()
    {
        MaxGapSize = 5,
        MaxBatchSize = 125
    };
}

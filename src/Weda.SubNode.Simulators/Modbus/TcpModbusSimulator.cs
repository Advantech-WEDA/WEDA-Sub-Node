using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Weda.SubNode.Simulators.Modbus;

/// <summary>
/// Modbus TCP simulator server
/// Simulates a Modbus device with holding registers
/// </summary>
public class TcpModbusSimulator : IDisposable
{
    private readonly ILogger<TcpModbusSimulator> _logger;
    private readonly TcpModbusSimulatorConfiguration _configuration;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private Task? _simulationTask;
    private bool _disposed;

    // Holding Registers storage (address → value)
    private readonly Dictionary<ushort, ushort> _holdingRegisters = new();
    private readonly object _registerLock = new();

    // Sensor simulation state
    private readonly Dictionary<string, double> _sensorCurrentValues = new();
    private readonly Dictionary<string, string> _sensorUnits = new();

    public TcpModbusSimulator(
        TcpModbusSimulatorConfiguration configuration,
        ILogger<TcpModbusSimulator>? logger = null)
    {
        _configuration = configuration;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<TcpModbusSimulator>();

        // Initialize sensor values
        InitializeSensorValues();
    }

    private void InitializeSensorValues()
    {
        lock (_registerLock)
        {
            foreach (var sensor in _configuration.Sensors)
            {
                // Get default parameters for sensor type
                var defaults = SensorDefaults.GetDefaults(sensor.Type);
                var simParams = sensor.SimulationParams;

                // Apply defaults if not configured
                if (simParams.MinValue == 0 && simParams.MaxValue == 0)
                {
                    simParams.MinValue = defaults.MinValue;
                    simParams.MaxValue = defaults.MaxValue;
                }
                if (simParams.ChangeRate == 0) simParams.ChangeRate = defaults.ChangeRate;
                if (simParams.UpdateIntervalSeconds == 0) simParams.UpdateIntervalSeconds = defaults.UpdateIntervalSeconds;
                if (simParams.NoiseLevel == 0) simParams.NoiseLevel = defaults.NoiseLevel;

                // Set unit (use configured unit or default based on type)
                var unit = sensor.Unit ?? SensorDefaults.GetDefaultUnit(sensor.Type);
                _sensorUnits[sensor.Name] = unit;

                // Set initial value
                var initialValue = simParams.InitialValue ?? Random.Shared.NextDouble() * (simParams.MaxValue - simParams.MinValue) + simParams.MinValue;
                _sensorCurrentValues[sensor.Name] = initialValue;

                // Write to registers
                WriteSensorToRegisters(sensor, initialValue);

                _logger.LogInformation(
                    "Initialized sensor {Name} ({Type}): Address={Address}, InitialValue={Value:F2} {Unit}",
                    sensor.Name, sensor.Type, sensor.StartAddress, initialValue, unit);
            }
        }
    }

    private void WriteSensorToRegisters(SimulatedSensor sensor, double value)
    {
        var registerAddress = GetPhysicalAddress(sensor.StartAddress);
        var registerValues = ConvertValueToRegisters(value, sensor.DataType);

        for (int i = 0; i < registerValues.Length && i < sensor.RegisterCount; i++)
        {
            _holdingRegisters[(ushort)(registerAddress + i)] = registerValues[i];
        }
    }

    private ushort GetPhysicalAddress(int modbusAddress)
    {
        if (_configuration.ModbusProtocol.UseModbusAddressing)
        {
            // Map 40001-49999 to 0-9998
            return (ushort)(modbusAddress - _configuration.ModbusProtocol.HoldingRegisterBase);
        }
        return (ushort)modbusAddress;
    }

    private ushort[] ConvertValueToRegisters(double value, SimulatedDataType dataType)
    {
        return dataType switch
        {
            SimulatedDataType.UInt16 => [(ushort)value],
            SimulatedDataType.Int16 => [(ushort)(short)value],
            SimulatedDataType.Float32 => ConvertFloat32ToRegisters((float)value),
            SimulatedDataType.UInt32 => ConvertUInt32ToRegisters((uint)value),
            SimulatedDataType.Int32 => ConvertUInt32ToRegisters((uint)(int)value),
            SimulatedDataType.Float64 => ConvertFloat64ToRegisters(value),
            SimulatedDataType.UInt64 => ConvertUInt64ToRegisters((ulong)value),
            SimulatedDataType.Int64 => ConvertUInt64ToRegisters((ulong)(long)value),
            _ => [(ushort)value]
        };
    }

    private ushort[] ConvertFloat32ToRegisters(float value)
    {
        // Standard Modbus Big Endian (ABCD order)
        // BitConverter on little-endian: bytes = [0]=LSB, [1], [2], [3]=MSB
        // We need: reg[0] = AB (high word), reg[1] = CD (low word)
        var bytes = BitConverter.GetBytes(value);
        return [
            (ushort)((bytes[3] << 8) | bytes[2]),  // AB (high word: MSB first)
            (ushort)((bytes[1] << 8) | bytes[0])   // CD (low word)
        ];
    }

    private ushort[] ConvertUInt32ToRegisters(uint value)
    {
        return [
            (ushort)(value >> 16),
            (ushort)(value & 0xFFFF)
        ];
    }

    private ushort[] ConvertFloat64ToRegisters(double value)
    {
        // Standard Modbus Big Endian
        // BitConverter on little-endian: bytes = [0]=LSB ... [7]=MSB
        // We need: reg[0] = most significant word, reg[3] = least significant word
        var bytes = BitConverter.GetBytes(value);
        return [
            (ushort)((bytes[7] << 8) | bytes[6]),  // Most significant word
            (ushort)((bytes[5] << 8) | bytes[4]),
            (ushort)((bytes[3] << 8) | bytes[2]),
            (ushort)((bytes[1] << 8) | bytes[0])   // Least significant word
        ];
    }

    private ushort[] ConvertUInt64ToRegisters(ulong value)
    {
        return [
            (ushort)(value >> 48),
            (ushort)((value >> 32) & 0xFFFF),
            (ushort)((value >> 16) & 0xFFFF),
            (ushort)(value & 0xFFFF)
        ];
    }

    /// <summary>
    /// Set a holding register value
    /// </summary>
    public void SetHoldingRegister(ushort address, ushort value)
    {
        lock (_registerLock)
        {
            _holdingRegisters[address] = value;
        }
        _logger.LogDebug("Set register {Address} = {Value}", address, value);
    }

    /// <summary>
    /// Get a holding register value
    /// </summary>
    public ushort GetHoldingRegister(ushort address)
    {
        lock (_registerLock)
        {
            return _holdingRegisters.TryGetValue(address, out var value) ? value : (ushort)0;
        }
    }

    /// <summary>
    /// Start the Modbus TCP server
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener != null)
            throw new InvalidOperationException("Simulator already started");

        _logger.LogInformation("Starting Modbus TCP Simulator on {IP}:{Port} (Slave ID: {SlaveId})",
            _configuration.TcpConnection.IpAddress, _configuration.TcpConnection.Port, _configuration.ModbusProtocol.SlaveId);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Parse(_configuration.TcpConnection.IpAddress), _configuration.TcpConnection.Port);
        _listener.Start();

        _listenerTask = Task.Run(() => AcceptClientsAsync(_cts.Token), _cts.Token);
        _simulationTask = Task.Run(() => SimulateSensorValuesAsync(_cts.Token), _cts.Token);

        _logger.LogInformation("Modbus TCP Simulator started successfully");
        _logger.LogInformation("────────────────────────────────────────────────────────");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulate sensor value changes
    /// </summary>
    private async Task SimulateSensorValuesAsync(CancellationToken cancellationToken)
    {
        var sensorTasks = _configuration.Sensors.Select(sensor =>
            SimulateSingleSensorAsync(sensor, cancellationToken));

        await Task.WhenAll(sensorTasks);
    }

    private async Task SimulateSingleSensorAsync(SimulatedSensor sensor, CancellationToken cancellationToken)
    {
        var simParams = sensor.SimulationParams;

        // Use global interval if configured, otherwise use sensor-specific interval
        var updateIntervalSeconds = _configuration.Simulation.GlobalUpdateIntervalSeconds ?? simParams.UpdateIntervalSeconds;
        var updateIntervalMs = updateIntervalSeconds * 1000;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(updateIntervalMs, cancellationToken);

                // Skip value changes if disabled
                if (!_configuration.Simulation.EnableValueChanges)
                {
                    continue;
                }

                lock (_registerLock)
                {
                    var currentValue = _sensorCurrentValues[sensor.Name];

                    // Calculate change direction (random walk with boundaries)
                    var direction = Random.Shared.NextDouble() * 2 - 1; // -1 to 1
                    var change = direction * simParams.ChangeRate;

                    // Add noise
                    var noise = (Random.Shared.NextDouble() * 2 - 1) * simParams.NoiseLevel;

                    // Apply change
                    var newValue = currentValue + change + noise;

                    // Clamp to min/max
                    newValue = Math.Clamp(newValue, simParams.MinValue, simParams.MaxValue);

                    // Update state
                    _sensorCurrentValues[sensor.Name] = newValue;
                    WriteSensorToRegisters(sensor, newValue);

                    var unit = _sensorUnits.GetValueOrDefault(sensor.Name, "");
                    _logger.LogDebug("Sensor {Name} updated: {Value:F2} {Unit}", sensor.Name, newValue, unit);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating sensor {Name}", sensor.Name);
            }
        }
    }

    /// <summary>
    /// Stop the Modbus TCP server
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping Modbus TCP Simulator");

        _cts?.Cancel();
        _listener?.Stop();

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        if (_simulationTask != null)
        {
            try
            {
                await _simulationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _logger.LogInformation("Modbus TCP Simulator stopped");
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation("Client connected from {RemoteEndPoint}", client.Client.RemoteEndPoint);

                // Handle client in background
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            var buffer = new byte[260]; // Modbus TCP max frame size

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    _logger.LogInformation("Client disconnected");
                    break;
                }

                _logger.LogDebug("Received {ByteCount} bytes", bytesRead);

                // Parse and process Modbus request
                var request = buffer[..bytesRead];
                var response = ProcessModbusRequest(request);

                if (response != null)
                {
                    await stream.WriteAsync(response, cancellationToken);
                    _logger.LogDebug("Sent {ByteCount} bytes response", response.Length);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client");
        }
        finally
        {
            client.Close();
        }
    }

    private byte[]? ProcessModbusRequest(byte[] request)
    {
        try
        {
            // Validate minimum Modbus TCP frame length (MBAP Header: 7 bytes + Function Code: 1 byte)
            if (request.Length < 8)
            {
                _logger.LogWarning("Invalid request length: {Length}", request.Length);
                return null;
            }

            // Parse MBAP Header
            var transactionId = (ushort)((request[0] << 8) | request[1]);
            var protocolId = (ushort)((request[2] << 8) | request[3]);
            var length = (ushort)((request[4] << 8) | request[5]);
            var unitId = request[6];

            // Parse PDU
            var functionCode = request[7];

            _logger.LogDebug(
                "Modbus Request: TID={TransactionId}, Unit={UnitId}, FC={FunctionCode}",
                transactionId, unitId, functionCode);

            // Check slave ID
            if (unitId != _configuration.ModbusProtocol.SlaveId)
            {
                _logger.LogWarning("Invalid unit ID: {UnitId}, expected: {SlaveId}", unitId, _configuration.ModbusProtocol.SlaveId);
                return null;
            }

            // Process function code
            return functionCode switch
            {
                0x03 => ProcessReadHoldingRegisters(transactionId, unitId, request[8..]),
                0x06 => ProcessWriteSingleRegister(transactionId, unitId, request[8..]),
                0x10 => ProcessWriteMultipleRegisters(transactionId, unitId, request[8..]),
                _ => CreateExceptionResponse(transactionId, unitId, functionCode, 0x01) // Illegal function
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Modbus request");
            return null;
        }
    }

    private byte[] ProcessReadHoldingRegisters(ushort transactionId, byte unitId, byte[] pdu)
    {
        // Parse request
        var startAddress = (ushort)((pdu[0] << 8) | pdu[1]);
        var quantity = (ushort)((pdu[2] << 8) | pdu[3]);

        _logger.LogDebug("Read Holding Registers: Start={Start}, Quantity={Quantity}", startAddress, quantity);

        // Validate quantity
        if (quantity < 1 || quantity > 125)
        {
            return CreateExceptionResponse(transactionId, unitId, 0x03, 0x03); // Illegal data value
        }

        // Convert Modbus address to physical address (e.g., 40001 -> 0)
        var physicalStartAddress = GetPhysicalAddress(startAddress);

        // Read registers
        var registerValues = new List<ushort>();
        lock (_registerLock)
        {
            for (ushort i = 0; i < quantity; i++)
            {
                var address = (ushort)(physicalStartAddress + i);
                var value = _holdingRegisters.TryGetValue(address, out var v) ? v : (ushort)0;
                registerValues.Add(value);
            }
        }

        // Build response
        var byteCount = (byte)(quantity * 2);
        var response = new List<byte>
        {
            // MBAP Header
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),
            0x00, 0x00, // Protocol ID
            (byte)(0 >> 8), (byte)((3 + byteCount) & 0xFF), // Length
            unitId,

            // PDU
            0x03, // Function Code
            byteCount
        };

        // Add register values (big-endian)
        foreach (var value in registerValues)
        {
            response.Add((byte)(value >> 8));
            response.Add((byte)(value & 0xFF));
        }

        return response.ToArray();
    }

    private byte[] ProcessWriteSingleRegister(ushort transactionId, byte unitId, byte[] pdu)
    {
        var address = (ushort)((pdu[0] << 8) | pdu[1]);
        var value = (ushort)((pdu[2] << 8) | pdu[3]);

        _logger.LogDebug("Write Single Register: Address={Address}, Value={Value}", address, value);

        SetHoldingRegister(address, value);

        // Echo request as response
        return new byte[]
        {
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),
            0x00, 0x00,
            0x00, 0x06,
            unitId,
            0x06,
            pdu[0], pdu[1], pdu[2], pdu[3]
        };
    }

    private byte[] ProcessWriteMultipleRegisters(ushort transactionId, byte unitId, byte[] pdu)
    {
        var startAddress = (ushort)((pdu[0] << 8) | pdu[1]);
        var quantity = (ushort)((pdu[2] << 8) | pdu[3]);
        var byteCount = pdu[4];

        _logger.LogDebug("Write Multiple Registers: Start={Start}, Quantity={Quantity}", startAddress, quantity);

        // Write registers
        for (ushort i = 0; i < quantity; i++)
        {
            var offset = 5 + (i * 2);
            var value = (ushort)((pdu[offset] << 8) | pdu[offset + 1]);
            SetHoldingRegister((ushort)(startAddress + i), value);
        }

        // Build response
        return new byte[]
        {
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),
            0x00, 0x00,
            0x00, 0x06,
            unitId,
            0x10,
            pdu[0], pdu[1], pdu[2], pdu[3]
        };
    }

    private byte[] CreateExceptionResponse(ushort transactionId, byte unitId, byte functionCode, byte exceptionCode)
    {
        _logger.LogWarning("Modbus Exception: FC={FC}, Exception={Exception}", functionCode, exceptionCode);

        return new byte[]
        {
            (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),
            0x00, 0x00,
            0x00, 0x03,
            unitId,
            (byte)(functionCode | 0x80), // Exception function code
            exceptionCode
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

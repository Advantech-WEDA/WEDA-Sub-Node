using Microsoft.Extensions.Logging;
using Shouldly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Protocols.Modbus;
using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Modbus;

public class ModbusBatchReaderTests
{
    private readonly ILogger<ModbusBatchReaderTests> _logger;

    public ModbusBatchReaderTests()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<ModbusBatchReaderTests>();
    }

    [Fact]
    public async Task ReadSensorsAsync_ConsecutiveSensors_ShouldBatchIntoSingleRead()
    {
        // Arrange
        var mockComm = new MockModbusCommunication();
        var batchReader = new ModbusBatchReader(
            mockComm,
            slaveId: 1,
            ModbusByteOrder.BigEndian,
            _logger,
            new ModbusBatchOptimizationOptions { MaxGapSize = 2, MaxBatchSize = 125 });

        var sensors = new List<ModbusSensorRegister>
        {
            new() { Name = "Sensor1", Dtmi = "dtmi:test:1", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 0, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor2", Dtmi = "dtmi:test:2", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 1, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor3", Dtmi = "dtmi:test:3", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 2, RegisterCount = 1, DataType = ModbusDataType.UInt16 }
        };

        // Act
        var results = await batchReader.ReadSensorsAsync(sensors, CancellationToken.None);

        // Assert
        results.Count.ShouldBe(3);
        results["Sensor1"].Success.ShouldBeTrue();
        results["Sensor2"].Success.ShouldBeTrue();
        results["Sensor3"].Success.ShouldBeTrue();

        // Verify only one Modbus request was made (batch optimization)
        mockComm.RequestCount.ShouldBe(1);
        mockComm.LastStartAddress.ShouldBe((ushort)0);
        mockComm.LastRegisterCount.ShouldBe((ushort)3);

        _logger.LogInformation("✅ Batch optimization successful: 3 sensors read with 1 Modbus request");
    }

    [Fact]
    public async Task ReadSensorsAsync_SensorsWithGap_ShouldCreateMultipleBatches()
    {
        // Arrange
        var mockComm = new MockModbusCommunication();
        var batchReader = new ModbusBatchReader(
            mockComm,
            slaveId: 1,
            ModbusByteOrder.BigEndian,
            _logger,
            new ModbusBatchOptimizationOptions { MaxGapSize = 1, MaxBatchSize = 125 });

        var sensors = new List<ModbusSensorRegister>
        {
            new() { Name = "Sensor1", Dtmi = "dtmi:test:1", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 0, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor2", Dtmi = "dtmi:test:2", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 5, RegisterCount = 1, DataType = ModbusDataType.UInt16 }, // Gap = 5
            new() { Name = "Sensor3", Dtmi = "dtmi:test:3", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 6, RegisterCount = 1, DataType = ModbusDataType.UInt16 }
        };

        // Act
        var results = await batchReader.ReadSensorsAsync(sensors, CancellationToken.None);

        // Assert
        results.Count.ShouldBe(3);

        // Should create 2 batches due to large gap:
        // Batch 1: Sensor1 (address 0)
        // Batch 2: Sensor2-3 (address 5-6)
        mockComm.RequestCount.ShouldBe(2);

        _logger.LogInformation("✅ Gap handling successful: 3 sensors with gap resulted in 2 batches");
    }

    [Fact]
    public async Task ReadSensorsAsync_MixedRegisterTypes_ShouldGroupByType()
    {
        // Arrange
        var mockComm = new MockModbusCommunication();
        var batchReader = new ModbusBatchReader(
            mockComm,
            slaveId: 1,
            ModbusByteOrder.BigEndian,
            _logger,
            ModbusBatchOptimizationOptions.Default);

        var sensors = new List<ModbusSensorRegister>
        {
            new() { Name = "Holding1", Dtmi = "dtmi:test:1", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 0, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Input1", Dtmi = "dtmi:test:2", RegisterType = ModbusRegisterType.InputRegister, RegisterAddress = 0, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Holding2", Dtmi = "dtmi:test:3", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 1, RegisterCount = 1, DataType = ModbusDataType.UInt16 }
        };

        // Act
        var results = await batchReader.ReadSensorsAsync(sensors, CancellationToken.None);

        // Assert
        results.Count.ShouldBe(3);

        // Should create at least 2 batches (different register types)
        mockComm.RequestCount.ShouldBeGreaterThanOrEqualTo(2);

        _logger.LogInformation("✅ Register type grouping successful: Mixed types handled correctly");
    }

    [Fact]
    public async Task ReadSensorsAsync_Float32Sensor_ShouldParseCorrectly()
    {
        // Arrange
        var mockComm = new MockModbusCommunication();
        var batchReader = new ModbusBatchReader(
            mockComm,
            slaveId: 1,
            ModbusByteOrder.BigEndian,
            _logger,
            ModbusBatchOptimizationOptions.Default);

        var sensors = new List<ModbusSensorRegister>
        {
            new() { Name = "Temperature", Dtmi = "dtmi:test:temp", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 0, RegisterCount = 2, DataType = ModbusDataType.Float32 }
        };

        // Act
        var results = await batchReader.ReadSensorsAsync(sensors, CancellationToken.None);

        // Assert
        results.Count.ShouldBe(1);
        results["Temperature"].Success.ShouldBeTrue();
        results["Temperature"].Value.ShouldNotBeNull();
        results["Temperature"].RawRegisters.ShouldNotBeNull();
        results["Temperature"].RawRegisters!.Length.ShouldBe(2);

        _logger.LogInformation("✅ Float32 parsing successful: Value = {Value}", results["Temperature"].Value);
    }
}

/// <summary>
/// Mock Modbus communication for testing
/// </summary>
public class MockModbusCommunication : IRequestResponseCommunication<byte[], byte[]>
{
    public int RequestCount { get; private set; } = 0;
    public ushort LastStartAddress { get; private set; }
    public ushort LastRegisterCount { get; private set; }

    public Task<byte[]> RequestAsync(byte[] request, CancellationToken cancellationToken = default)
    {
        RequestCount++;

        // Parse Modbus PDU request: [SlaveId, FC, AddrHi, AddrLo, CountHi, CountLo]
        var slaveId = request[0];
        var functionCode = request[1];
        var startAddress = (ushort)((request[2] << 8) | request[3]);
        var registerCount = (ushort)((request[4] << 8) | request[5]);

        LastStartAddress = startAddress;
        LastRegisterCount = registerCount;

        // Generate mock PDU response: [SlaveId, FC, ByteCount, Data...]
        var response = new List<byte>
        {
            slaveId,                  // Slave ID
            functionCode,             // Function code
            (byte)(registerCount * 2) // Byte count
        };

        // Add register values (mock data: sequential values starting from address)
        for (int i = 0; i < registerCount; i++)
        {
            var value = (ushort)(startAddress + i + 100); // Mock value
            response.Add((byte)(value >> 8));
            response.Add((byte)(value & 0xFF));
        }

        return Task.FromResult(response.ToArray());
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public ConnectionSettings Settings => new ConnectionSettings();
    public CommunicationState State => CommunicationState.Connected;
    public bool IsConnected => true;
    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    // Suppress CS0067: Event required by interface, invoke to satisfy compiler
    protected virtual void OnStateChanged(ConnectionStateChangedEvent e) => StateChanged?.Invoke(this, e);

    public void Dispose()
    {
        // No-op for mock
    }
}

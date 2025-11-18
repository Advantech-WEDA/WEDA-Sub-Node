using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Shouldly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Protocols.Modbus;
using Xunit;
using Xunit.Abstractions;

namespace Weda.SubNode.Core.Tests.Protocols.Modbus;

public class ModbusBatchPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<ModbusBatchPerformanceTests> _logger;

    public ModbusBatchPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        _logger = loggerFactory.CreateLogger<ModbusBatchPerformanceTests>();
    }

    [Theory]
    [InlineData(5, 1)]   // 5 consecutive sensors
    [InlineData(10, 1)]  // 10 consecutive sensors
    [InlineData(20, 1)]  // 20 consecutive sensors
    [InlineData(50, 1)]  // 50 consecutive sensors
    public async Task PerformanceComparison_ConsecutiveSensors(int sensorCount, int expectedBatches)
    {
        _output.WriteLine($"\n{"=".PadRight(30, '=')} Performance Test: {sensorCount} Consecutive Sensors {"=".PadRight(30, '=')}");

        // Create test sensors with consecutive addresses
        var sensors = Enumerable.Range(0, sensorCount)
            .Select(i => new ModbusSensorRegister
            {
                Name = $"Sensor{i}",
                Dtmi = $"dtmi:test:{i}",
                RegisterType = ModbusRegisterType.HoldingRegister,
                RegisterAddress = (ushort)i,
                RegisterCount = 1,
                DataType = ModbusDataType.UInt16
            })
            .ToList();

        // Test 1: Legacy single-point reading
        var legacyComm = new PerformanceTrackingCommunication();
        var legacyStopwatch = Stopwatch.StartNew();
        await SimulateLegacyReading(sensors, legacyComm);
        legacyStopwatch.Stop();

        // Test 2: Batch reading
        var batchComm = new PerformanceTrackingCommunication();
        var batchReader = new ModbusBatchReader(
            batchComm,
            slaveId: 1,
            _logger,
            ModbusBatchOptimizationOptions.Default);
        var batchStopwatch = Stopwatch.StartNew();
        await batchReader.ReadSensorsAsync(sensors, CancellationToken.None);
        batchStopwatch.Stop();

        // Analysis
        var requestReduction = ((double)(legacyComm.RequestCount - batchComm.RequestCount) / legacyComm.RequestCount) * 100;
        var timeImprovement = ((double)(legacyStopwatch.ElapsedMilliseconds - batchStopwatch.ElapsedMilliseconds) / legacyStopwatch.ElapsedMilliseconds) * 100;

        _output.WriteLine($"Legacy Reading:");
        _output.WriteLine($"  - Requests: {legacyComm.RequestCount}");
        _output.WriteLine($"  - Time: {legacyStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"");
        _output.WriteLine($"Batch Reading:");
        _output.WriteLine($"  - Requests: {batchComm.RequestCount}");
        _output.WriteLine($"  - Time: {batchStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"");
        _output.WriteLine($"Improvements:");
        _output.WriteLine($"  - Request Reduction: {requestReduction:F1}% (from {legacyComm.RequestCount} to {batchComm.RequestCount})");
        _output.WriteLine($"  - Time Improvement: {timeImprovement:F1}%");
        _output.WriteLine($"  - Expected Batches: {expectedBatches}, Actual: {batchComm.RequestCount}");

        // Assertions
        batchComm.RequestCount.ShouldBeLessThan(legacyComm.RequestCount);
        batchComm.RequestCount.ShouldBe(expectedBatches);
        requestReduction.ShouldBeGreaterThan(50); // At least 50% reduction for consecutive sensors
    }

    [Fact]
    public async Task PerformanceComparison_SensorsWithGaps()
    {
        _output.WriteLine($"\n{"=".PadRight(30, '=')} Performance Test: Sensors with Gaps {"=".PadRight(30, '=')}");

        // Create sensors with gaps: 0, 1, 2, [gap], 10, 11, 12, [gap], 20, 21
        var sensors = new List<ModbusSensorRegister>
        {
            new() { Name = "Sensor0", Dtmi = "dtmi:test:0", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 0, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor1", Dtmi = "dtmi:test:1", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 1, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor2", Dtmi = "dtmi:test:2", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 2, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor10", Dtmi = "dtmi:test:10", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 10, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor11", Dtmi = "dtmi:test:11", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 11, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor12", Dtmi = "dtmi:test:12", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 12, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor20", Dtmi = "dtmi:test:20", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 20, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
            new() { Name = "Sensor21", Dtmi = "dtmi:test:21", RegisterType = ModbusRegisterType.HoldingRegister, RegisterAddress = 21, RegisterCount = 1, DataType = ModbusDataType.UInt16 },
        };

        // Legacy reading
        var legacyComm = new PerformanceTrackingCommunication();
        await SimulateLegacyReading(sensors, legacyComm);

        // Batch reading
        var batchComm = new PerformanceTrackingCommunication();
        var batchReader = new ModbusBatchReader(
            batchComm,
            slaveId: 1,
            _logger,
            ModbusBatchOptimizationOptions.Default);
        await batchReader.ReadSensorsAsync(sensors, CancellationToken.None);

        var requestReduction = ((double)(legacyComm.RequestCount - batchComm.RequestCount) / legacyComm.RequestCount) * 100;

        _output.WriteLine($"Legacy: {legacyComm.RequestCount} requests");
        _output.WriteLine($"Batch: {batchComm.RequestCount} requests");
        _output.WriteLine($"Reduction: {requestReduction:F1}%");

        // Should create 3 batches: [0-2], [10-12], [20-21]
        batchComm.RequestCount.ShouldBe(3);
        batchComm.RequestCount.ShouldBeLessThan(legacyComm.RequestCount);
    }

    private async Task SimulateLegacyReading(
        List<ModbusSensorRegister> sensors,
        IRequestResponseCommunication<byte[], byte[]> communication)
    {
        foreach (var sensor in sensors)
        {
            // Simulate individual read for each sensor (legacy behavior)
            var request = BuildModbusRequest(0x03, sensor.RegisterAddress, sensor.RegisterCount);
            await communication.RequestAsync(request, CancellationToken.None);
        }
    }

    private byte[] BuildModbusRequest(byte functionCode, ushort startAddress, ushort count)
    {
        return new byte[]
        {
            0x00, 0x01,  // Transaction ID
            0x00, 0x00,  // Protocol ID
            0x00, 0x06,  // Length
            0x01,        // Unit ID
            functionCode,
            (byte)(startAddress >> 8), (byte)(startAddress & 0xFF),
            (byte)(count >> 8), (byte)(count & 0xFF)
        };
    }
}

/// <summary>
/// Communication mock that tracks performance metrics
/// </summary>
public class PerformanceTrackingCommunication : IRequestResponseCommunication<byte[], byte[]>
{
    public int RequestCount { get; private set; } = 0;
    private const int SimulatedLatencyMs = 10; // Simulate 10ms network latency per request

    public async Task<byte[]> RequestAsync(byte[] request, CancellationToken cancellationToken = default)
    {
        RequestCount++;

        // Simulate network latency
        await Task.Delay(SimulatedLatencyMs, cancellationToken);

        // Parse request
        var functionCode = request[7];
        var startAddress = (ushort)((request[8] << 8) | request[9]);
        var registerCount = (ushort)((request[10] << 8) | request[11]);

        // Generate response
        var response = new List<byte>
        {
            request[0], request[1],
            0x00, 0x00,
            (byte)((registerCount * 2 + 3) >> 8), (byte)((registerCount * 2 + 3) & 0xFF),
            request[6],
            functionCode,
            (byte)(registerCount * 2)
        };

        for (int i = 0; i < registerCount; i++)
        {
            var value = (ushort)(startAddress + i + 100);
            response.Add((byte)(value >> 8));
            response.Add((byte)(value & 0xFF));
        }

        return response.ToArray();
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ConnectionSettings Settings => new ConnectionSettings();
    public CommunicationState State => CommunicationState.Connected;
    public bool IsConnected => true;
    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;
    public void Dispose() { }
}

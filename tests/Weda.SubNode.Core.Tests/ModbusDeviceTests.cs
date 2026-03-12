using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.TestBase;
using Weda.SubNode.TestBase.Builders;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// Unit tests for ModbusDevice
/// Tests telemetry reading, protocol parsing, error handling, and core functionality
/// </summary>
public class ModbusDeviceTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly IWedaCloudService _mockCloudService;
    private readonly IRequestResponseCommunication<byte[], byte[]> _mockCommunication;

    public ModbusDeviceTests()
    {
        // Use MockApplicationContext for testing
        _context = new MockApplicationContext();
        _mockCloudService = _context.MockCloudService;
        _mockCommunication = _context.MockCommunication;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Configuration Tests

    [Fact]
    public void ModbusDevice_Should_ParseSlaveIdFromConfiguration()
    {
        // Arrange
        var config = CreateModbusConfiguration(slaveId: 5);

        // Act
        var device = new ModbusDevice(_context, config, _mockCommunication);

        // Assert
        device.ShouldNotBeNull();
        device.Configuration.DeviceCommunication["SlaveId"].ShouldBe(5);
    }

    [Fact]
    public void ModbusDevice_Should_ConvertSensorsToModbusRegisters()
    {
        // Arrange
        var config = DeviceConfigurationBuilder.Default()
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502,
                ["SlaveId"] = 1
            })
            .WithSensors(new List<Sensor>
            {
                new()
                {
                    Name = "temperature",
                    ResourceId = "temp-001",
                    Dtmi = "dtmi:test:Temperature;1",
                    DeviceResourceId = "device-001",
                    Parameters = new Dictionary<string, object>
                    {
                        ["RegisterAddress"] = 0,
                        ["RegisterCount"] = 2,
                        ["RegisterType"] = "HoldingRegister",
                        ["DataType"] = "Float32"
                    }
                }
            })
            .Build();

        // Act
        var device = new ModbusDevice(_context, config, _mockCommunication);

        // Assert
        device.ShouldNotBeNull();
        device.Configuration.Sensors.Count.ShouldBe(1);
    }

    #endregion

    #region ReadTelemetryAsync Tests

    [Fact]
    public async Task ReadTelemetryAsync_Should_ReadFromModbusRegisters()
    {
        // Arrange
        var config = CreateModbusConfiguration();
        var device = new ModbusDevice(_context, config, _mockCommunication);

        // Mock Modbus PDU response for reading holding registers
        // PDU format: [SlaveId, FC, ByteCount, Data...]
        var mockResponse = new byte[]
        {
            0x01,       // Slave ID
            0x03,       // Function Code (Read Holding Registers)
            0x04,       // Byte Count = 4
            0x41, 0xC8, 0x00, 0x00  // Float32: 25.0 (IEEE 754)
        };

        _mockCommunication.RequestAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(mockResponse);

        // Act
        var measures = await device.ReadTelemetryAsync();

        // Assert
        measures.ShouldNotBeNull();
        measures.Count.ShouldBe(1);
        measures[0].ResourceId.ShouldBe("temp-001");

        // Verify communication was called
        await _mockCommunication.Received(1).RequestAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadTelemetryAsync_Should_SkipDisabledSensors()
    {
        // Arrange
        var config = DeviceConfigurationBuilder.Default()
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502,
                ["SlaveId"] = 1
            })
            .WithSensors(new List<Sensor>
            {
                new()
                {
                    Name = "temperature",
                    ResourceId = "temp-001",
                    Dtmi = "dtmi:test:Temperature;1",
                    DeviceResourceId = "device-001",
                    Parameters = new Dictionary<string, object>
                    {
                        ["RegisterAddress"] = 0,
                        ["RegisterCount"] = 2,
                        ["DataType"] = "Float32"
                    },
                    Report = new SensorReport { Enabled = false }
                }
            })
            .Build();

        var device = new ModbusDevice(_context, config, _mockCommunication);

        // Act
        var measures = await device.ReadTelemetryAsync();

        // Assert
        measures.ShouldBeEmpty();
        await _mockCommunication.DidNotReceive().RequestAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadTelemetryAsync_Should_ContinueOnSensorError()
    {
        // Arrange
        var config = CreateModbusConfigurationWithMultipleSensors();
        var device = new ModbusDevice(_context, config, _mockCommunication);

        // First sensor fails, second succeeds
        _mockCommunication.RequestAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(
                x => throw new InvalidOperationException("Read timeout"),
                x => CreateModbusResponse(25.0f));

        // Act
        var measures = await device.ReadTelemetryAsync();

        // Assert
        // Should have 1 measure from the second sensor (first failed)
        measures.ShouldNotBeNull();
        // Even though first sensor failed, the method continues
        await _mockCommunication.Received(2).RequestAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadTelemetryAsync_Should_RaiseDataReceivedEvent()
    {
        // Arrange
        var config = CreateModbusConfiguration();
        var device = new ModbusDevice(_context, config, _mockCommunication);
        device.EnableDataReceivedTracking = true; // Enable tracking to receive events

        var dataReceivedEventRaised = false;
        device.DataReceived += (sender, args) =>
        {
            dataReceivedEventRaised = true;
        };

        _mockCommunication.RequestAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(CreateModbusResponse(25.0f));

        // Act
        await device.ReadTelemetryAsync();

        // Assert
        dataReceivedEventRaised.ShouldBeTrue();
    }

    #endregion

    #region Modbus Protocol Tests

    [Fact]
    public async Task BuildModbusRequest_Should_GenerateCorrectFormat()
    {
        // Arrange
        var config = CreateModbusConfiguration();
        var device = new ModbusDevice(_context, config, _mockCommunication);

        byte[]? capturedRequest = null;
        _mockCommunication.RequestAsync(Arg.Do<byte[]>(x => capturedRequest = x), Arg.Any<CancellationToken>())
            .Returns(CreateModbusResponse(25.0f));

        // Act
        await device.ReadTelemetryAsync();

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Length.ShouldBe(6); // Pure PDU: SlaveId(1) + FC(1) + Addr(2) + Count(2)

        // Check PDU format: [SlaveId, FC, AddrHi, AddrLo, CountHi, CountLo]
        capturedRequest[0].ShouldBe((byte)0x01); // Slave ID
        capturedRequest[1].ShouldBe((byte)0x03); // Function Code (Read Holding Registers)
    }

    #endregion

    #region GetHealthAsync Tests

    [Fact]
    public async Task GetHealthAsync_Should_ReturnHealthy_When_Connected()
    {
        // Arrange
        var config = CreateModbusConfiguration();
        var device = new ModbusDevice(_context, config, _mockCommunication);

        _mockCommunication.State.Returns(CommunicationState.Connected);

        // Act
        var health = await device.GetHealthAsync();

        // Assert
        health.ShouldNotBeNull();
        health.IsHealthy.ShouldBeTrue();
        health.DeviceId.ShouldBe(config.DeviceId);
    }

    [Fact]
    public async Task GetHealthAsync_Should_ReturnUnhealthy_When_Disconnected()
    {
        // Arrange
        var config = CreateModbusConfiguration();
        var device = new ModbusDevice(_context, config, _mockCommunication);

        _mockCommunication.State.Returns(CommunicationState.Disconnected);

        // Act
        var health = await device.GetHealthAsync();

        // Assert
        health.ShouldNotBeNull();
        health.IsHealthy.ShouldBeFalse();
    }

    #endregion

    #region Helper Methods

    private DeviceConfiguration CreateModbusConfiguration(int slaveId = 1)
    {
        return DeviceConfigurationBuilder.Default()
            .WithDeviceId("modbus-device-001")
            .WithDeviceName("Test Modbus Device")
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502,
                ["SlaveId"] = slaveId
            })
            .WithSensors(new List<Sensor>
            {
                new()
                {
                    Name = "temperature",
                    ResourceId = "temp-001",
                    Dtmi = "dtmi:test:Temperature;1",
                    DeviceResourceId = "device-001",
                    Parameters = new Dictionary<string, object>
                    {
                        ["RegisterAddress"] = 0,
                        ["RegisterCount"] = 2,
                        ["RegisterType"] = "HoldingRegister",
                        ["DataType"] = "Float32"
                    }
                }
            })
            .Build();
    }

    private DeviceConfiguration CreateModbusConfigurationWithMultipleSensors()
    {
        return DeviceConfigurationBuilder.Default()
            .WithDeviceId("modbus-device-001")
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502,
                ["SlaveId"] = 1
            })
            .WithSensors(new List<Sensor>
            {
                new()
                {
                    Name = "temperature",
                    ResourceId = "temp-001",
                    Dtmi = "dtmi:test:Temperature;1",
                    DeviceResourceId = "device-001",
                    Parameters = new Dictionary<string, object>
                    {
                        ["RegisterAddress"] = 0,
                        ["RegisterCount"] = 2,
                        ["DataType"] = "Float32"
                    }
                },
                new()
                {
                    Name = "humidity",
                    ResourceId = "humidity-001",
                    Dtmi = "dtmi:test:Humidity;1",
                    DeviceResourceId = "device-001",
                    Parameters = new Dictionary<string, object>
                    {
                        ["RegisterAddress"] = 10,
                        ["RegisterCount"] = 2,
                        ["DataType"] = "Float32"
                    }
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a Modbus PDU response (no MBAP header).
    /// PDU format: [SlaveId, FC, ByteCount, Data...]
    /// </summary>
    private static byte[] CreateModbusResponse(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return
        [
            0x01,       // Slave ID
            0x03,       // Function Code
            0x04,       // Byte Count
            bytes[0], bytes[1], bytes[2], bytes[3] // Float32 data
        ];
    }

    private DeviceConfiguration CreateModbusConfigurationWithDigitalOutput()
    {
        return DeviceConfigurationBuilder.Default()
            .WithDeviceId("modbus-device-001")
            .WithDeviceName("Test Modbus Device with DO")
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502,
                ["SlaveId"] = 1
            })
            .WithSensors(new List<Sensor>
            {
                new()
                {
                    Name = "do0",
                    ResourceId = "do0-001",
                    Dtmi = "dtmi:advantech:EdgeSync:DigitalOutput;1",
                    DeviceResourceId = "device-001",
                    SensorGroup = SensorGroup.DO,
                    Parameters = new Dictionary<string, object>
                    {
                        ["RegisterAddress"] = 0,
                        ["RegisterType"] = "Coil",
                        ["DataType"] = "UInt16"  // Coil uses UInt16 for representation
                    }
                },
                new()
                {
                    Name = "do1",
                    ResourceId = "do1-001",
                    Dtmi = "dtmi:advantech:EdgeSync:DigitalOutput;1",
                    DeviceResourceId = "device-001",
                    SensorGroup = SensorGroup.DO,
                    Parameters = new Dictionary<string, object>
                    {
                        ["RegisterAddress"] = 1,
                        ["RegisterType"] = "Coil",
                        ["DataType"] = "UInt16"  // Coil uses UInt16 for representation
                    }
                }
            })
            .Build();
    }

    /// <summary>
    /// Create Modbus FC 05 (Write Single Coil) response
    /// FC 05 echoes the request on success
    /// </summary>
    private byte[] CreateWriteCoilResponse(ushort address, bool state)
    {
        var value = state ? (ushort)0xFF00 : (ushort)0x0000;

        return new byte[]
        {
            0x00, 0x01, // Transaction ID
            0x00, 0x00, // Protocol ID
            0x00, 0x06, // Length (6 bytes follow)
            0x01,       // Unit ID (Slave ID)
            0x05,       // Function Code (Write Single Coil)
            (byte)(address >> 8), (byte)(address & 0xFF),  // Coil Address
            (byte)(value >> 8), (byte)(value & 0xFF)       // Value (0xFF00 or 0x0000)
        };
    }

    #endregion
}

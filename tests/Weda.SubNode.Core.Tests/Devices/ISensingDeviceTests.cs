using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Mqtt;
using Weda.SubNode.Core.Protocols.ISensing;
using Weda.SubNode.TestBase;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

public class ISensingDeviceTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly DeviceConfiguration _configuration;
    private readonly MqttCommunication _mqtt;
    private readonly ISensingDevice _device;

    public ISensingDeviceTests()
    {
        _context = new MockApplicationContext();

        _configuration = new DeviceConfiguration
        {
            DeviceId = "test-mqtt-device",
            DeviceName = "Test MQTT ISensing Device",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "Test",
                Manufacturer = "Advantech",
                Model = "WISE-4012SE",
                SwVersion = "1.0",
                SubNodeType = SubNodeType.CustomDevice
            },
            Dtdl = new DtdlConfig
            {
                AutoGenEnabled = false,
                DtdlPath = "tests/Weda.SubNode.TestBase/Fixtures/test-device.dtdl.json"
            },
            DeviceCommunication = new Dictionary<string, object>
            {
                ["BrokerUrl"] = "mqtt://localhost:1883",
                ["ClientId"] = "test-client",
                ["MacAddress"] = "00D0C9FAC80E",
                ["Manufacturer"] = "Advantech"
            },
            Sensors = new List<Sensor>
            {
                new()
                {
                    Name = "AI0",
                    ResourceId = "ai0",
                    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
                    DeviceResourceId = "test-mqtt-device",
                    SensorGroup = SensorGroup.AI,
                    Report = new SensorReport
                    {
                        Enabled = true,
                        Unit = "mA"
                    },
                    SensorInfo = new SensorInfo { Schema = "double" }
                },
                new()
                {
                    Name = "do1",
                    ResourceId = "do1",
                    Dtmi = "dtmi:advantech:EdgeSync:DO;1",
                    DeviceResourceId = "test-mqtt-device",
                    SensorGroup = SensorGroup.DO,
                    Report = new SensorReport { Enabled = true },
                    SensorInfo = new SensorInfo { Schema = "boolean" }
                }
            }
        };

        _mqtt = new MqttCommunication("localhost", 1883, "test-client", null, NullLogger<CommunicationBase>.Instance);
        _device = new ISensingDevice(_context, _configuration, _mqtt);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Assert
        Assert.NotNull(_device);
        Assert.Equal("test-mqtt-device", _device.SubNodeId);
        Assert.Equal("Test MQTT ISensing Device", _device.DeviceName);
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ISensingDevice(_context, null!, _mqtt));
    }

    [Fact]
    public void Constructor_WithNullPubSub_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ISensingDevice(_context, _configuration, null!));
    }

    [Fact]
    public void Constructor_WithoutMacAddress_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidConfig = new DeviceConfiguration
        {
            DeviceName = "Test",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "Test",
                Manufacturer = "Advantech",
                Model = "Test",
                SwVersion = "1.0",
                SubNodeType = SubNodeType.CustomDevice
            },
            Dtdl = new DtdlConfig
            {
                AutoGenEnabled = false,
                DtdlPath = "tests/Weda.SubNode.TestBase/Fixtures/test-device.dtdl.json"
            },
            DeviceCommunication = new Dictionary<string, object>()
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new ISensingDevice(_context, invalidConfig, _mqtt));
    }

    [Fact]
    public async Task ReadTelemetryAsync_ShouldReturnCachedData()
    {
        // ISensingDevice now returns cached data from MQTT messages
        // When no data is received yet, it should return an empty list

        // Act
        var result = await _device.ReadTelemetryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // No data received yet
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Act
        _device.Dispose();

        // Assert - no exception should be thrown
        Assert.True(true);
    }

    public void Dispose()
    {
        _device?.Dispose();
        _mqtt?.Dispose();
        _context?.Dispose();
    }
}

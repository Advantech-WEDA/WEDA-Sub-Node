using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Mqtt;
using Weda.SubNode.Core.Devices;
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
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "WISE-4012SE",
                SubNodeSwVersion = "1.0",
                DeviceInfo = new Dictionary<string, object>()
            },
            Communication = new Dictionary<string, object>
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
                    Config = new SensorConfig
                    {
                        Enabled = true,
                        Unit = "mA"
                    }
                },
                new()
                {
                    Name = "do1",
                    ResourceId = "do1",
                    Dtmi = "dtmi:advantech:EdgeSync:DO;1",
                    DeviceResourceId = "test-mqtt-device",
                    SensorGroup = SensorGroup.DO,
                    Config = new SensorConfig { Enabled = true }
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
        Assert.Equal("test-mqtt-device", _device.DeviceId);
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
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "Test",
                SubNodeSwVersion = "1.0",
                DeviceInfo = new Dictionary<string, object>()
            },
            Communication = new Dictionary<string, object>()
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
    public async Task ExecuteCommandAsync_ShouldReturnTrue()
    {
        // ISensingDevice ExecuteCommandAsync validates sensor exists, encodes command, and publishes to MQTT

        // Arrange
        var command = new DeviceCommand
        {
            DeviceCmd = "SetDigitalOutput",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = "do1",  // Sensor name must exist in configuration
                ["state"] = true
            }
        };

        // Act
        var result = await _device.ExecuteCommandAsync(command);

        // Assert
        Assert.True(result); // Should succeed if sensor exists and validation passes
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

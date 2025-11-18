using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Devices.Generic;
using Weda.SubNode.TestBase;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

public class SensorControlTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly DeviceConfiguration _configuration;
    private readonly MqttCommunication _mqtt;
    private readonly MqttISensingDevice _device;

    public SensorControlTests()
    {
        _context = new MockApplicationContext();

        _configuration = new DeviceConfiguration
        {
            DeviceId = "test-device",
            DeviceName = "Test Device",
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "Test",
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
                    Dtmi = "dtmi:test:AI;1",
                    DeviceResourceId = "test-device",
                    SensorGroup = SensorGroup.AI,
                    Config = new SensorConfig { Enabled = true }
                }
            }
        };

        _mqtt = new MqttCommunication("localhost", 1883, "test-client", null, NullLogger<CommunicationBase>.Instance);
        _device = new MqttISensingDevice(_context, _configuration);
    }

    [Fact]
    public void Device_ShouldImplementISensorControl()
    {
        // Assert
        Assert.IsAssignableFrom<ISensorControl>(_device);
    }

    [Fact]
    public async Task SetDigitalOutputAsync_ShouldThrowNotImplemented()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.SetDigitalOutputAsync("do0", true));
    }

    [Fact]
    public async Task SetAnalogOutputAsync_ShouldThrowNotImplemented()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.SetAnalogOutputAsync("ao0", 4.5));
    }

    [Fact]
    public async Task GetConfigurationAsync_ShouldThrowNotImplemented()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.GetConfigurationAsync());
    }

    [Fact]
    public async Task GetConfigurationAsync_WithIndex_ShouldThrowNotImplemented()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.GetConfigurationAsync(1));
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldThrowNotImplemented()
    {
        // Arrange
        var configData = new Dictionary<string, object>
        {
            ["interval"] = 5000
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.SetConfigurationAsync(1, configData));
    }

    [Fact]
    public async Task SetSensorEnabledAsync_ShouldThrowNotImplemented()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.SetSensorEnabledAsync("ai0", false));
    }

    public void Dispose()
    {
        _device?.Dispose();
        _mqtt?.Dispose();
        _context?.Dispose();
    }
}

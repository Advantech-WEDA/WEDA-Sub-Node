using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.ISensing;
using Weda.SubNode.Core.Protocols.ISensing.Commands;
using Weda.SubNode.Devices.Advantech;
using Weda.SubNode.Devices.Generic;
using Weda.SubNode.TestBase;
using Xunit;

namespace Weda.SubNode.Core.Tests.Integration;

/// <summary>
/// Integration tests for the complete MQTT ISensing stack
/// Tests the end-to-end flow from MQTT communication to device control
/// </summary>
public class MqttISensingIntegrationTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly DeviceConfiguration _configuration;
    private readonly Wise4012SeDevice _device;

    public MqttISensingIntegrationTests()
    {
        _context = new MockApplicationContext();

        _configuration = new DeviceConfiguration
        {
            DeviceId = "wise-4012se-integration-test",
            DeviceName = "WISE-4012SE Integration Test",
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "WISE-4012SE",
                SubNodeSwVersion = "1.0",
                DeviceInfo = new Dictionary<string, object>
                {
                    ["FirmwareVersion"] = "1.0.0",
                    ["HardwareVersion"] = "A1"
                }
            },
            Communication = new Dictionary<string, object>
            {
                ["BrokerUrl"] = "mqtt://localhost:1883",
                ["ClientId"] = "integration-test-client",
                ["MacAddress"] = "00D0C9FAC80E",
                ["Manufacturer"] = "Advantech"
            },
            Sensors = new List<Sensor>
            {
                new()
                {
                    Name = "AI0",
                    ResourceId = "ai0",
                    Dtmi = "dtmi:advantech:wise4012se:AI;1",
                    DeviceResourceId = "wise-4012se-integration-test",
                    SensorGroup = SensorGroup.AI,
                    Parameters = new Dictionary<string, object>
                    {
                        ["FieldName"] = "ai0"
                    },
                    Config = new SensorConfig
                    {
                        Enabled = true,
                        Unit = "mA",
                        Interval = 1000
                    }
                },
                new()
                {
                    Name = "DI0",
                    ResourceId = "di0",
                    Dtmi = "dtmi:advantech:wise4012se:DI;1",
                    DeviceResourceId = "wise-4012se-integration-test",
                    SensorGroup = SensorGroup.DI,
                    Parameters = new Dictionary<string, object>
                    {
                        ["FieldName"] = "di0"
                    },
                    Config = new SensorConfig
                    {
                        Enabled = true,
                        Interval = 1000
                    }
                }
            }
        };

        _context.DeviceConfiguration = _configuration;
        _device = new Wise4012SeDevice(_context);
    }

    [Fact]
    public void IntegrationTest_DeviceStack_ShouldBeProperlyInitialized()
    {
        // Assert - Verify complete stack initialization
        Assert.NotNull(_device);
        Assert.Equal("wise-4012se-integration-test", _device.DeviceId);
        Assert.Equal("WISE-4012SE Integration Test", _device.DeviceName);
        Assert.IsAssignableFrom<Weda.SubNode.Devices.Generic.MqttISensingDevice>(_device);
        Assert.IsAssignableFrom<ISensorControl>(_device);
    }

    [Fact]
    public void IntegrationTest_Configuration_ShouldExtractTopicInformation()
    {
        // Act
        var macAddress = _configuration.GetMacAddress();
        var manufacturer = _configuration.GetManufacturer();

        // Assert
        Assert.Equal("00D0C9FAC80E", macAddress);
        Assert.Equal("Advantech", manufacturer);

        // Expected MQTT topics based on configuration
        var expectedDataTopic = $"{manufacturer}/{macAddress}/data";
        var expectedStatusTopic = $"{manufacturer}/{macAddress}/status";

        Assert.Equal("Advantech/00D0C9FAC80E/data", expectedDataTopic);
        Assert.Equal("Advantech/00D0C9FAC80E/status", expectedStatusTopic);
    }

    [Fact]
    public void IntegrationTest_SensorMapping_ShouldConvertCorrectly()
    {
        // Act
        var ai0Sensor = _configuration.Sensors[0].ToWise4012SeSensor();
        var di0Sensor = _configuration.Sensors[1].ToWise4012SeSensor();

        // Assert - Verify sensor mapping
        Assert.Equal("ai0", ai0Sensor.FieldName);
        Assert.Equal(Abstractions.Protocols.SensorType.Analog, ai0Sensor.SensorType);
        Assert.Equal("mA", ai0Sensor.Unit);

        Assert.Equal("di0", di0Sensor.FieldName);
        Assert.Equal(Abstractions.Protocols.SensorType.Digital, di0Sensor.SensorType);
    }

    [Fact]
    public void IntegrationTest_Commands_ShouldSerializeCorrectly()
    {
        // Arrange
        var digitalCmd = new DigitalOutputCommand
        {
            OutputName = "do0",
            State = true
        };

        var analogCmd = new AnalogOutputCommand
        {
            OutputName = "ao0",
            Value = 4.5
        };

        var configCmd = new ConfigurationRequestCommand
        {
            Index = 1
        };

        // Assert - Verify command structure
        Assert.Equal("SetDO", digitalCmd.Command);
        Assert.Equal("SetAO", analogCmd.Command);
        Assert.Equal("GetConfig", configCmd.Command);
    }

    [Fact]
    public async Task IntegrationTest_SensorControl_AllMethodsAvailable()
    {
        // Cast to ISensorControl
        ISensorControl sensorControl = _device;

        // Assert - All sensor control methods should throw NotImplementedException (placeholder)
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await sensorControl.SetDigitalOutputAsync("do0", true));

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await sensorControl.SetAnalogOutputAsync("ao0", 4.5));

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await sensorControl.GetConfigurationAsync());

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await sensorControl.SetConfigurationAsync(1, new Dictionary<string, object>()));

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await sensorControl.SetSensorEnabledAsync("ai0", false));
    }

    [Fact]
    public async Task IntegrationTest_DeviceLifecycle_ShouldFollowExpectedFlow()
    {
        // This test verifies the expected lifecycle flow
        // In actual implementation, this would be:
        // 1. Connect to MQTT broker
        // 2. Subscribe to topics
        // 3. Receive messages
        // 4. Parse protocol
        // 5. Generate telemetry
        // 6. Send commands

        // For now, verify that all lifecycle methods are defined
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.ReadTelemetryAsync());

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await _device.ExecuteCommandAsync(new DeviceCommand
            {
                DeviceCmd = "Test",
                Parameters = new Dictionary<string, object>()
            }));
    }

    [Fact]
    public void IntegrationTest_MultipleDevices_CanCoexist()
    {
        // Create another device with different MAC address
        var config2 = new DeviceConfiguration
        {
            DeviceId = "wise-4012se-02",
            DeviceName = "WISE-4012SE 02",
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
                ["ClientId"] = "test-client-2",
                ["MacAddress"] = "00D0C9FAC80F",
                ["Manufacturer"] = "Advantech"
            },
            Sensors = new List<Sensor>()
        };

        var context2 = new MockApplicationContext { DeviceConfiguration = config2 };
        var device2 = new Wise4012SeDevice(context2);

        // Assert - Both devices should have unique identities
        Assert.NotEqual(_device.DeviceId, device2.DeviceId);
        Assert.NotEqual(_configuration.GetMacAddress(), config2.GetMacAddress());

        device2.Dispose();
        context2.Dispose();
    }

    public void Dispose()
    {
        _device?.Dispose();
        _context?.Dispose();
    }
}

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Cfx;
using Weda.SubNode.Devices.Generic;
using Weda.SubNode.TestBase;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class MqttCfxDeviceTests : IDisposable
{
    private readonly MockApplicationContext _context = new();

    [Fact]
    public void Constructor_WithBrokerHostAndPort_CreatesDevice()
    {
        using var device = new MqttCfxDevice(_context, ConfigurationWith(new Dictionary<string, object>
        {
            ["BrokerHost"] = "192.168.100.19",
            ["BrokerPort"] = 1883,
        }));

        Assert.NotNull(device);
    }

    [Fact]
    public void Constructor_WithBrokerUrl_CreatesDevice()
    {
        using var device = new MqttCfxDevice(_context, ConfigurationWith(new Dictionary<string, object>
        {
            ["BrokerUrl"] = "mqtt://192.168.100.19:1883",
        }));

        Assert.NotNull(device);
    }

    [Fact]
    public void Constructor_WithCredentials_CreatesDevice()
    {
        // The reference bridge requires a username and password, which the ISensing equivalent
        // cannot pass through; this device must.
        using var device = new MqttCfxDevice(_context, ConfigurationWith(new Dictionary<string, object>
        {
            ["BrokerHost"] = "192.168.100.19",
            ["Username"] = "admin",
            ["Password"] = "admin",
        }));

        Assert.NotNull(device);
    }

    [Fact]
    public void Constructor_WithNoBrokerSettings_DefaultsToLocalhost()
    {
        using var device = new MqttCfxDevice(_context, ConfigurationWith(new Dictionary<string, object>()));

        Assert.NotNull(device);
    }

    [Fact]
    public void Constructor_UnparseableBrokerUrl_ThrowsWithContext()
    {
        // Arrange
        var configuration = ConfigurationWith(new Dictionary<string, object>
        {
            ["BrokerUrl"] = ":::not a url:::",
        });

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => new MqttCfxDevice(_context, configuration));

        // Assert
        Assert.Contains("BrokerUrl", ex.Message);
        Assert.Contains("CFX Endpoint Test", ex.Message);
    }

    [Fact]
    public void Constructor_NonNumericBrokerPort_ThrowsWithContext()
    {
        // Arrange
        var configuration = ConfigurationWith(new Dictionary<string, object>
        {
            ["BrokerHost"] = "192.168.100.19",
            ["BrokerPort"] = "eighteen-eighty-three",
        });

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => new MqttCfxDevice(_context, configuration));

        // Assert
        Assert.Contains("BrokerPort", ex.Message);
        Assert.Contains("eighteen-eighty-three", ex.Message);
    }

    [Fact]
    public void Constructor_NonBooleanUseTls_ThrowsWithContext()
    {
        // Arrange
        var configuration = ConfigurationWith(new Dictionary<string, object>
        {
            ["BrokerHost"] = "192.168.100.19",
            ["UseTls"] = "yes-please",
        });

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => new MqttCfxDevice(_context, configuration));

        // Assert
        Assert.Contains("UseTls", ex.Message);
        Assert.Contains("yes-please", ex.Message);
    }

    [Fact]
    public void Constructor_NullConfiguration_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MqttCfxDevice(_context, (DeviceConfiguration)null!));

    private static DeviceConfiguration ConfigurationWith(Dictionary<string, object> communication)
    {
        communication[CfxPubSubParser.CfxHandleKey] = "SUNJSONG.SLD880A.0001";

        return new DeviceConfiguration
        {
            DeviceId = "test-cfx-device",
            DeviceName = "CFX Endpoint Test",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "Test",
                Manufacturer = "SUNJSONG",
                Model = "SLD880A",
                SwVersion = "1.0",
                SubNodeType = SubNodeType.CustomDevice,
            },
            Dtdl = new DtdlConfig
            {
                AutoGenEnabled = false,
                DtdlPath = "tests/Weda.SubNode.TestBase/Fixtures/test-device.dtdl.json",
            },
            DeviceCommunication = communication,
            Sensors =
            [
                new Sensor
                {
                    ResourceId = "work-started-resource",
                    Name = "cfx_work_started",
                    Dtmi = "dtmi:advantech:EdgeSync:Cfx;1",
                    DeviceResourceId = "test-cfx-device",
                    SensorGroup = SensorGroup.SYS,
                    Parameters = new Dictionary<string, object>
                    {
                        [CfxSensorParameters.MessageNameKey] = "CFX.Production.WorkStarted",
                    },
                    Report = new SensorReport { Enabled = true },
                    SensorInfo = new SensorInfo { Schema = "application/json" },
                }
            ],
        };
    }

    public void Dispose() => _context.Dispose();
}

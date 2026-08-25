using Microsoft.Extensions.Logging.Abstractions;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Mqtt;
using Weda.SubNode.Core.Protocols.Cfx;
using Weda.SubNode.TestBase;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxDeviceTests : IDisposable
{
    private const string MessageName = "CFX.ResourcePerformance.StationStateChanged";

    private readonly MockApplicationContext _context;
    private readonly DeviceConfiguration _configuration;
    private readonly MqttCommunication _mqtt;
    private readonly CfxDevice _device;

    public CfxDeviceTests()
    {
        _context = new MockApplicationContext();
        _configuration = BuildConfiguration();
        _mqtt = new MqttCommunication(
            "192.168.100.19",
            1883,
            "wiseiot-yujietest-sub-001",
            null,
            NullLogger<CommunicationBase>.Instance);

        _device = new CfxDevice(_context, _configuration, _mqtt);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesDevice()
    {
        Assert.NotNull(_device);
        Assert.Equal("test-cfx-device", _device.SubNodeId);
        Assert.Equal("SUNJSONG SLD880A", _device.DeviceName);
    }

    [Fact]
    public void Constructor_NullConfiguration_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new CfxDevice(_context, null!, _mqtt));

    [Fact]
    public void Constructor_NullPubSub_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new CfxDevice(_context, _configuration, null!));

    [Fact]
    public void Constructor_SensorMissingMessageName_ThrowsAtStartup()
    {
        // Arrange
        var configuration = BuildConfiguration();
        configuration.Sensors[0].Parameters.Clear();

        // Act & Assert — a mis-configured sensor must fail construction, not silently never report.
        Assert.Throws<InvalidOperationException>(() => new CfxDevice(_context, configuration, _mqtt));
    }

    [Fact]
    public async Task ReadTelemetryAsync_BeforeAnyMessage_ReturnsEmpty()
    {
        // CFX is push-based, so the cache is empty until an endpoint publishes.
        var measures = await _device.ReadTelemetryAsync();

        Assert.Empty(measures);
    }

    private static DeviceConfiguration BuildConfiguration() => new()
    {
        DeviceId = "test-cfx-device",
        DeviceName = "SUNJSONG SLD880A",
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
        DeviceCommunication = new Dictionary<string, object>
        {
            ["BrokerHost"] = "192.168.100.19",
            ["BrokerPort"] = 1883,
            [CfxPubSubParser.CfxHandleKey] = "SUNJSONG.SLD880A.0001",
        },
        Sensors =
        [
            new Sensor
            {
                ResourceId = "station-state-resource",
                Name = "cfx_station_state_changed",
                Dtmi = "dtmi:advantech:EdgeSync:Cfx;1",
                DeviceResourceId = "test-cfx-device",
                SensorGroup = SensorGroup.SYS,
                Parameters = new Dictionary<string, object>
                {
                    [CfxSensorParameters.MessageNameKey] = MessageName,
                },
                Report = new SensorReport { Enabled = true },
                SensorInfo = new SensorInfo { Schema = "application/json" },
            }
        ],
    };

    public void Dispose()
    {
        _device.Dispose();
        _mqtt.Dispose();
        _context.Dispose();
    }
}

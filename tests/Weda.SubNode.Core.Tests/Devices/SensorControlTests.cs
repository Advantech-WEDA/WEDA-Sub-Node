using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Mqtt;
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
                    Dtmi = "dtmi:test:AI;1",
                    DeviceResourceId = "test-device",
                    SensorGroup = SensorGroup.AI,
                    Report = new SensorReport { Enabled = true }
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

    public void Dispose()
    {
        _device?.Dispose();
        _mqtt?.Dispose();
        _context?.Dispose();
    }
}

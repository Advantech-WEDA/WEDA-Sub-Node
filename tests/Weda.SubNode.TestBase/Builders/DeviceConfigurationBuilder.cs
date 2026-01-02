using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.TestBase.Builders;

/// <summary>
/// Test Data Builder for DeviceConfiguration
/// Provides fluent API to create test configurations
/// </summary>
public class DeviceConfigurationBuilder
{
    private bool _enabled = true;
    private string _deviceId = "test-device-001";
    private string _deviceName = "Test Device";
    private DtdlConfig _dtdl = new();
    private List<Sensor> _sensors = new()
    {
        new Sensor
        {
            ResourceId = "test-resource-001",
            Name = "temperature",
            Dtmi = "dtmi:test:Temperature;1",
            SensorGroup = SensorGroup.AI,
            DeviceResourceId = "test-device-001",
            Parameters = new Dictionary<string, object>
            {
                ["RegisterAddress"] = 0,
                ["RegisterCount"] = 2,
                ["DataType"] = "Float32"
            },
            Config = new SensorConfig
            {
                Enabled = true,
                Interval = 1000
            }
        }
    };
    private Dictionary<string, object> _deviceCommunication = new()
    {
        ["Host"] = "127.0.0.1",
        ["Port"] = 502,
        ["SlaveId"] = 1
    };
    private BackgroundTaskPeriods _periods = new()
    {
        ReportHealth = 60000,
        PollCommands = 1000
    };
    private Dictionary<string, object> _properties = new();
    private SubNodeInfo _subNodeInfo = new()
    {
        Name = "TestSubNode",
        Manufacturer = "Test Corp",
        Model = "TEST-001",
        SwVersion = "1.0.0",
        SubNodeType = SubNodeType.AdamEthernet
    };

    public DeviceConfigurationBuilder WithEnabled(bool enabled)
    {
        _enabled = enabled;
        return this;
    }

    public DeviceConfigurationBuilder WithDeviceId(string deviceId)
    {
        _deviceId = deviceId;
        return this;
    }

    public DeviceConfigurationBuilder WithDeviceName(string deviceName)
    {
        _deviceName = deviceName;
        return this;
    }

    public DeviceConfigurationBuilder WithSubNodeInfo(SubNodeInfo subNodeInfo)
    {
        _subNodeInfo = subNodeInfo;
        return this;
    }

    public DeviceConfigurationBuilder WithSubNodeType(SubNodeType deviceType)
    {
        _subNodeInfo.SubNodeType = deviceType;
        return this;
    }

    public DeviceConfigurationBuilder WithDtdl(DtdlConfig dtdl)
    {
        _dtdl = dtdl;
        return this;
    }

    public DeviceConfigurationBuilder AddSensor(Sensor sensor)
    {
        _sensors.Add(sensor);
        return this;
    }

    public DeviceConfigurationBuilder WithSensors(List<Sensor> sensors)
    {
        _sensors = sensors;
        return this;
    }

    public DeviceConfigurationBuilder WithDeviceCommunication(Dictionary<string, object> communication)
    {
        _deviceCommunication = communication;
        return this;
    }

    public DeviceConfigurationBuilder WithPeriods(BackgroundTaskPeriods periods)
    {
        _periods = periods;
        return this;
    }

    public DeviceConfigurationBuilder WithProperties(Dictionary<string, object> properties)
    {
        _properties = properties;
        return this;
    }

    public DeviceConfiguration Build()
    {
        return new DeviceConfiguration
        {
            Enabled = _enabled,
            DeviceId = _deviceId,
            DeviceName = _deviceName,
            SubNodeInfo = _subNodeInfo,
            Dtdl = _dtdl,
            Sensors = _sensors,
            DeviceCommunication = _deviceCommunication,
            Periods = _periods,
            Properties = _properties
        };
    }

    public static DeviceConfigurationBuilder Default() => new();
}

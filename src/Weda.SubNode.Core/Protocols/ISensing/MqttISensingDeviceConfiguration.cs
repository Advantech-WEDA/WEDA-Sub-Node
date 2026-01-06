using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Protocols.ISensing;

/// <summary>
/// Strongly-typed configuration for MQTT ISensing devices (e.g., WISE-4012SE).
/// Provides IntelliSense-friendly programmatic configuration without needing to consult documentation.
/// </summary>
public class MqttISensingDeviceConfiguration : IDeviceConfiguration
{
    /// <summary>
    /// Device ID (optional, will be auto-generated if not provided)
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Device name (required)
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// Manufacturer name (default: "Advantech")
    /// </summary>
    public string Manufacturer { get; set; } = "Advantech";

    /// <summary>
    /// Device model (default: "WISE-4012SE")
    /// </summary>
    public string Model { get; set; } = "WISE-4012SE";

    /// <summary>
    /// SubNode software version (default: "1.0")
    /// </summary>
    public string SubNodeSwVersion { get; set; } = "1.0";

    /// <summary>
    /// Group ID for resource ID generation (optional)
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// MAC Address of the ISensing device (required for MQTT topic subscription)
    /// Format: lowercase hexadecimal without separators (e.g., "00d0c9aabbcc")
    /// </summary>
    public required string MacAddress { get; set; }

    /// <summary>
    /// MQTT broker host address (default: "localhost")
    /// </summary>
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port (default: 1883)
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// MQTT client ID (optional, will be auto-generated if not provided)
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// MQTT username (optional)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// MQTT password (optional)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Use TLS/SSL for MQTT connection (default: false)
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Sensors to extract from ISensing protocol messages
    /// </summary>
    public List<ISensingSensorReporturation> Sensors { get; set; } = new();

    /// <summary>
    /// Background task execution periods
    /// </summary>
    public BackgroundTaskPeriods Periods { get; set; } = new();

    /// <summary>
    /// Path to the DTDL JSON file (optional)
    /// </summary>
    public string? DtdlPath { get; set; }

    /// <summary>
    /// Whether this device configuration is enabled (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Custom properties for device-specific settings
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Adds a sensor configuration to this device
    /// </summary>
    public MqttISensingDeviceConfiguration AddSensor(ISensingSensorReporturation sensor)
    {
        Sensors.Add(sensor);
        return this;
    }

    /// <summary>
    /// Adds a sensor with inline configuration (fluent API)
    /// </summary>
    public MqttISensingDeviceConfiguration AddSensor(
        string name,
        string dtmi,
        string fieldName,
        SensorGroup sensorGroup = SensorGroup.AI,
        string unit = "")
    {
        Sensors.Add(new ISensingSensorReporturation
        {
            Name = name,
            Dtmi = dtmi,
            FieldName = fieldName,
            SensorGroup = sensorGroup,
            Unit = unit
        });
        return this;
    }

    /// <summary>
    /// Sets MQTT broker connection settings
    /// </summary>
    public MqttISensingDeviceConfiguration WithBroker(
        string host,
        int port = 1883,
        bool useTls = false)
    {
        BrokerHost = host;
        BrokerPort = port;
        UseTls = useTls;
        return this;
    }

    /// <summary>
    /// Sets MQTT authentication credentials
    /// </summary>
    public MqttISensingDeviceConfiguration WithCredentials(
        string username,
        string password)
    {
        Username = username;
        Password = password;
        return this;
    }

    /// <summary>
    /// Converts this strongly-typed configuration to the generic DeviceConfiguration
    /// used by the DeviceBase framework.
    /// </summary>
    public DeviceConfiguration ToDeviceConfiguration()
    {
        // Use device ID (will be enriched during initialization)
        string deviceId = DeviceId ?? Guid.NewGuid().ToString();

        // Convert sensor configurations to Sensor objects
        var sensors = Sensors.Select(sensorConfig =>
        {
            string resourceId;
            if (sensorConfig.ResourceId != null)
            {
                resourceId = sensorConfig.ResourceId;
            }
            else if (GroupId != null)
            {
                // Generate resourceId using UUID5
                resourceId = ResourceIdGenerator.GenerateResourceId(GroupId, DeviceName, sensorConfig.Name);
            }
            else
            {
                // Fallback to random GUID
                resourceId = Guid.NewGuid().ToString();
            }

            return new Sensor
            {
                ResourceId = resourceId,
                Name = sensorConfig.Name,
                Dtmi = sensorConfig.Dtmi,
                SensorGroup = sensorConfig.SensorGroup,
                DeviceResourceId = deviceId,
                Parameters = new Dictionary<string, object>
                {
                    ["FieldName"] = sensorConfig.FieldName
                },
                Report = new SensorReport
                {
                    Unit = sensorConfig.Unit
                },
                Metadata = sensorConfig.Metadata
            };
        }).ToList();

        // Build communication settings
        var communication = new Dictionary<string, object>
        {
            ["MacAddress"] = MacAddress,
            ["Manufacturer"] = Manufacturer,
            ["BrokerHost"] = BrokerHost,
            ["BrokerPort"] = BrokerPort,
            ["UseTls"] = UseTls
        };

        if (!string.IsNullOrEmpty(ClientId))
            communication["ClientId"] = ClientId;

        if (!string.IsNullOrEmpty(Username))
            communication["Username"] = Username;

        if (!string.IsNullOrEmpty(Password))
            communication["Password"] = Password;

        return new DeviceConfiguration
        {
            Enabled = Enabled,
            DeviceId = deviceId,
            DeviceName = DeviceName,
            Dtdl = new Weda.SubNode.Abstractions.DigitalTwin.DtdlConfig { DtdlPath = DtdlPath },
            Sensors = sensors,
            DeviceCommunication = communication,
            Periods = Periods,
            Properties = Properties
        };
    }
}

/// <summary>
/// Strongly-typed sensor configuration for ISensing protocol sensors
/// </summary>
public class ISensingSensorReporturation
{
    /// <summary>
    /// Resource ID (optional, will be auto-generated if not provided)
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Sensor name (required, e.g., "AI0", "DI1")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Digital Twin Model Identifier (DTMI) (required)
    /// </summary>
    public required string Dtmi { get; set; }

    /// <summary>
    /// Sensor group/type (default: AI - Analog Input)
    /// </summary>
    public SensorGroup SensorGroup { get; set; } = SensorGroup.AI;

    /// <summary>
    /// ISensing protocol field name (required, e.g., "ai0", "di1")
    /// This is the key used in the ISensing JSON payload
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Unit of measurement (optional, e.g., "V", "mA", "°C")
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

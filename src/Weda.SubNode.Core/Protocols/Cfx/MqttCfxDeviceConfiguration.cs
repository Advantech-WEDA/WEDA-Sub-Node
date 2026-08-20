using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// Strongly-typed configuration for a CFX endpoint reached over MQTT.
/// </summary>
/// <remarks>
/// Provides IntelliSense-friendly programmatic configuration for a bridged CFX endpoint, without
/// having to hand-assemble the generic <see cref="DeviceConfiguration"/> parameter bags.
/// </remarks>
public class MqttCfxDeviceConfiguration : IDeviceConfiguration
{
    /// <summary>
    /// Device ID (optional, auto-generated when not provided).
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Device name (required).
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// Equipment manufacturer (default: <c>Advantech</c>).
    /// </summary>
    public string Manufacturer { get; set; } = "Advantech";

    /// <summary>
    /// Equipment model.
    /// </summary>
    public string Model { get; set; } = "CFX-Endpoint";

    /// <summary>
    /// SubNode software version (default: <c>1.0</c>).
    /// </summary>
    public string SubNodeSwVersion { get; set; } = "1.0";

    /// <summary>
    /// Group ID used for deterministic resource ID generation (optional).
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// CFX handle of the endpoint to subscribe to, conventionally
    /// <c>{Vendor}.{Model}.{Serial}</c> — for example <c>SUNJSONG.SLD880A.0001</c>.
    /// </summary>
    /// <remarks>
    /// Leave null to subscribe to every endpoint on the broker, which is useful for discovery but
    /// means one device instance receives messages from the whole line.
    /// </remarks>
    public string? CfxHandle { get; set; }

    /// <summary>
    /// Topic root segment separating the handle from the CFX message path (default: <c>CFX</c>).
    /// </summary>
    public string TopicRoot { get; set; } = CfxTopic.DefaultRoot;

    /// <summary>
    /// Number of segments in the CFX handles matched when <see cref="CfxHandle"/> is not set
    /// (default: 3).
    /// </summary>
    public int HandleSegments { get; set; } = 3;

    /// <summary>
    /// MQTT broker host address (default: <c>localhost</c>).
    /// </summary>
    public string BrokerHost { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port (default: 1883).
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// MQTT client ID (optional, auto-generated when not provided).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// MQTT username (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// MQTT password (optional).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Use TLS for the MQTT connection (default: <c>false</c>).
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// CFX messages to report, one sensor per message type.
    /// </summary>
    public List<CfxSensorConfiguration> Sensors { get; set; } = [];

    /// <summary>
    /// Background task execution periods.
    /// </summary>
    public BackgroundTaskPeriods Periods { get; set; } = new();

    /// <summary>
    /// Path to the DTDL JSON file (optional).
    /// </summary>
    public string? DtdlPath { get; set; }

    /// <summary>
    /// Whether this device configuration is enabled (default: <c>true</c>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Custom properties for device-specific settings.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = [];

    /// <summary>
    /// Adds a sensor bound to one CFX message.
    /// </summary>
    public MqttCfxDeviceConfiguration AddSensor(CfxSensorConfiguration sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        Sensors.Add(sensor);
        return this;
    }

    /// <summary>
    /// Adds a sensor bound to one CFX message, naming it from the message.
    /// </summary>
    /// <param name="messageName">
    /// Fully-qualified CFX message name, for example <c>CFX.Production.WorkStarted</c>.
    /// </param>
    /// <param name="dtmi">Digital Twin Model Identifier for the sensor.</param>
    /// <param name="name">
    /// Sensor name; defaults to the snake_case form derived from <paramref name="messageName"/>.
    /// </param>
    /// <param name="interval">Reporting interval in milliseconds.</param>
    public MqttCfxDeviceConfiguration AddMessage(
        string messageName,
        string dtmi,
        string? name = null,
        int interval = 1000)
    {
        Sensors.Add(new CfxSensorConfiguration
        {
            Name = name ?? CfxMessageCatalog.ToSensorName(messageName),
            Dtmi = dtmi,
            MessageName = messageName,
            Interval = interval,
        });

        return this;
    }

    /// <summary>
    /// Adds a sensor for every CFX message in <see cref="CfxMessageCatalog.SupportedMessageNames"/>.
    /// </summary>
    /// <param name="dtmi">Digital Twin Model Identifier applied to each sensor.</param>
    /// <param name="interval">Reporting interval in milliseconds.</param>
    public MqttCfxDeviceConfiguration AddAllCatalogMessages(string dtmi, int interval = 1000)
    {
        foreach (var messageName in CfxMessageCatalog.SupportedMessageNames)
        {
            AddMessage(messageName, dtmi, interval: interval);
        }

        return this;
    }

    /// <summary>
    /// Sets the MQTT broker connection settings.
    /// </summary>
    public MqttCfxDeviceConfiguration WithBroker(string host, int port = 1883, bool useTls = false)
    {
        BrokerHost = host;
        BrokerPort = port;
        UseTls = useTls;
        return this;
    }

    /// <summary>
    /// Sets the MQTT authentication credentials.
    /// </summary>
    public MqttCfxDeviceConfiguration WithCredentials(string username, string password)
    {
        Username = username;
        Password = password;
        return this;
    }

    /// <summary>
    /// Converts this configuration into the generic <see cref="DeviceConfiguration"/> consumed by
    /// the device framework.
    /// </summary>
    public DeviceConfiguration ToDeviceConfiguration()
    {
        var deviceId = DeviceId ?? Guid.NewGuid().ToString();

        var sensors = Sensors.Select(sensorConfig =>
        {
            var resourceId = sensorConfig.ResourceId
                ?? (GroupId is not null
                    ? ResourceIdGenerator.GenerateResourceId(GroupId, DeviceName, sensorConfig.Name)
                    : Guid.NewGuid().ToString());

            return new Sensor
            {
                ResourceId = resourceId,
                Name = sensorConfig.Name,
                Dtmi = sensorConfig.Dtmi,
                SensorGroup = sensorConfig.SensorGroup,
                DeviceResourceId = deviceId,
                Parameters = new Dictionary<string, object>
                {
                    [CfxSensorParameters.MessageNameKey] = sensorConfig.MessageName,
                },
                Report = new SensorReport
                {
                    Enabled = sensorConfig.Enabled,
                    Interval = sensorConfig.Interval,
                    Unit = sensorConfig.Unit,
                },
                Metadata = sensorConfig.Metadata,
                SensorInfo = sensorConfig.Info,
            };
        }).ToList();

        var communication = new Dictionary<string, object>
        {
            ["Manufacturer"] = Manufacturer,
            ["BrokerHost"] = BrokerHost,
            ["BrokerPort"] = BrokerPort,
            ["UseTls"] = UseTls,
            [CfxPubSubParser.TopicRootKey] = TopicRoot,
            [CfxPubSubParser.HandleSegmentsKey] = HandleSegments,
        };

        if (!string.IsNullOrEmpty(CfxHandle))
        {
            communication[CfxPubSubParser.CfxHandleKey] = CfxHandle;
        }

        if (!string.IsNullOrEmpty(ClientId))
        {
            communication["ClientId"] = ClientId;
        }

        if (!string.IsNullOrEmpty(Username))
        {
            communication["Username"] = Username;
        }

        if (!string.IsNullOrEmpty(Password))
        {
            communication["Password"] = Password;
        }

        return new DeviceConfiguration
        {
            Enabled = Enabled,
            DeviceId = deviceId,
            DeviceName = DeviceName,
            Dtdl = new Abstractions.DigitalTwin.DtdlConfig { DtdlPath = DtdlPath },
            Sensors = sensors,
            DeviceCommunication = communication,
            Periods = Periods,
            Properties = Properties,
        };
    }
}

/// <summary>
/// Strongly-typed sensor configuration binding one CFX message to one reported value.
/// </summary>
public class CfxSensorConfiguration
{
    /// <summary>
    /// Resource ID (optional, auto-generated when not provided).
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Sensor name (required), conventionally the snake_case form of the CFX message name —
    /// for example <c>cfx_station_state_changed</c>.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Digital Twin Model Identifier (required).
    /// </summary>
    public required string Dtmi { get; set; }

    /// <summary>
    /// Fully-qualified CFX message name this sensor reports (required) — for example
    /// <c>CFX.ResourcePerformance.StationStateChanged</c>.
    /// </summary>
    public required string MessageName { get; set; }

    /// <summary>
    /// Sensor group. CFX messages are process events rather than analog or digital channels, so
    /// they default to <see cref="SensorGroup.SYS"/>.
    /// </summary>
    public SensorGroup SensorGroup { get; set; } = SensorGroup.SYS;

    /// <summary>
    /// Whether this sensor reports (default: <c>true</c>).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Reporting interval in milliseconds.
    /// </summary>
    public int Interval { get; set; } = 1000;

    /// <summary>
    /// Unit of measurement. CFX bodies are structured JSON documents rather than scalar readings,
    /// so this is normally left empty.
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// DTDL-related information for the sensor.
    /// </summary>
    /// <remarks>
    /// Defaults to the <c>application/json</c> schema because a CFX sensor reports a message body,
    /// which is a JSON document rather than a scalar. Declaring <c>string</c> instead would still
    /// transmit correctly but would misreport the payload: the sensor would be emitted as a plain
    /// DTDL string telemetry instead of receiving the <c>dtmi:advantech:app:json</c> DTMI, and
    /// recordings would be tagged <c>SchemaType.String</c> rather than
    /// <c>SchemaType.ApplicationJson</c>.
    /// </remarks>
    public SensorInfo Info { get; set; } = new() { Schema = CfxSchema };

    /// <summary>DTDL schema declared for CFX message bodies.</summary>
    public const string CfxSchema = "application/json";
}

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Communication.OpcUa;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Protocols.OpcUa;

/// <summary>
/// Strongly-typed configuration for OPC-UA devices.
/// Provides IntelliSense-friendly programmatic configuration without needing to consult documentation.
/// </summary>
public class OpcUaTypedDeviceConfiguration : IDeviceConfiguration
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
    /// Device model (default: "Generic OPC-UA")
    /// </summary>
    public string Model { get; set; } = "Generic OPC-UA";

    /// <summary>
    /// SubNode software version (default: "1.0")
    /// </summary>
    public string SubNodeSwVersion { get; set; } = "1.0";

    /// <summary>
    /// Group ID for resource ID generation (optional)
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// OPC-UA server endpoint URL (required)
    /// </summary>
    public required string EndpointUrl { get; set; }

    /// <summary>
    /// Security mode (default: None)
    /// </summary>
    public OpcUaSecurityMode SecurityMode { get; set; } = OpcUaSecurityMode.None;

    /// <summary>
    /// Authentication type (default: Anonymous)
    /// </summary>
    public OpcUaAuthType AuthType { get; set; } = OpcUaAuthType.Anonymous;

    /// <summary>
    /// Username for UserPassword authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for UserPassword authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Certificate file path for Certificate authentication
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Sensors/nodes to read from the OPC-UA server
    /// </summary>
    public List<OpcUaSensorConfiguration> Sensors { get; set; } = new();

    /// <summary>
    /// Background task execution periods
    /// </summary>
    public BackgroundTaskPeriods Periods { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to automatically generate DTDL content from sensor definitions.
    /// </summary>
    public bool AutoGenEnabled { get; set; } = true;

    /// <summary>
    /// Path to the DTDL JSON file (optional)
    /// </summary>
    public string? DtdlPath { get; set; }

    /// <summary>
    /// Whether this device configuration is enabled (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Connection settings (retry, timeout, security)
    /// </summary>
    public ConnectionSettings? ConnectionSettings { get; set; }

    /// <summary>
    /// Custom properties for device-specific settings
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Adds a sensor configuration to this device
    /// </summary>
    public OpcUaTypedDeviceConfiguration AddSensor(OpcUaSensorConfiguration sensor)
    {
        Sensors.Add(sensor);
        return this;
    }

    /// <summary>
    /// Adds a sensor with inline configuration (fluent API)
    /// </summary>
    public OpcUaTypedDeviceConfiguration AddSensor(
        string name,
        string nodeId,
        ushort namespaceIndex = 2,
        OpcUaDataType dataType = OpcUaDataType.Double,
        SensorGroup sensorGroup = SensorGroup.AI)
    {
        Sensors.Add(new OpcUaSensorConfiguration
        {
            Name = name,
            NodeId = nodeId,
            NamespaceIndex = namespaceIndex,
            DataType = dataType,
            SensorGroup = sensorGroup
        });
        return this;
    }

    /// <summary>
    /// Adds multiple sensors to this device
    /// </summary>
    public OpcUaTypedDeviceConfiguration AddSensors(params OpcUaSensorConfiguration[] sensors)
    {
        foreach (var sensor in sensors)
        {
            Sensors.Add(sensor);
        }
        return this;
    }

    /// <summary>
    /// Converts this strongly-typed configuration to the generic DeviceConfiguration
    /// used by the DeviceBase framework.
    /// </summary>
    public DeviceConfiguration ToDeviceConfiguration()
    {
        string deviceId = DeviceId ?? string.Empty;

        var sensors = Sensors.Select(sensorConfig =>
        {
            string resourceId;
            if (sensorConfig.ResourceId != null)
            {
                resourceId = sensorConfig.ResourceId;
            }
            else if (GroupId != null)
            {
                resourceId = ResourceIdGenerator.GenerateResourceId(GroupId, DeviceName, sensorConfig.Name);
            }
            else
            {
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
                    ["NodeId"] = sensorConfig.NodeId,
                    ["NamespaceIndex"] = sensorConfig.NamespaceIndex,
                    ["DataType"] = sensorConfig.DataType.ToString()
                },
                Report = sensorConfig.Config,
                Metadata = sensorConfig.Metadata,
                SensorInfo = sensorConfig.Info
            };
        }).ToList();

        return new DeviceConfiguration
        {
            Enabled = Enabled,
            DeviceId = deviceId,
            DeviceName = DeviceName,
            Dtdl = new DtdlConfig
            {
                AutoGenEnabled = AutoGenEnabled,
                DtdlPath = DtdlPath
            },
            Sensors = sensors,
            DeviceCommunication = new Dictionary<string, object>
            {
                ["EndpointUrl"] = EndpointUrl,
                ["SecurityMode"] = SecurityMode.ToString(),
                ["AuthType"] = AuthType.ToString(),
                ["Username"] = Username ?? string.Empty,
                ["Password"] = Password ?? string.Empty,
                ["CertificatePath"] = CertificatePath ?? string.Empty
            },
            ConnectionSettings = ConnectionSettings,
            Periods = Periods,
            Properties = Properties
        };
    }
}

/// <summary>
/// Strongly-typed sensor configuration for OPC-UA sensors
/// </summary>
public class OpcUaSensorConfiguration
{
    /// <summary>
    /// Resource ID (optional, will be auto-generated if not provided)
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// Sensor name (required)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Digital Twin Model Identifier (DTMI) (optional)
    /// </summary>
    public string? Dtmi { get; set; }

    /// <summary>
    /// Sensor group/type (default: AI - Analog Input)
    /// </summary>
    public SensorGroup SensorGroup { get; set; } = SensorGroup.AI;

    /// <summary>
    /// OPC-UA Node identifier string (e.g., "Temperature", "ns=2;s=Temperature")
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// OPC-UA namespace index (default: 2)
    /// </summary>
    public ushort NamespaceIndex { get; set; } = 2;

    /// <summary>
    /// Expected data type of the node value (default: Double)
    /// </summary>
    public OpcUaDataType DataType { get; set; } = OpcUaDataType.Double;

    /// <summary>
    /// Sensor configuration (transform pipeline, DSP filters, etc.)
    /// </summary>
    public SensorReport Config { get; set; } = new();

    /// <summary>
    /// DTDL-related information for the sensor (DisplayName, Description, Schema)
    /// </summary>
    public SensorInfo Info { get; set; } = new() { Schema = "double" };

    /// <summary>
    /// Fluent API: Adds a transform to this sensor's pipeline
    /// </summary>
    public OpcUaSensorConfiguration AddTransform(ITelemetryTransform transform)
    {
        Config.AddTransform(transform);
        return this;
    }

    /// <summary>
    /// Fluent API: Adds a DSP filter to this sensor's pipeline
    /// </summary>
    public OpcUaSensorConfiguration AddDspFilter(IDspFilter filter)
    {
        Config.AddDspFilter(filter);
        return this;
    }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

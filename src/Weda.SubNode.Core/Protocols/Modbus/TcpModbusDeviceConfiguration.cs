using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Strongly-typed configuration for TCP Modbus devices.
/// Provides IntelliSense-friendly programmatic configuration without needing to consult documentation.
/// </summary>
public class TcpModbusDeviceConfiguration : IDeviceConfiguration
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
    /// Device model (default: "Generic Modbus")
    /// </summary>
    public string Model { get; set; } = "Generic Modbus";

    /// <summary>
    /// SubNode software version (default: "1.0")
    /// </summary>
    public string SubNodeSwVersion { get; set; } = "1.0";

    /// <summary>
    /// Group ID for resource ID generation (optional)
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// TCP/IP host address (required)
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// TCP/IP port (default: 502)
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// Modbus slave/unit ID (default: 1)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Sensors/registers to read from the Modbus device
    /// </summary>
    public List<ModbusSensorReporturation> Sensors { get; set; } = new();

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
    public TcpModbusDeviceConfiguration AddSensor(ModbusSensorReporturation sensor)
    {
        Sensors.Add(sensor);
        return this;
    }

    /// <summary>
    /// Adds a sensor with inline configuration (fluent API)
    /// </summary>
    public TcpModbusDeviceConfiguration AddSensor(
        string name,
        string dtmi,
        ushort registerAddress,
        ushort registerCount = 1,
        ModbusDataType dataType = ModbusDataType.UInt16,
        ModbusRegisterType registerType = ModbusRegisterType.HoldingRegister,
        SensorGroup sensorGroup = SensorGroup.AI)
    {
        Sensors.Add(new ModbusSensorReporturation
        {
            Name = name,
            Dtmi = dtmi,
            RegisterAddress = registerAddress,
            RegisterCount = registerCount,
            DataType = dataType,
            RegisterType = registerType,
            SensorGroup = sensorGroup
        });
        return this;
    }

    /// <summary>
    /// Adds multiple sensors to this device
    /// </summary>
    public TcpModbusDeviceConfiguration AddSensors(params ModbusSensorReporturation[] sensors)
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
        // Use device ID (will be enriched during initialization)
        string deviceId = DeviceId ?? string.Empty; 

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
                    ["RegisterAddress"] = sensorConfig.RegisterAddress,
                    ["RegisterCount"] = sensorConfig.RegisterCount,
                    ["RegisterType"] = sensorConfig.RegisterType.ToString(),
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
                ["Host"] = Host,
                ["Port"] = Port,
                ["SlaveId"] = SlaveId
            },
            ConnectionSettings = ConnectionSettings,
            Periods = Periods,
            Properties = Properties
        };
    }
}

/// <summary>
/// Strongly-typed sensor configuration for Modbus sensors
/// </summary>
public class ModbusSensorReporturation
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
    /// Digital Twin Model Identifier (DTMI) (required)
    /// </summary>
    public string? Dtmi { get; set; }

    /// <summary>
    /// Sensor group/type (default: AI - Analog Input)
    /// </summary>
    public SensorGroup SensorGroup { get; set; } = SensorGroup.AI;

    /// <summary>
    /// Modbus register type (default: HoldingRegister)
    /// </summary>
    public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;

    /// <summary>
    /// Starting register address (required)
    /// </summary>
    public ushort RegisterAddress { get; set; }

    /// <summary>
    /// Number of registers to read (default: 1)
    /// </summary>
    public ushort RegisterCount { get; set; } = 1;

    /// <summary>
    /// Data type for parsing the register values (default: UInt16)
    /// </summary>
    public ModbusDataType DataType { get; set; } = ModbusDataType.UInt16;

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
    public ModbusSensorReporturation AddTransform(ITelemetryTransform transform)
    {
        Config.AddTransform(transform);
        return this;
    }

    /// <summary>
    /// Fluent API: Adds a DSP filter to this sensor's pipeline
    /// </summary>
    public ModbusSensorReporturation AddDspFilter(IDspFilter filter)
    {
        Config.AddDspFilter(filter);
        return this;
    }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

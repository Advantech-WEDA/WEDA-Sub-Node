using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// SubNode configuration update message received from cloud.
///
/// JSON format:
/// <code>
/// {
///   "cmd": "updateSysConfig",
///   "protoVer": "eco1j",
///   "deviceId": "device-001",
///   "orgId": "tenant-alias",
///   "seqId": 1,
///   "reqSeqId": "df00a8d8-01e0-44c0-a169-8b0630a1a860",
///   "timestamp": 1735776000000,
///   "data": {
///     "cfg": {
///       "reported": { ... },
///       "desired": {
///         "systemcfg": { ... },
///         "customcfg": { ... },
///         "devicecfg": { ... }
///       }
///     }
///   }
/// }
/// </code>
/// </summary>
public class SubNodeConfigUpdateMessage
{
    /// <summary>
    /// Command type (e.g., "updateSysConfig", "updateDevConfig", "updateCustomConfig")
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = string.Empty;

    /// <summary>
    /// Protocol version
    /// </summary>
    [JsonPropertyName("protoVer")]
    public string ProtoVer { get; set; } = string.Empty;

    /// <summary>
    /// Device ID that this configuration update is for
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Group/tenant ID for multi-tenant scenarios
    /// </summary>
    [JsonPropertyName("groupId")]
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Sequence ID for tracking
    /// </summary>
    [JsonPropertyName("seqId")]
    public long SeqId { get; set; }

    /// <summary>
    /// Request sequence ID for correlation
    /// </summary>
    [JsonPropertyName("reqSeqId")]
    public string ReqSeqId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp (Unix milliseconds, 0 if not specified)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Configuration update data
    /// </summary>
    [JsonPropertyName("data")]
    public SubNodeConfigUpdateData? Data { get; set; }
}

/// <summary>
/// Configuration update data wrapper
/// </summary>
public class SubNodeConfigUpdateData
{
    /// <summary>
    /// Configuration state (desired/reported)
    /// </summary>
    [JsonPropertyName("cfg")]
    public SubNodeConfigState? Cfg { get; set; }
}

/// <summary>
/// Configuration state containing desired and reported states.
/// Both desired and reported use the same structure with systemcfg, devicecfg, customcfg keys.
/// </summary>
public class SubNodeConfigState
{
    /// <summary>
    /// Desired configuration from cloud.
    /// Contains the configuration sections that should be applied.
    /// </summary>
    [JsonPropertyName("desired")]
    public SubNodeDesiredConfigSections? Desired { get; set; }

    /// <summary>
    /// Reported configuration from device.
    /// Contains the current configuration state plus status information.
    /// </summary>
    [JsonPropertyName("reported")]
    public SubNodeReportedConfigSections? Reported { get; set; }
}

/// <summary>
/// Base configuration sections container.
/// The structure mirrors the local config files: systemcfg.json, devicecfg.json, customcfg.json.
/// </summary>
public class SubNodeConfigSections
{
    /// <summary>
    /// System configuration section (maps to systemcfg.json).
    /// Contains WedaNode connection settings and Serilog logging configuration.
    /// </summary>
    [JsonPropertyName("systemcfg")]
    public SubNodeSystemCfgDto? SystemCfg { get; set; }

    /// <summary>
    /// Alias for SystemCfg (backward compatibility)
    /// </summary>
    [JsonIgnore]
    public SubNodeSystemCfgDto? SystemConfig
    {
        get => SystemCfg;
        set => SystemCfg = value;
    }

    /// <summary>
    /// Device configuration section (maps to devicecfg.json).
    /// Contains SubNode info and device configurations.
    /// </summary>
    [JsonPropertyName("devicecfg")]
    public SubNodeDeviceCfgDto? DeviceCfg { get; set; }

    /// <summary>
    /// Alias for DeviceCfg (backward compatibility).
    /// Provides access to DeviceConfigs dictionary directly.
    /// </summary>
    [JsonIgnore]
    public SubNodeDeviceCfgDto? SubNodeDeviceConfig
    {
        get => DeviceCfg;
        set => DeviceCfg = value;
    }

    /// <summary>
    /// Custom configuration section (maps to customcfg.json).
    /// Contains user-defined settings. Uses JsonElement to preserve original JSON structure
    /// for round-trip serialization without type loss.
    /// </summary>
    [JsonPropertyName("customcfg")]
    public Dictionary<string, JsonElement>? CustomCfg { get; set; }

    /// <summary>
    /// Alias for CustomCfg (backward compatibility)
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, JsonElement>? CustomConfig
    {
        get => CustomCfg;
        set => CustomCfg = value;
    }

    /// <summary>
    /// Additional configuration sections (extensible).
    /// Allows for future config types without code changes.
    /// Uses JsonElement to preserve original JSON structure.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalConfigs { get; set; }
}

/// <summary>
/// Desired configuration sections from cloud.
/// Inherits base config sections structure.
/// </summary>
public class SubNodeDesiredConfigSections : SubNodeConfigSections
{
}

/// <summary>
/// Reported configuration sections from device.
/// Extends base config sections with status, error message, and timestamp.
/// </summary>
public class SubNodeReportedConfigSections : SubNodeConfigSections
{
    /// <summary>
    /// Alias for DeviceCfg.DeviceConfigs (backward compatibility).
    /// Allows direct access to device configurations dictionary.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, SubNodeDeviceConfigDto>? DeviceConfigs
    {
        get => DeviceCfg?.DeviceConfigs;
        set
        {
            DeviceCfg ??= new SubNodeDeviceCfgDto();
            DeviceCfg.DeviceConfigs = value;
        }
    }

    /// <summary>
    /// Update status (updating, success, failed, invalid, noUpdateRequired)
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Error message if update failed
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Last update timestamp (ISO 8601 format)
    /// </summary>
    [JsonPropertyName("lastUpdateTime")]
    public DateTimeOffset? LastUpdateTime { get; set; }
}

/// <summary>
/// Type alias for backward compatibility
/// </summary>
public class SubNodeReportedConfig : SubNodeReportedConfigSections
{
}

/// <summary>
/// System configuration DTO (systemcfg.json structure).
/// Contains WedaNode connection settings and Serilog logging configuration.
/// </summary>
public class SubNodeSystemCfgDto
{
    /// <summary>
    /// WedaNode (NATS) connection settings.
    /// Note: Changes to WedaNode require application restart.
    /// </summary>
    [JsonPropertyName("WedaNode")]
    public SubNodeWedaNodeConfigDto? WedaNode { get; set; }

    /// <summary>
    /// Serilog logging configuration
    /// </summary>
    [JsonPropertyName("Serilog")]
    public Dictionary<string, JsonElement>? Serilog { get; set; }
}

/// <summary>
/// WedaNode (NATS) connection configuration DTO.
/// Maps to the WedaNode section in systemcfg.json.
/// </summary>
public class SubNodeWedaNodeConfigDto
{
    /// <summary>
    /// NATS server URL
    /// </summary>
    [JsonPropertyName("Url")]
    public string? Url { get; set; }

    /// <summary>
    /// Connection name
    /// </summary>
    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    /// <summary>
    /// Authentication strategy (None, UserPassword, Token, TlsCert, CredFile)
    /// </summary>
    [JsonPropertyName("AuthStrategy")]
    public string? AuthStrategy { get; set; }

    /// <summary>
    /// Username for UserPassword authentication
    /// </summary>
    [JsonPropertyName("Username")]
    public string? Username { get; set; }

    /// <summary>
    /// Password for UserPassword authentication
    /// </summary>
    [JsonPropertyName("Password")]
    public string? Password { get; set; }

    /// <summary>
    /// Token for Token authentication
    /// </summary>
    [JsonPropertyName("Token")]
    public string? Token { get; set; }

    /// <summary>
    /// Credential file path for CredFile authentication
    /// </summary>
    [JsonPropertyName("CredFile")]
    public string? CredFile { get; set; }

    /// <summary>
    /// Serializer type (json, protobuf, default)
    /// </summary>
    [JsonPropertyName("SerializerType")]
    public string? SerializerType { get; set; }
}

/// <summary>
/// Device configuration DTO (devicecfg.json structure).
/// Contains SubNode info and device configurations.
/// </summary>
public class SubNodeDeviceCfgDto
{
    private Dictionary<string, SubNodeDeviceConfigDto>? _deviceConfigs;

    /// <summary>
    /// SubNode information and settings
    /// </summary>
    [JsonPropertyName("SubNode")]
    public SubNodeInfoDto? SubNode { get; set; }

    /// <summary>
    /// Dictionary of device configurations keyed by device name (case-insensitive).
    /// </summary>
    [JsonPropertyName("DeviceConfigs")]
    public Dictionary<string, SubNodeDeviceConfigDto>? DeviceConfigs
    {
        get => _deviceConfigs;
        set
        {
            if (value != null && value.Comparer != StringComparer.OrdinalIgnoreCase)
            {
                _deviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>(value, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _deviceConfigs = value;
            }
        }
    }
}

/// <summary>
/// SubNode information DTO
/// </summary>
public class SubNodeInfoDto
{
    /// <summary>
    /// SubNode name
    /// </summary>
    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    /// <summary>
    /// Whether to auto-generate DTDL
    /// </summary>
    [JsonPropertyName("AutoGenEnabled")]
    public bool AutoGenEnabled { get; set; }
}

/// <summary>
/// Device configuration DTO matching the devicecfg.json structure
/// </summary>
public class SubNodeDeviceConfigDto
{
    /// <summary>
    /// Whether this device is enabled
    /// </summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Device type (e.g., "TcpModbus", "WebSocket")
    /// </summary>
    [JsonPropertyName("DeviceType")]
    public string? DeviceType { get; set; }

    /// <summary>
    /// Alias for DeviceType (backward compatibility)
    /// </summary>
    [JsonIgnore]
    public string? SubNodeType
    {
        get => DeviceType;
        set => DeviceType = value;
    }

    /// <summary>
    /// DTDL configuration
    /// </summary>
    [JsonPropertyName("Dtdl")]
    public SubNodeDtdlConfigDto? Dtdl { get; set; }

    /// <summary>
    /// Alias for Dtdl.DtdlPath (backward compatibility)
    /// </summary>
    [JsonIgnore]
    public string? DtdlPath
    {
        get => Dtdl?.DtdlPath;
        set
        {
            Dtdl ??= new SubNodeDtdlConfigDto();
            Dtdl.DtdlPath = value;
        }
    }

    /// <summary>
    /// Device capabilities
    /// </summary>
    [JsonPropertyName("DeviceCapabilities")]
    public SubNodeDeviceCapabilitiesDto? DeviceCapabilities { get; set; }

    /// <summary>
    /// Communication settings (protocol-specific).
    /// Uses object to allow runtime manipulation.
    /// </summary>
    [JsonPropertyName("Communication")]
    public Dictionary<string, object>? Communication { get; set; }

    /// <summary>
    /// Sensor configurations
    /// </summary>
    [JsonPropertyName("Sensors")]
    public List<SubNodeSensorReportDto>? Sensors { get; set; }

    /// <summary>
    /// Background task periods (milliseconds)
    /// </summary>
    [JsonPropertyName("Periods")]
    public SubNodePeriodsDto? Periods { get; set; }
}

/// <summary>
/// DTDL configuration DTO
/// </summary>
public class SubNodeDtdlConfigDto
{
    /// <summary>
    /// Whether to auto-generate DTDL from sensors
    /// </summary>
    [JsonPropertyName("AutoGenEnabled")]
    public bool AutoGenEnabled { get; set; }

    /// <summary>
    /// Path to DTDL file (when AutoGenEnabled is false)
    /// </summary>
    [JsonPropertyName("DtdlPath")]
    public string? DtdlPath { get; set; }

    /// <summary>
    /// DTDL interface object (auto-generated or loaded from file).
    /// Contains @context, @id, @type, and contents for the device.
    /// </summary>
    [JsonPropertyName("DtdlInterface")]
    public object? DtdlInterface { get; set; }
}

/// <summary>
/// Background task periods DTO
/// </summary>
public class SubNodePeriodsDto
{
    /// <summary>
    /// Telemetry reading period (ms)
    /// </summary>
    [JsonPropertyName("ReadTelemetry")]
    public int ReadTelemetry { get; set; }

    /// <summary>
    /// Telemetry sending period (ms)
    /// </summary>
    [JsonPropertyName("SendTelemetry")]
    public int SendTelemetry { get; set; }

    /// <summary>
    /// Health reporting period (ms)
    /// </summary>
    [JsonPropertyName("ReportHealth")]
    public int ReportHealth { get; set; }

    /// <summary>
    /// Configuration sync/report period (ms).
    /// When enabled (> 0), periodically reports device configuration to cloud.
    /// </summary>
    [JsonPropertyName("ReportConfiguration")]
    public int ReportConfiguration { get; set; }
}

/// <summary>
/// Device capabilities DTO
/// </summary>
public class SubNodeDeviceCapabilitiesDto
{
    /// <summary>
    /// Manufacturer name
    /// </summary>
    [JsonPropertyName("Manufacturer")]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model
    /// </summary>
    [JsonPropertyName("Model")]
    public string? Model { get; set; }

    /// <summary>
    /// SubNode software version
    /// </summary>
    [JsonPropertyName("SubNodeSwVersion")]
    public string? SubNodeSwVersion { get; set; }

    /// <summary>
    /// Additional device info
    /// </summary>
    [JsonPropertyName("DeviceInfo")]
    public Dictionary<string, object>? DeviceInfo { get; set; }
}

/// <summary>
/// Sensor configuration DTO matching the devicecfg.json structure
/// </summary>
public class SubNodeSensorReportDto
{
    /// <summary>
    /// Sensor name/identifier
    /// </summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Digital Twin Model Identifier
    /// </summary>
    [JsonPropertyName("Dtmi")]
    public string? Dtmi { get; set; }

    /// <summary>
    /// Sensor group (AI, DI, DO, AO, TEMP, etc.)
    /// </summary>
    [JsonPropertyName("SensorGroup")]
    public string? SensorGroup { get; set; }

    /// <summary>
    /// Protocol-specific parameters.
    /// Uses object to allow runtime manipulation of parameter values.
    /// </summary>
    [JsonPropertyName("Parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Sensor report configuration (sampling interval, transforms, DSP, thresholds)
    /// </summary>
    [JsonPropertyName("Report")]
    public SubNodeSensorRuntimeConfigDto? Report { get; set; }

    /// <summary>
    /// Backward compatibility: Config is an alias for Report
    /// </summary>
    [JsonIgnore]
    public SubNodeSensorRuntimeConfigDto? Config
    {
        get => Report;
        set => Report = value;
    }

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("Metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Sensor runtime configuration DTO
/// </summary>
public class SubNodeSensorRuntimeConfigDto
{
    /// <summary>
    /// Whether the sensor is enabled
    /// </summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Polling interval in milliseconds
    /// </summary>
    [JsonPropertyName("Interval")]
    public int Interval { get; set; } = 1000;

    /// <summary>
    /// Measurement unit
    /// </summary>
    [JsonPropertyName("Unit")]
    public string? Unit { get; set; }

    /// <summary>
    /// Transform pipeline configuration
    /// </summary>
    [JsonPropertyName("TransformPipeline")]
    public List<SubNodeTransformConfigDto>? TransformPipeline { get; set; }

    /// <summary>
    /// DSP filter pipeline configuration
    /// </summary>
    [JsonPropertyName("DspPipeline")]
    public List<SubNodeDspFilterConfigDto>? DspPipeline { get; set; }

    /// <summary>
    /// Threshold configuration
    /// </summary>
    [JsonPropertyName("Thresholds")]
    public SubNodeThresholdsDto? Thresholds { get; set; }
}

/// <summary>
/// Transform configuration DTO
/// </summary>
public class SubNodeTransformConfigDto
{
    /// <summary>
    /// Transform type (e.g., "Calibration", "UnitConversion")
    /// </summary>
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this transform is enabled
    /// </summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Transform-specific parameters.
    /// Uses object to allow runtime manipulation of parameter values.
    /// </summary>
    [JsonPropertyName("Parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// DSP filter configuration DTO
/// </summary>
public class SubNodeDspFilterConfigDto
{
    /// <summary>
    /// Filter type (e.g., "MovingAverage", "Kalman")
    /// </summary>
    [JsonPropertyName("Type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this filter is enabled
    /// </summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Filter-specific parameters.
    /// Uses object to allow runtime manipulation of parameter values.
    /// </summary>
    [JsonPropertyName("Parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Threshold configuration DTO
/// </summary>
public class SubNodeThresholdsDto
{
    /// <summary>
    /// Upper critical threshold
    /// </summary>
    [JsonPropertyName("UpperCritical")]
    public double? UpperCritical { get; set; }

    /// <summary>
    /// Upper warning threshold
    /// </summary>
    [JsonPropertyName("UpperWarning")]
    public double? UpperWarning { get; set; }

    /// <summary>
    /// Lower warning threshold
    /// </summary>
    [JsonPropertyName("LowerWarning")]
    public double? LowerWarning { get; set; }

    /// <summary>
    /// Lower critical threshold
    /// </summary>
    [JsonPropertyName("LowerCritical")]
    public double? LowerCritical { get; set; }
}

/// <summary>
/// Configuration update status constants
/// </summary>
public static class ConfigUpdateStatus
{
    /// <summary>
    /// Configuration update is in progress
    /// </summary>
    public const string Updating = "updating";

    /// <summary>
    /// Configuration update completed successfully
    /// </summary>
    public const string Success = "success";

    /// <summary>
    /// Configuration update failed
    /// </summary>
    public const string Failed = "failed";

    /// <summary>
    /// Configuration payload is invalid
    /// </summary>
    public const string Invalid = "invalid";

    /// <summary>
    /// No update required (configuration unchanged)
    /// </summary>
    public const string NoUpdateRequired = "noUpdateRequired";
}

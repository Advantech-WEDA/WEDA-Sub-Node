using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// SubNode configuration update message received from cloud.
/// This message contains the full device configuration structure following the SubNode config format.
/// </summary>
public class SubNodeConfigUpdateMessage
{
    /// <summary>
    /// Device ID that this configuration update is for
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Group ID for multi-tenant scenarios
    /// </summary>
    [JsonPropertyName("groupId")]
    public string GroupId { get; set; } = string.Empty;

    /// <summary>
    /// Command type (e.g., "updateCmd")
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = string.Empty;

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
/// Configuration state containing desired and reported states
/// </summary>
public class SubNodeConfigState
{
    /// <summary>
    /// Desired configuration from cloud
    /// </summary>
    [JsonPropertyName("desired")]
    public SubNodeDesiredConfig? Desired { get; set; }

    /// <summary>
    /// Reported configuration from device (included in response)
    /// </summary>
    [JsonPropertyName("reported")]
    public SubNodeReportedConfig? Reported { get; set; }
}

/// <summary>
/// Desired configuration from cloud
/// </summary>
public class SubNodeDesiredConfig
{
    /// <summary>
    /// SubNode device configuration (for device-config type)
    /// </summary>
    [JsonPropertyName("subNodeDeviceConfig")]
    public SubNodeDeviceConfigWrapper? SubNodeDeviceConfig { get; set; }

    /// <summary>
    /// System configuration (for system-config type) - Serilog + WedaNode
    /// </summary>
    [JsonPropertyName("systemConfig")]
    public SubNodeSystemConfigDto? SystemConfig { get; set; }

    /// <summary>
    /// Custom configuration (for custom-config type) - User-defined settings
    /// </summary>
    [JsonPropertyName("customConfig")]
    public Dictionary<string, object>? CustomConfig { get; set; }
}

/// <summary>
/// Reported configuration from device
/// </summary>
public class SubNodeReportedConfig
{
    /// <summary>
    /// SubNode device configuration (current state) - for device-config type
    /// </summary>
    [JsonPropertyName("subNodeDeviceConfig")]
    public SubNodeDeviceConfigWrapper? SubNodeDeviceConfig { get; set; }

    /// <summary>
    /// System configuration (current state) - for system-config type
    /// </summary>
    [JsonPropertyName("systemConfig")]
    public SubNodeSystemConfigDto? SystemConfig { get; set; }

    /// <summary>
    /// Custom configuration (current state) - for custom-config type
    /// </summary>
    [JsonPropertyName("customConfig")]
    public Dictionary<string, object>? CustomConfig { get; set; }

    /// <summary>
    /// Update status
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Error message if update failed
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    [JsonPropertyName("lastUpdateTime")]
    public DateTimeOffset? LastUpdateTime { get; set; }
}

/// <summary>
/// SubNode device configuration wrapper containing device configs dictionary
/// </summary>
public class SubNodeDeviceConfigWrapper
{
    /// <summary>
    /// Dictionary of device configurations keyed by device type name
    /// </summary>
    [JsonPropertyName("deviceConfigs")]
    public Dictionary<string, SubNodeDeviceConfigDto>? DeviceConfigs { get; set; }
}

/// <summary>
/// Device configuration DTO matching the SubNode appsettings.json structure
/// </summary>
public class SubNodeDeviceConfigDto
{
    /// <summary>
    /// Whether this device is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Device name for identification
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device type (e.g., "adamEthernet")
    /// </summary>
    [JsonPropertyName("deviceType")]
    public string SubNodeType { get; set; } = string.Empty;

    /// <summary>
    /// Path to DTDL file (for reference only)
    /// </summary>
    [JsonPropertyName("dtdlPath")]
    public string? DtdlPath { get; set; }

    /// <summary>
    /// DTDL interface object (auto-generated or loaded from file).
    /// Contains @context, @id, @type, and contents for the device.
    /// </summary>
    [JsonPropertyName("dtdl")]
    public object? Dtdl { get; set; }

    /// <summary>
    /// Device capabilities
    /// </summary>
    [JsonPropertyName("deviceCapabilities")]
    public SubNodeDeviceCapabilitiesDto? DeviceCapabilities { get; set; }

    /// <summary>
    /// Communication settings
    /// </summary>
    [JsonPropertyName("communication")]
    public Dictionary<string, object>? Communication { get; set; }

    /// <summary>
    /// Sensor configurations
    /// </summary>
    [JsonPropertyName("sensors")]
    public List<SubNodeSensorConfigDto>? Sensors { get; set; }

    /// <summary>
    /// Background task periods (milliseconds)
    /// </summary>
    [JsonPropertyName("periods")]
    public SubNodePeriodsDto? Periods { get; set; }
}

/// <summary>
/// Background task periods DTO
/// </summary>
public class SubNodePeriodsDto
{
    /// <summary>
    /// Telemetry reading period (ms)
    /// </summary>
    [JsonPropertyName("readTelemetry")]
    public int ReadTelemetry { get; set; }

    /// <summary>
    /// Telemetry sending period (ms)
    /// </summary>
    [JsonPropertyName("sendTelemetry")]
    public int SendTelemetry { get; set; }

    /// <summary>
    /// Health reporting period (ms)
    /// </summary>
    [JsonPropertyName("reportHealth")]
    public int ReportHealth { get; set; }

    /// <summary>
    /// Configuration sync/report period (ms).
    /// When enabled (> 0), periodically reports device configuration to cloud.
    /// </summary>
    [JsonPropertyName("reportConfiguration")]
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
    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// SubNode software version
    /// </summary>
    [JsonPropertyName("subNodeSwVersion")]
    public string? SubNodeSwVersion { get; set; }

    /// <summary>
    /// Additional device info
    /// </summary>
    [JsonPropertyName("deviceInfo")]
    public Dictionary<string, object>? DeviceInfo { get; set; }
}

/// <summary>
/// Sensor configuration DTO matching the SubNode appsettings.json structure
/// </summary>
public class SubNodeSensorConfigDto
{
    /// <summary>
    /// Sensor name/identifier
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Digital Twin Model Identifier
    /// </summary>
    [JsonPropertyName("dtmi")]
    public string? Dtmi { get; set; }

    /// <summary>
    /// Sensor group (AI, DI, DO, AO, etc.)
    /// </summary>
    [JsonPropertyName("sensorGroup")]
    public string? SensorGroup { get; set; }

    /// <summary>
    /// Protocol-specific parameters
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Sensor runtime configuration
    /// </summary>
    [JsonPropertyName("config")]
    public SubNodeSensorRuntimeConfigDto? Config { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    [JsonPropertyName("metadata")]
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
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Polling interval in milliseconds
    /// </summary>
    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 1000;

    /// <summary>
    /// Measurement unit
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>
    /// Transform pipeline configuration
    /// </summary>
    [JsonPropertyName("transformPipeline")]
    public List<SubNodeTransformConfigDto>? TransformPipeline { get; set; }

    /// <summary>
    /// DSP filter pipeline configuration
    /// </summary>
    [JsonPropertyName("dspPipeline")]
    public List<SubNodeDspFilterConfigDto>? DspPipeline { get; set; }

    /// <summary>
    /// Threshold configuration
    /// </summary>
    [JsonPropertyName("thresholds")]
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
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this transform is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Transform-specific parameters
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// DSP filter configuration DTO
/// </summary>
public class SubNodeDspFilterConfigDto
{
    /// <summary>
    /// Filter type (e.g., "movingAverage", "kalman")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this filter is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Filter-specific parameters
    /// </summary>
    [JsonPropertyName("parameters")]
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
    [JsonPropertyName("upperCritical")]
    public double? UpperCritical { get; set; }

    /// <summary>
    /// Upper warning threshold
    /// </summary>
    [JsonPropertyName("upperWarning")]
    public double? UpperWarning { get; set; }

    /// <summary>
    /// Lower warning threshold
    /// </summary>
    [JsonPropertyName("lowerWarning")]
    public double? LowerWarning { get; set; }

    /// <summary>
    /// Lower critical threshold
    /// </summary>
    [JsonPropertyName("lowerCritical")]
    public double? LowerCritical { get; set; }
}

/// <summary>
/// System configuration DTO for system-config type.
/// Contains Serilog logging configuration and WedaNode (NATS) connection settings.
/// </summary>
public class SubNodeSystemConfigDto
{
    /// <summary>
    /// Serilog logging configuration
    /// </summary>
    [JsonPropertyName("serilog")]
    public Dictionary<string, object>? Serilog { get; set; }

    /// <summary>
    /// WedaNode (NATS) connection settings.
    /// Note: Changes to WedaNode require application restart.
    /// </summary>
    [JsonPropertyName("wedaNode")]
    public SubNodeWedaNodeConfigDto? WedaNode { get; set; }
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
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// Connection name
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Authentication strategy (None, UserPassword, Token, TlsCert, CredFile)
    /// </summary>
    [JsonPropertyName("authStrategy")]
    public string? AuthStrategy { get; set; }

    /// <summary>
    /// Username for UserPassword authentication
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    /// <summary>
    /// Password for UserPassword authentication
    /// </summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    /// <summary>
    /// Token for Token authentication
    /// </summary>
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    /// <summary>
    /// Credential file path for CredFile authentication
    /// </summary>
    [JsonPropertyName("credFile")]
    public string? CredFile { get; set; }

    /// <summary>
    /// Serializer type (json, protobuf, default)
    /// </summary>
    [JsonPropertyName("serializerType")]
    public string? SerializerType { get; set; }
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
}

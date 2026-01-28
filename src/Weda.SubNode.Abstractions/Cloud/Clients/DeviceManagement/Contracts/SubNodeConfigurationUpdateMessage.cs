using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Custom JsonConverter for SubNodeDesiredConfigSections.
/// Preserves raw JSON for devicecfg during deserialization.
/// </summary>
public class SubNodeDesiredConfigSectionsConverter : JsonConverter<SubNodeDesiredConfigSections>
{
    public override SubNodeDesiredConfigSections? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var result = new SubNodeDesiredConfigSections();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            switch (prop.Name.ToLowerInvariant())
            {
                case "systemcfg":
                    result.SystemCfg = JsonSerializer.Deserialize<SubNodeSystemCfgDto>(prop.Value.GetRawText(), options);
                    break;
                case "devicecfg":
                    result.DeviceCfg = JsonSerializer.Deserialize<SubNodeDeviceCfgDto>(prop.Value.GetRawText(), options);
                    result.RawDeviceCfg = prop.Value.Clone(); // Preserve raw JSON
                    break;
                case "customcfg":
                    result.CustomCfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(prop.Value.GetRawText(), options);
                    break;
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, SubNodeDesiredConfigSections value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.SystemCfg != null)
        {
            writer.WritePropertyName("systemcfg");
            JsonSerializer.Serialize(writer, value.SystemCfg, options);
        }

        // Write devicecfg - prefer RawDeviceCfg if set (preserves original structure)
        if (value.RawDeviceCfg.HasValue)
        {
            writer.WritePropertyName("devicecfg");
            value.RawDeviceCfg.Value.WriteTo(writer);
        }
        else if (value.DeviceCfg != null)
        {
            writer.WritePropertyName("devicecfg");
            JsonSerializer.Serialize(writer, value.DeviceCfg, options);
        }

        if (value.CustomCfg != null)
        {
            writer.WritePropertyName("customcfg");
            JsonSerializer.Serialize(writer, value.CustomCfg, options);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// Custom JsonConverter for SubNodeReportedConfigSections.
/// When RawDeviceCfg is set, it serializes that instead of the typed DeviceCfg property.
/// </summary>
public class SubNodeReportedConfigSectionsConverter : JsonConverter<SubNodeReportedConfigSections>
{
    public override SubNodeReportedConfigSections? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For deserialization, use default behavior
        using var doc = JsonDocument.ParseValue(ref reader);
        var result = new SubNodeReportedConfigSections();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            switch (prop.Name.ToLowerInvariant())
            {
                case "systemcfg":
                    result.SystemCfg = JsonSerializer.Deserialize<SubNodeSystemCfgDto>(prop.Value.GetRawText(), options);
                    break;
                case "devicecfg":
                    result.DeviceCfg = JsonSerializer.Deserialize<SubNodeDeviceCfgDto>(prop.Value.GetRawText(), options);
                    result.RawDeviceCfg = prop.Value.Clone();
                    break;
                case "customcfg":
                    result.CustomCfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(prop.Value.GetRawText(), options);
                    break;
            }
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, SubNodeReportedConfigSections value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        // Write systemcfg if present
        if (value.SystemCfg != null)
        {
            writer.WritePropertyName("systemcfg");
            JsonSerializer.Serialize(writer, value.SystemCfg, options);
        }

        // Write devicecfg - prefer RawDeviceCfg if set
        if (value.RawDeviceCfg.HasValue)
        {
            writer.WritePropertyName("devicecfg");
            value.RawDeviceCfg.Value.WriteTo(writer);
        }
        else if (value.DeviceCfg != null)
        {
            writer.WritePropertyName("devicecfg");
            JsonSerializer.Serialize(writer, value.DeviceCfg, options);
        }

        // Write customcfg if present
        if (value.CustomCfg != null)
        {
            writer.WritePropertyName("customcfg");
            JsonSerializer.Serialize(writer, value.CustomCfg, options);
        }

        writer.WriteEndObject();
    }
}

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
    public string ReqSeqId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp (Unix milliseconds, 0 if not specified)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTime.UtcNow.Millisecond;

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
/// Supports raw JSON for devicecfg to preserve original structure.
/// </summary>
[JsonConverter(typeof(SubNodeDesiredConfigSectionsConverter))]
public class SubNodeDesiredConfigSections : SubNodeConfigSections
{
    /// <summary>
    /// Raw device configuration JSON.
    /// Preserved during deserialization to maintain original structure (e.g., SensorInfo).
    /// </summary>
    [JsonIgnore]
    public JsonElement? RawDeviceCfg { get; set; }
}

/// <summary>
/// Reported configuration sections from device.
/// Status, error message, and timestamp are inside DeviceCfg.Message.
/// Supports raw JSON for devicecfg to preserve original structure.
/// </summary>
[JsonConverter(typeof(SubNodeReportedConfigSectionsConverter))]
public class SubNodeReportedConfigSections : SubNodeConfigSections
{
    /// <summary>
    /// Raw device configuration JSON for reporting.
    /// When set, this takes priority over DeviceCfg during serialization.
    /// Use SetRawDeviceCfg() to set this with Message included.
    /// </summary>
    [JsonIgnore]
    public JsonElement? RawDeviceCfg { get; set; }

    /// <summary>
    /// Sets raw device config JSON with Message embedded.
    /// Creates a new JsonElement that includes the original devicecfg content plus Message.
    /// </summary>
    public void SetRawDeviceCfgWithMessage(JsonElement rawDeviceCfg, ConfigUpdateMessageDto message)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            // Copy all properties from rawDeviceCfg
            foreach (var prop in rawDeviceCfg.EnumerateObject())
            {
                prop.WriteTo(writer);
            }

            // Add Message
            writer.WritePropertyName("Message");
            writer.WriteStartObject();
            if (message.Status != null)
            {
                writer.WriteString("status", message.Status);
            }
            if (message.ErrorMessage != null)
            {
                writer.WriteString("errorMessage", message.ErrorMessage);
            }
            if (message.LastUpdateTime.HasValue)
            {
                writer.WriteString("lastUpdateTime", message.LastUpdateTime.Value);
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        RawDeviceCfg = doc.RootElement.Clone();

        // Also set DeviceCfg for in-memory access compatibility
        // Parse DeviceConfigs from raw JSON if present
        if (rawDeviceCfg.TryGetProperty("DeviceConfigs", out var deviceConfigsElement))
        {
            try
            {
                var deviceConfigs = JsonSerializer.Deserialize<Dictionary<string, SubNodeDeviceConfigDto>>(
                    deviceConfigsElement.GetRawText());
                DeviceCfg = new SubNodeDeviceCfgDto
                {
                    Message = message,
                    DeviceConfigs = deviceConfigs ?? new Dictionary<string, SubNodeDeviceConfigDto>()
                };
            }
            catch
            {
                DeviceCfg = new SubNodeDeviceCfgDto { Message = message };
            }
        }
        else
        {
            DeviceCfg ??= new SubNodeDeviceCfgDto();
            DeviceCfg.Message = message;
        }
    }
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
/// Configuration update message containing status, error, and timestamp.
/// Embedded within devicecfg for proper shadow storage.
/// </summary>
public class ConfigUpdateMessageDto
{
    /// <summary>
    /// Update status (updating, success, failed, invalid, noUpdateRequired)
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Error message if update failed or invalid
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubNodeInfoDto? SubNode { get; set; }

    /// <summary>
    /// Dictionary of device configurations keyed by device name (case-insensitive).
    /// </summary>
    [JsonPropertyName("DeviceConfigs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

    /// <summary>
    /// Configuration update message containing status and error information.
    /// This field is populated during report generation to provide update feedback.
    /// </summary>
    [JsonPropertyName("Message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ConfigUpdateMessageDto? Message { get; set; }
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubNodeDeviceCapabilitiesDto? DeviceCapabilities { get; set; }

    /// <summary>
    /// Communication settings (protocol-specific).
    /// Uses object to allow runtime manipulation.
    /// </summary>
    [JsonPropertyName("Communication")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Communication { get; set; }

    /// <summary>
    /// Sensor configurations
    /// </summary>
    [JsonPropertyName("Sensors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SubNodeSensorReportDto>? Sensors { get; set; }

    /// <summary>
    /// Background task periods (milliseconds)
    /// </summary>
    [JsonPropertyName("Periods")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DtdlPath { get; set; }

    /// <summary>
    /// DTDL interface object (auto-generated or loaded from file).
    /// Contains @context, @id, @type, and contents for the device.
    /// </summary>
    [JsonPropertyName("DtdlInterface")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model
    /// </summary>
    [JsonPropertyName("Model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; set; }

    /// <summary>
    /// SubNode software version
    /// </summary>
    [JsonPropertyName("SubNodeSwVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SubNodeSwVersion { get; set; }

    /// <summary>
    /// Additional device info
    /// </summary>
    [JsonPropertyName("DeviceInfo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Dtmi { get; set; }

    /// <summary>
    /// Sensor group (AI, DI, DO, AO, TEMP, etc.)
    /// </summary>
    [JsonPropertyName("SensorGroup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SensorGroup { get; set; }

    /// <summary>
    /// Protocol-specific parameters.
    /// Uses object to allow runtime manipulation of parameter values.
    /// </summary>
    [JsonPropertyName("Parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Sensor report configuration (sampling interval, transforms, DSP, thresholds)
    /// </summary>
    [JsonPropertyName("Report")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// DTDL-related information for the sensor (DisplayName, Description, Schema)
    /// </summary>
    [JsonPropertyName("SensorInfo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubNodeSensorInfoDto? SensorInfo { get; set; }
}

/// <summary>
/// Sensor DTDL-related information DTO
/// </summary>
public class SubNodeSensorInfoDto
{
    /// <summary>
    /// DTDL schema type for this sensor's telemetry value.
    /// Examples: "double", "integer", "boolean", "string", "float"
    /// </summary>
    [JsonPropertyName("Schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; set; }

    /// <summary>
    /// Human-readable display name for DTDL generation.
    /// </summary>
    [JsonPropertyName("DisplayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description for DTDL generation.
    /// </summary>
    [JsonPropertyName("Description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
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
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unit { get; set; }

    /// <summary>
    /// Transform pipeline configuration
    /// </summary>
    [JsonPropertyName("TransformPipeline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SubNodeTransformConfigDto>? TransformPipeline { get; set; }

    /// <summary>
    /// DSP filter pipeline configuration
    /// </summary>
    [JsonPropertyName("DspPipeline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SubNodeDspFilterConfigDto>? DspPipeline { get; set; }

    /// <summary>
    /// Threshold configuration
    /// </summary>
    [JsonPropertyName("Thresholds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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

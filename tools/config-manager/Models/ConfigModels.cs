using System.Text.Json;

namespace ConfigManager.Models;

/// <summary>
/// Response model containing all configuration files from a SubNode project.
/// Supports the new sectioned configuration structure:
/// - systemcfg.json: NATS connection settings (WedaNode)
/// - devicecfg.json: SubNode identity and device configurations
/// - customcfg.json: Application-specific custom settings
/// - .weda/subnode.registration.json: Device registration status
/// </summary>
public class ConfigData
{
    // Legacy support for appsettings.json (optional)
    public JsonElement? AppSettings { get; set; }
    public string? AppSettingsPath { get; set; }

    // New sectioned configuration files
    public SystemCfgData? SystemConfig { get; set; }
    public string? SystemConfigPath { get; set; }

    public DeviceCfgData? DeviceConfig { get; set; }
    public string? DeviceConfigPath { get; set; }

    public JsonElement? CustomConfig { get; set; }
    public string? CustomConfigPath { get; set; }

    // Registration data
    public RegistrationData? Registration { get; set; }
    public string? RegistrationPath { get; set; }

    // Config cache (deprecated, kept for backward compatibility)
    public JsonElement? ConfigCache { get; set; }
    public string? ConfigCachePath { get; set; }
}

/// <summary>
/// System configuration from systemcfg.json
/// Contains NATS connection settings
/// </summary>
public class SystemCfgData
{
    public WedaNodeSettings? WedaNode { get; set; }
}

/// <summary>
/// NATS connection settings
/// </summary>
public class WedaNodeSettings
{
    public string? Url { get; set; }
    public string? AuthStrategy { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Token { get; set; }
    public string? CredFile { get; set; }
    public string? TlsCertPath { get; set; }
    public string? TlsKeyPath { get; set; }
    public string? TlsCaPath { get; set; }
}

/// <summary>
/// Device configuration from devicecfg.json
/// Contains SubNode identity and device configurations
/// </summary>
public class DeviceCfgData
{
    public SubNodeData? SubNode { get; set; }
    public JsonElement? DeviceConfigs { get; set; }
}

/// <summary>
/// SubNode identity information
/// </summary>
public class SubNodeData
{
    public string? Name { get; set; }
    public string? SubNodeType { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SwVersion { get; set; }
}

public class RegistrationData
{
    public string? DeviceName { get; set; }
    public string? RegistrationStatus { get; set; }
    public string? DeviceId { get; set; }
    public NatsTopicAssignments? NatsTopicAssignments { get; set; }
}

public class NatsTopicAssignments
{
    public string? TelemetryTopic { get; set; }
    public string? BatchTelemetryTopic { get; set; }
    public string? HealthTopic { get; set; }
    public string? EventTopic { get; set; }
    public string? CommandTopic { get; set; }
    public string? CommandResponseTopic { get; set; }

    // New config topic structure (replaces ConfigUpdateTopic/ConfigResponseTopic)
    public string? SystemConfigDesiredTopic { get; set; }
    public string? SystemConfigReportedTopic { get; set; }
    public string? DeviceConfigDesiredTopic { get; set; }
    public string? DeviceConfigReportedTopic { get; set; }
    public string? CustomConfigDesiredTopic { get; set; }
    public string? CustomConfigReportedTopic { get; set; }
}

public class ApplyConfigRequest
{
    public string? NatsUrl { get; set; }
    public string? NatsAuthStrategy { get; set; }
    public string? NatsUsername { get; set; }
    public string? NatsPassword { get; set; }
    public string? NatsToken { get; set; }
    public string? NatsCredFile { get; set; }
    public RegistrationData? Registration { get; set; }
    public JsonElement? DeviceConfigs { get; set; }
}

public class ApplyConfigResponse
{
    public bool Success { get; set; }
    public string? Topic { get; set; }
    public string? MessageId { get; set; }
    public long Timestamp { get; set; }
    public string? Error { get; set; }
    public object? Payload { get; set; }
}

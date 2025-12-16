using System.Text.Json;

namespace ConfigManager.Models;

public class ConfigData
{
    public JsonElement? AppSettings { get; set; }
    public string? AppSettingsPath { get; set; }
    public RegistrationData? Registration { get; set; }
    public string? RegistrationPath { get; set; }
    public JsonElement? ConfigCache { get; set; }
    public string? ConfigCachePath { get; set; }
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
    public string? ConfigUpdateTopic { get; set; }
    public string? ConfigResponseTopic { get; set; }
    public string? CommandTopic { get; set; }
    public string? CommandResponseTopic { get; set; }
    public string? EventTopic { get; set; }
}

public class ApplyConfigRequest
{
    public string? NatsUrl { get; set; }
    public string? NatsUsername { get; set; }
    public string? NatsPassword { get; set; }
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

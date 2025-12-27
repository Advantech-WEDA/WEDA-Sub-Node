using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Cloud.Clients.Common;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Device registration response data
/// </summary>
public class DeviceRegistrationResponseData
{
    /// <summary>
    /// Device name
    /// </summary>
    [JsonPropertyName("deviceName")]
    public required string DeviceName { get; set; }

    /// <summary>
    /// Registration status
    /// </summary>
    [JsonPropertyName("registrationStatus")]
    public string RegistrationStatus { get; set; } = "accepted";

    /// <summary>
    /// Assigned SubNode ID (deviceUid)
    /// Format: {machineId}-{suffix} or UUID
    /// </summary>
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; set; }

    /// <summary>
    /// NATS topic assignments
    /// </summary>
    [JsonPropertyName("natsTopicAssignments")]
    public required NatsTopicAssignments NatsTopicAssignments { get; set; }
}

/// <summary>
/// Device registration response
/// </summary>
public class DeviceRegistrationResponse : Response<DeviceRegistrationResponseData>
{
}

/// <summary>
/// NATS topic assignments from Cloud
/// </summary>
public class NatsTopicAssignments
{
    // ===== Uplink Topics (SubNode → Cloud) =====

    /// <summary>
    /// Topic for sending telemetry data
    /// Format: {protoVer}.weda.dm.telemetry.{deviceName}
    /// </summary>
    [JsonPropertyName("telemetryTopic")]
    public required string TelemetryTopic { get; set; }

    /// <summary>
    /// Topic for sending batch telemetry data
    /// </summary>
    [JsonPropertyName("batchTelemetryTopic")]
    public required string BatchTelemetryTopic { get; set; }

    /// <summary>
    /// Topic for sending health reports
    /// </summary>
    [JsonPropertyName("healthTopic")]
    public required string HealthTopic { get; set; }

    /// <summary>
    /// Topic for sending events
    /// </summary>
    [JsonPropertyName("eventTopic")]
    public required string EventTopic { get; set; }

    // ===== Command Topics =====

    /// <summary>
    /// Topic for receiving command requests
    /// </summary>
    [JsonPropertyName("commandTopic")]
    public required string CommandTopic { get; set; }

    /// <summary>
    /// Topic for sending command responses
    /// </summary>
    [JsonPropertyName("commandResponseTopic")]
    public required string CommandResponseTopic { get; set; }

    // ===== System Config Topics =====

    /// <summary>
    /// Topic for receiving system config desired state (delta)
    /// </summary>
    [JsonPropertyName("systemConfigDesiredTopic")]
    public string? SystemConfigDesiredTopic { get; set; }

    /// <summary>
    /// Topic for publishing system config reported state
    /// </summary>
    [JsonPropertyName("systemConfigReportedTopic")]
    public string? SystemConfigReportedTopic { get; set; }

    // ===== Device Config Topics =====

    /// <summary>
    /// Topic for receiving device config desired state (delta)
    /// </summary>
    [JsonPropertyName("deviceConfigDesiredTopic")]
    public string? DeviceConfigDesiredTopic { get; set; }

    /// <summary>
    /// Topic for publishing device config reported state
    /// </summary>
    [JsonPropertyName("deviceConfigReportedTopic")]
    public string? DeviceConfigReportedTopic { get; set; }

    // ===== Custom Config Topics =====

    /// <summary>
    /// Topic for receiving custom config desired state (delta)
    /// </summary>
    [JsonPropertyName("customConfigDesiredTopic")]
    public string? CustomConfigDesiredTopic { get; set; }

    /// <summary>
    /// Topic for publishing custom config reported state
    /// </summary>
    [JsonPropertyName("customConfigReportedTopic")]
    public string? CustomConfigReportedTopic { get; set; }
}

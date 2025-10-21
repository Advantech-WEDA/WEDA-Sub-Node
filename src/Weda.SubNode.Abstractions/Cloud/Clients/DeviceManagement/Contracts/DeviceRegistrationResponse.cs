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
    /// <summary>
    /// Topic for sending telemetry data
    /// Format: {groupId}.weda.dm.telemetry.{deviceName}
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
    /// Topic for receiving config update requests
    /// </summary>
    [JsonPropertyName("configUpdateTopic")]
    public required string ConfigUpdateTopic { get; set; }

    /// <summary>
    /// Topic for sending config update responses
    /// </summary>
    [JsonPropertyName("configResponseTopic")]
    public required string ConfigResponseTopic { get; set; }

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

    /// <summary>
    /// Topic for sending events
    /// </summary>
    [JsonPropertyName("eventTopic")]
    public required string EventTopic { get; set; }
}

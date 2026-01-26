using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// Response from NATS $SRV.PING request.
/// Represents a NATS microservice ping response.
/// </summary>
public record NatsServicePingResponse
{
    /// <summary>
    /// The service name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The unique service instance ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// The service version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Service metadata containing additional information.
    /// </summary>
    [JsonPropertyName("metadata")]
    public NatsServiceMetadata? Metadata { get; init; }

    /// <summary>
    /// The response type identifier.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
}

/// <summary>
/// Metadata for a NATS microservice.
/// </summary>
public record NatsServiceMetadata
{
    /// <summary>
    /// The device ID associated with this service.
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; init; }

    /// <summary>
    /// Comma-separated list of available endpoints.
    /// </summary>
    [JsonPropertyName("endpoints")]
    public string? Endpoints { get; init; }

    /// <summary>
    /// The service start time in ISO 8601 format.
    /// </summary>
    [JsonPropertyName("startTime")]
    public string? StartTime { get; init; }
}
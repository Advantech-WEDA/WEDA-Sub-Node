using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Cloud.Clients.Common;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Configuration upload response data
/// </summary>
public record ConfigurationUploadResponseData
{
    /// <summary>
    /// Upload configuration status
    /// </summary>
    [JsonPropertyName("configurationStatus")]
    public string ConfigurationStatus { get; set; } = string.Empty;

    /// <summary>
    /// Error detail during registration
    /// </summary>
    public ErrorDetail? ErrorDetail { get; set; } 
}

/// <summary>
/// Configuration upload response
/// </summary>
public class ConfigurationUploadResponse : Response<ConfigurationUploadResponseData>
{
}

public record ErrorDetail(
    [property: JsonPropertyName("retryable")] bool Retryable,
    [property: JsonPropertyName("retryAfter")] bool RetryAfter);
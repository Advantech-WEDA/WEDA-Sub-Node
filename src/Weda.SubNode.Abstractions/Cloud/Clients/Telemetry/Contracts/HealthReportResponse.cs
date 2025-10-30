using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Cloud.Clients.Common;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;

/// <summary>
/// Health report response data
/// </summary>
public class HealthReportResponseData
{
    /// <summary>
    /// Report status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    /// <summary>
    /// Device ID
    /// </summary>
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; set; }
}

/// <summary>
/// Health report response
/// </summary>
public class HealthReportResponse : Response<HealthReportResponseData>
{
}

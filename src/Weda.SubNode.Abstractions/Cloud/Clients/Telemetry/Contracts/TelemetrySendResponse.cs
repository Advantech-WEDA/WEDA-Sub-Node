using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Cloud.Clients.Common;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;

/// <summary>
/// Telemetry send response data
/// </summary>
public class TelemetrySendResponseData
{
    /// <summary>
    /// Send status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "success";

    /// <summary>
    /// Number of measures sent
    /// </summary>
    [JsonPropertyName("measureCount")]
    public int MeasureCount { get; set; }
}

/// <summary>
/// Telemetry send response
/// </summary>
public class TelemetrySendResponse : Response<TelemetrySendResponseData>
{
}

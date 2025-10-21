using System.Text.Json.Serialization;
using NATS.Client.Core;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Common;

/// <summary>
/// Base class for all cloud service responses with audit information
/// </summary>
public abstract class Response<TData> : IResponse<TData>
{
    /// <summary>
    /// Request sequence ID (from request)
    /// </summary>
    [JsonPropertyName("reqSeqId")]
    public string ReqSeqId { get; set; } = string.Empty;

    /// <summary>
    /// Response sequence ID
    /// </summary>
    [JsonPropertyName("rspSeqId")]
    public string RspSeqId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Response timestamp (Unix milliseconds)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Response code (optional, 0 = success)
    /// </summary>
    [JsonPropertyName("code")]
    public int? Code { get; set; }

    /// <summary>
    /// Response message (optional)
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Response data payload (null if error)
    /// </summary>
    [JsonPropertyName("data")]
    public TData? Data { get; set; }

    /// <summary>
    /// Check if response is successful (optional)
    /// </summary>
    [JsonIgnore]
    public bool? IsSuccess => Code.HasValue ? Code.Value == 0 : null;
}
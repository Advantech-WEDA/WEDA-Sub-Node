using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Common;

/// <summary>
/// Base class for all cloud service requests with audit information
/// </summary>
public abstract class Request<TData> : IRequest<TData>
{
    /// <summary>
    /// Request sequence ID for tracking
    /// </summary>
    [JsonPropertyName("reqSeqId")]
    public string ReqSeqId { get; set; } = string.Empty;

    /// <summary>
    /// Request timestamp (Unix milliseconds)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Request data payload
    /// </summary>
    [JsonPropertyName("data")]
    public TData Data { get; set; } = default!;

    /// <summary>
    /// Create a new request with auto-generated SeqId and Timestamp
    /// </summary>
    protected static TRequest Create<TRequest>(TData data) where TRequest : Request<TData>, new()
    {
        return new TRequest
        {
            ReqSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = data
        };
    }
}

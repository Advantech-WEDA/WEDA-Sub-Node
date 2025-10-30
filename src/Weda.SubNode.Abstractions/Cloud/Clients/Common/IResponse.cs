namespace Weda.SubNode.Abstractions.Cloud.Clients.Common;

/// <summary>
/// Base interface for all cloud service responses
/// </summary>
public interface IResponse
{
    /// <summary>
    /// Request sequence ID (from request)
    /// </summary>
    string ReqSeqId { get; set; }

    /// <summary>
    /// Response sequence ID
    /// </summary>
    string RspSeqId { get; set; }

    /// <summary>
    /// Response timestamp (Unix milliseconds)
    /// </summary>
    long Timestamp { get; set; }

    /// <summary>
    /// Response code (optional, 0 = success)
    /// </summary>
    int? Code { get; set; }

    /// <summary>
    /// Response message (optional)
    /// </summary>
    string? Message { get; set; }

    /// <summary>
    /// Check if response is successful (optional)
    /// </summary>
    bool? IsSuccess { get; }
}

/// <summary>
/// Generic response interface with typed data payload
/// </summary>
public interface IResponse<TData> : IResponse
{
    /// <summary>
    /// Response data payload (null if error)
    /// </summary>
    TData? Data { get; set; }
}

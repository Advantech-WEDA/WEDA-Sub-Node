namespace Weda.SubNode.Abstractions.Cloud.Clients.Common;

/// <summary>
/// Base interface for all cloud service requests
/// </summary>
public interface IRequest
{
    /// <summary>
    /// Request sequence ID for tracking
    /// </summary>
    string ReqSeqId { get; set; }

    /// <summary>
    /// Request timestamp (Unix milliseconds)
    /// </summary>
    long Timestamp { get; set; }
}

/// <summary>
/// Generic request interface with typed data payload
/// </summary>
public interface IRequest<TData> : IRequest
{
    /// <summary>
    /// Request data payload
    /// </summary>
    TData Data { get; set; }
}

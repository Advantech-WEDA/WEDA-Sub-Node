namespace Weda.SubNode.Abstractions.Cloud.Clients.Common;

/// <summary>
/// Base interface for all cloud service messages
/// </summary>
public interface IMessage
{
    /// <summary>
    /// sequence ID for tracking
    /// </summary>
    int SeqId { get; set; }

    /// <summary>
    /// Message timestamp (Unix milliseconds)
    /// </summary>
    long Timestamp { get; set; }
}

/// <summary>
/// Generic message interface with typed data payload
/// </summary>
public interface IMessage<TData> : IMessage
{
    /// <summary>
    /// Message data payload
    /// </summary>
    TData Data { get; set; }
}

using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Common;

/// <summary>
/// Base class for all cloud service messages with audit information
/// </summary>
public abstract class Message<TData> : IMessage<TData>
{
    /// <summary>
    /// Message sequence ID for tracking
    /// </summary>
    [JsonPropertyName("seqId")]
    public int SeqId { get; set; } = 0;

    /// <summary>
    /// Message timestamp (Unix milliseconds)
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Message data payload
    /// </summary>
    [JsonPropertyName("data")]
    public TData Data { get; set; } = default!;

    /// <summary>
    /// Create a new message with auto-generated SeqId and Timestamp
    /// </summary>
    protected static TMessage Create<TMessage>(TData data) where TMessage : Message<TData>, new()
    {
        return new TMessage
        {
            SeqId = MessageSeqIdGenerator<TMessage>.GetNext(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = data
        };
    }
}

/// <summary>
/// Per-message-type sequence ID generator
/// </summary>
internal static class MessageSeqIdGenerator<TMessage>
{
    private static int _seqId = 0;

    public static int GetNext() => Interlocked.Increment(ref _seqId);
}

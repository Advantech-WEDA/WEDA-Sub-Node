namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Message received from message broker (MQTT, NATS, etc.).
/// Fired when a message is received on a subscribed topic.
/// </summary>
/// <param name="Topic">The topic the message was received on.</param>
/// <param name="Payload">The message payload.</param>
/// <param name="Timestamp">The timestamp when message was received.</param>
public sealed record MessageReceivedEvent(
    string Topic,
    byte[] Payload,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the Quality of Service level (if applicable).
    /// 0 = At most once, 1 = At least once, 2 = Exactly once
    /// </summary>
    public int QoS { get; init; } = 0;

    /// <summary>
    /// Gets whether the message was retained by the broker.
    /// </summary>
    public bool Retain { get; init; }
}

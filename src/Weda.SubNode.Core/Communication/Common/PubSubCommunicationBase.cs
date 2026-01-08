using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Communication.Common;

/// <summary>
/// Base class for Pub/Sub communication pattern implementations.
/// Suitable for protocols like MQTT, NATS, RabbitMQ where messages are published to topics and received via subscriptions.
/// </summary>
/// <typeparam name="TMessage">Message payload type (e.g., byte[] for MQTT, string for text-based protocols)</typeparam>
public abstract class PubSubCommunicationBase<TMessage>
    : CommunicationBase, IPubSubCommunication<TMessage>
{
    protected PubSubCommunicationBase(
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
    }

    /// <summary>
    /// Subscribe to a topic for receiving messages.
    /// Implementations should handle protocol-specific subscription logic.
    /// </summary>
    /// <param name="topic">Topic pattern to subscribe (supports wildcards)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if subscription successful</returns>
    public abstract Task<bool> SubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from a topic.
    /// Implementations should handle protocol-specific unsubscription logic.
    /// </summary>
    /// <param name="topic">Topic pattern to unsubscribe</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if unsubscription successful</returns>
    public abstract Task<bool> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a message to a specific topic.
    /// Implementations should handle protocol-specific publishing logic.
    /// </summary>
    /// <param name="topic">Topic to publish to</param>
    /// <param name="message">Message to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if publish successful</returns>
    public abstract Task<bool> PublishAsync(string topic, TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when a message is received on subscribed topics.
    /// Implementations should raise this event when messages arrive.
    /// </summary>
    public event EventHandler<MessageReceivedEvent<TMessage>>? MessageReceived;

    /// <summary>
    /// Helper method for implementations to raise MessageReceived event.
    /// </summary>
    /// <param name="topic">Topic the message was received on</param>
    /// <param name="message">Message payload</param>
    /// <param name="qos">Quality of Service level (if applicable)</param>
    /// <param name="retain">Whether the message was retained by the broker</param>
    protected virtual void OnMessageReceived(string topic, TMessage message, int qos = 0, bool retain = false)
    {
        var @event = new MessageReceivedEvent<TMessage>(
            Topic: topic,
            Payload: message,
            Timestamp: DateTimeOffset.UtcNow)
        {
            QoS = qos,
            Retain = retain
        };

        MessageReceived?.Invoke(this, @event);
    }
}

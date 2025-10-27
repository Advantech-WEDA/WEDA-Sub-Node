using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Message Broker communication interface for Pub/Sub protocols
/// Extends ICommunication to maintain compatibility with existing business logic
/// Suitable for MQTT, NATS, RabbitMQ, etc.
/// </summary>
public interface IMessageBroker : ICommunication
{
    /// <summary>
    /// Subscribe to a topic for receiving messages
    /// </summary>
    /// <param name="topic">Topic pattern to subscribe (supports wildcards)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if subscription successful</returns>
    Task<bool> SubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from a topic
    /// </summary>
    /// <param name="topic">Topic pattern to unsubscribe</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if unsubscription successful</returns>
    Task<bool> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a message to a specific topic
    /// </summary>
    /// <param name="topic">Topic to publish to</param>
    /// <param name="payload">Message payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if publish successful</returns>
    Task<bool> PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when a message is received on subscribed topics
    /// </summary>
    event EventHandler<MessageReceivedEvent>? MessageReceived;
}

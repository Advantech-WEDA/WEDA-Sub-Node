using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Base communication interface for all communication patterns.
/// Only contains connection management functionality.
/// Used by DeviceConnectionManager for connection lifecycle management.
///
/// Specific communication patterns extend this interface:
/// - IRequestResponseCommunication: for TCP, HTTP, Modbus RTU (request-reply)
/// - IPubSubCommunication: for MQTT, NATS, RabbitMQ (pub/sub)
/// - IStreamingCommunication: for WebSocket, gRPC (bidirectional streaming)
/// </summary>
public interface ICommunication : IDisposable
{
    /// <summary>
    /// Connection settings (retry, timeout, etc.)
    /// </summary>
    ConnectionSettings Settings { get; }

    /// <summary>
    /// Current communication state
    /// </summary>
    CommunicationState State { get; }

    /// <summary>
    /// Is connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to communication endpoint
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from communication endpoint
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event: Communication state changed
    /// </summary>
    event EventHandler<ConnectionStateChangedEvent>? StateChanged;
}

/// <summary>
/// Request-Response communication pattern (TCP, HTTP, Modbus RTU, etc.)
/// Suitable for synchronous request-reply interactions.
/// </summary>
/// <typeparam name="TRequest">Request message type</typeparam>
/// <typeparam name="TResponse">Response message type</typeparam>
public interface IRequestResponseCommunication<TRequest, TResponse> : ICommunication
{
    /// <summary>
    /// Send a request and wait for response
    /// </summary>
    /// <param name="request">Request message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response message</returns>
    Task<TResponse> RequestAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pub/Sub (Message Broker) communication pattern (MQTT, NATS, RabbitMQ, etc.)
/// Suitable for event-driven, topic-based messaging.
/// </summary>
/// <typeparam name="TMessage">Message type</typeparam>
public interface IPubSubCommunication<TMessage> : ICommunication
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
    /// <param name="message">Message to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if publish successful</returns>
    Task<bool> PublishAsync(string topic, TMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when a message is received on subscribed topics
    /// </summary>
    event EventHandler<MessageReceivedEvent<TMessage>>? MessageReceived;
}

/// <summary>
/// Streaming communication pattern (WebSocket, gRPC bidirectional streaming, etc.)
/// Suitable for continuous bidirectional data flow.
/// </summary>
/// <typeparam name="TRequest">Request/outgoing message type</typeparam>
/// <typeparam name="TResponse">Response/incoming message type</typeparam>
public interface IStreamingCommunication<TRequest, TResponse> : ICommunication
{
    /// <summary>
    /// Establish a bidirectional stream
    /// </summary>
    /// <param name="requests">Outgoing message stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Incoming message stream</returns>
    IAsyncEnumerable<TResponse> StreamAsync(
        IAsyncEnumerable<TRequest> requests,
        CancellationToken cancellationToken = default);
}

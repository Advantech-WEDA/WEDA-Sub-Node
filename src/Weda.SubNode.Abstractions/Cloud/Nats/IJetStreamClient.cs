using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// Interface for JetStream client operations
/// </summary>
public interface IJetStreamClient : IDisposable
{
    /// <summary>
    /// NATS connection instance
    /// </summary>
    INatsConnection? NatsConnection { get; }

    /// <summary>
    /// Maximum number of messages
    /// </summary>
    int MaxMsgs { get; }

    /// <summary>
    /// Check if connected to NATS server
    /// </summary>
    bool IsConnected();

    /// <summary>
    /// Try to connect to NATS server
    /// </summary>
    Task TryConnectAsync();

    /// <summary>
    /// Disconnect from NATS server
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Publish message to subject using NATS connection (fire-and-forget)
    /// </summary>
    Task NatsPublishAsync<T>(string subject, T? data, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish message to subject using JetStream (with ack)
    /// </summary>
    Task PublishAsync<T>(string subject, T? data, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Request-reply pattern (returns string response)
    /// </summary>
    Task<string?> RequestAsync<T>(string subject, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Request-reply pattern (returns typed response)
    /// </summary>
    Task<TResponse?> RequestAsync<TRequest, TResponse>(string subject, TRequest data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consume messages asynchronously from a consumer
    /// </summary>
    Task ConsumeAsync(INatsJSConsumer consumer, Func<byte[], string, Task> handler, bool autoAck = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a stream consumer asynchronously
    /// </summary>
    Task<INatsJSConsumer> CreateStreamConsumerAsync(ConsumerConfiguration consumerCfg, StreamConfiguration cfgOptions);
}

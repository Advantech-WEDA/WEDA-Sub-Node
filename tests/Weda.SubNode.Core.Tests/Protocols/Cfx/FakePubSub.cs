using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

/// <summary>
/// In-memory <see cref="IPubSub"/> that records subscriptions and lets a test inject messages.
/// </summary>
internal sealed class FakePubSub : IPubSub
{
    private readonly bool _failSubscribe;
    private readonly bool _failUnsubscribe;

    public FakePubSub(bool failSubscribe = false, bool failUnsubscribe = false)
    {
        _failSubscribe = failSubscribe;
        _failUnsubscribe = failUnsubscribe;
    }

    public List<string> Subscribed { get; } = [];

    public List<string> Unsubscribed { get; } = [];

    public List<(string Topic, byte[] Payload)> Published { get; } = [];

    public ConnectionSettings Settings { get; } = new();

    public CommunicationState State => CommunicationState.Connected;

    public bool IsConnected => true;

    public event EventHandler<MessageReceivedEvent<byte[]>>? MessageReceived;

    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    /// <summary>Number of handlers currently attached to <see cref="MessageReceived"/>.</summary>
    public int MessageReceivedHandlerCount => MessageReceived?.GetInvocationList().Length ?? 0;

    public Task<bool> SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_failSubscribe)
        {
            throw new IOException($"Broker refused subscription to '{topic}'.");
        }

        Subscribed.Add(topic);
        return Task.FromResult(true);
    }

    public Task<bool> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_failUnsubscribe)
        {
            throw new IOException($"Broker refused unsubscribe from '{topic}'.");
        }

        Unsubscribed.Add(topic);
        return Task.FromResult(true);
    }

    public Task<bool> PublishAsync(string topic, byte[] message, CancellationToken cancellationToken = default)
    {
        Published.Add((topic, message));
        return Task.FromResult(true);
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Delivers a message to every attached handler, as the broker would.</summary>
    public void Deliver(string topic, byte[] payload) =>
        MessageReceived?.Invoke(this, new MessageReceivedEvent<byte[]>(topic, payload, DateTimeOffset.UtcNow));

    public void Dispose()
    {
        StateChanged = null;
        MessageReceived = null;
    }
}

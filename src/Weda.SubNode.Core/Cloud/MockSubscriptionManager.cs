using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;

namespace Weda.SubNode.Core.Cloud;

/// <summary>
/// Mock implementation of ISubscriptionManager for testing.
/// Records subscriptions but doesn't actually connect to any messaging system.
/// </summary>
public sealed class MockSubscriptionManager : ISubscriptionManager
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, SubscriptionInfo> _subscriptions = new();
    private bool _disposed;

    public MockSubscriptionManager(ILogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<SubscriptionInfo> ActiveSubscriptions =>
        _subscriptions.Values
            .Where(s => s.IsActive)
            .ToList()
            .AsReadOnly();

    public bool HasSubscription(string topic) =>
        _subscriptions.TryGetValue(topic, out var sub) && sub.IsActive;

    public SubscriptionInfo? GetSubscription(string topic) =>
        _subscriptions.TryGetValue(topic, out var sub) ? sub : null;

    public IReadOnlyCollection<SubscriptionInfo> GetSubscriptionsByType(SubscriptionType type) =>
        _subscriptions.Values
            .Where(s => s.IsActive && s.Type == type)
            .ToList()
            .AsReadOnly();

    public Task<SubscriptionInfo> SubscribeAsync<TMessage>(
        string topic,
        Func<TMessage, Task> handler,
        SubscriptionType subscriptionType,
        string? responseTopic = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var info = new SubscriptionInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Topic = topic,
            ResponseTopic = responseTopic,
            Type = subscriptionType,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _subscriptions[topic] = info;

        _logger.LogInformation(
            "Mock subscription created: Topic={Topic}, Type={Type}, ResponseTopic={ResponseTopic}",
            topic, subscriptionType, responseTopic);

        return Task.FromResult(info);
    }

    public Task<bool> UnsubscribeAsync(string topic)
    {
        if (_subscriptions.TryRemove(topic, out var info))
        {
            info.IsActive = false;
            _logger.LogInformation("Mock subscription removed: Topic={Topic}", topic);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task<int> UnsubscribeAllAsync(SubscriptionType subscriptionType)
    {
        var topicsToRemove = _subscriptions
            .Where(kvp => kvp.Value.Type == subscriptionType)
            .Select(kvp => kvp.Key)
            .ToList();

        var count = 0;
        foreach (var topic in topicsToRemove)
        {
            if (await UnsubscribeAsync(topic))
                count++;
        }

        return count;
    }

    public async Task UnsubscribeAllAsync()
    {
        var topics = _subscriptions.Keys.ToList();
        foreach (var topic in topics)
        {
            await UnsubscribeAsync(topic);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await UnsubscribeAllAsync();
        _logger.LogInformation("MockSubscriptionManager disposed");
    }
}

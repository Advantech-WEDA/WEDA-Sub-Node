using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Net;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;

namespace Weda.SubNode.Cloud.Subscriptions;

/// <summary>
/// Cloud-based subscription manager that supports dynamic topic subscriptions.
/// Thread-safe implementation using ConcurrentDictionary.
/// Internal implementation detail - uses NATS for messaging.
/// </summary>
public sealed class CloudSubscriptionManager : ISubscriptionManager
{
    private readonly NatsClient _client;
    private readonly ILogger<CloudSubscriptionManager> _logger;
    private readonly ConcurrentDictionary<string, ManagedSubscription> _subscriptions = new();
    private bool _disposed;

    public CloudSubscriptionManager(
        NatsClient client,
        ILogger<CloudSubscriptionManager>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLogger<CloudSubscriptionManager>.Instance;
    }

    public IReadOnlyCollection<SubscriptionInfo> ActiveSubscriptions =>
        _subscriptions.Values
            .Where(s => s.Info.IsActive)
            .Select(s => s.Info)
            .ToList()
            .AsReadOnly();

    public bool HasSubscription(string topic) =>
        _subscriptions.TryGetValue(topic, out var sub) && sub.Info.IsActive;

    public SubscriptionInfo? GetSubscription(string topic) =>
        _subscriptions.TryGetValue(topic, out var sub) ? sub.Info : null;

    public IReadOnlyCollection<SubscriptionInfo> GetSubscriptionsByType(SubscriptionType type) =>
        _subscriptions.Values
            .Where(s => s.Info.IsActive && s.Info.Type == type)
            .Select(s => s.Info)
            .ToList()
            .AsReadOnly();

    public async Task<SubscriptionInfo> SubscribeAsync<TMessage>(
        string topic,
        Func<TMessage, Task> handler,
        SubscriptionType subscriptionType,
        string? responseTopic = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(subscriptionType);

        ObjectDisposedException.ThrowIf(_disposed, this);

        // Remove existing subscription if present
        if (_subscriptions.TryGetValue(topic, out var existing))
        {
            _logger.LogInformation("Replacing existing subscription for topic: {Topic}", topic);
            await RemoveSubscriptionAsync(existing);
        }

        var info = new SubscriptionInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Topic = topic,
            ResponseTopic = responseTopic,
            Type = subscriptionType,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        var cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        // Start subscription via messaging client
        var subscription = _client.SubscribeAsync<TMessage>(
            subject: topic,
            cancellationToken: linkedCts.Token);

        // Start background processing task
        var processingTask = Task.Run(async () =>
        {
            _logger.LogInformation(
                "Subscription started: Topic={Topic}, Type={Type}, ResponseTopic={ResponseTopic}",
                topic, subscriptionType, responseTopic);

            try
            {
                await foreach (var msg in subscription.WithCancellation(linkedCts.Token))
                {
                    try
                    {
                        if (msg.Data is null)
                        {
                            _logger.LogWarning("Received null data from topic: {Topic}", topic);
                            continue;
                        }

                        info.MessageCount++;
                        info.LastMessageAt = DateTimeOffset.UtcNow;
                        _logger.LogDebug(
                            "Message received: Topic={Topic}, Count={Count}",
                            topic, info.MessageCount);

                        await handler(msg.Data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from topic: {Topic}", topic);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Subscription cancelled: Topic={Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription error: Topic={Topic}", topic);
            }
            finally
            {
                info.IsActive = false;
                _logger.LogInformation("Subscription ended: Topic={Topic}", topic);
            }
        }, linkedCts.Token);

        var managedSub = new ManagedSubscription(info, cts, linkedCts, processingTask);
        _subscriptions[topic] = managedSub;

        _logger.LogInformation(
            "Subscription created: Id={Id}, Topic={Topic}, Type={Type}",
            info.Id, topic, subscriptionType);

        return info;
    }

    /// <summary>
    /// Subscribe to a topic with a handler that receives both message and subject.
    /// Use this overload when you need to parse routing information from the subject.
    /// </summary>
    public async Task<SubscriptionInfo> SubscribeAsync<TMessage>(
        string topic,
        Func<TMessage, string, Task> handler,
        SubscriptionType subscriptionType,
        string? responseTopic = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(subscriptionType);

        ObjectDisposedException.ThrowIf(_disposed, this);

        // Remove existing subscription if present
        if (_subscriptions.TryGetValue(topic, out var existing))
        {
            _logger.LogInformation("Replacing existing subscription for topic: {Topic}", topic);
            await RemoveSubscriptionAsync(existing);
        }

        var info = new SubscriptionInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Topic = topic,
            ResponseTopic = responseTopic,
            Type = subscriptionType,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        var cts = new CancellationTokenSource();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        // Start subscription via messaging client
        var subscription = _client.SubscribeAsync<TMessage>(
            subject: topic,
            cancellationToken: linkedCts.Token);

        // Start background processing task
        var processingTask = Task.Run(async () =>
        {
            _logger.LogInformation(
                "Subscription started: Topic={Topic}, Type={Type}, ResponseTopic={ResponseTopic}",
                topic, subscriptionType, responseTopic);

            try
            {
                await foreach (var msg in subscription.WithCancellation(linkedCts.Token))
                {
                    try
                    {
                        if (msg.Data is null)
                        {
                            _logger.LogWarning("Received null data from topic: {Topic}", topic);
                            continue;
                        }

                        info.MessageCount++;
                        info.LastMessageAt = DateTimeOffset.UtcNow;
                        _logger.LogDebug(
                            "Message received: Topic={Topic}, Subject={Subject}, Count={Count}",
                            topic, msg.Subject, info.MessageCount);

                        // Pass both message data and subject to handler
                        await handler(msg.Data, msg.Subject);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from topic: {Topic}", topic);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Subscription cancelled: Topic={Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription error: Topic={Topic}", topic);
            }
            finally
            {
                info.IsActive = false;
                _logger.LogInformation("Subscription ended: Topic={Topic}", topic);
            }
        }, linkedCts.Token);

        var managedSub = new ManagedSubscription(info, cts, linkedCts, processingTask);
        _subscriptions[topic] = managedSub;

        _logger.LogInformation(
            "Subscription created: Id={Id}, Topic={Topic}, Type={Type}",
            info.Id, topic, subscriptionType);

        return info;
    }

    public async Task<bool> UnsubscribeAsync(string topic)
    {
        if (!_subscriptions.TryRemove(topic, out var subscription))
        {
            _logger.LogDebug("Subscription not found for topic: {Topic}", topic);
            return false;
        }

        await RemoveSubscriptionAsync(subscription);
        _logger.LogInformation("Unsubscribed from topic: {Topic}", topic);
        return true;
    }

    public async Task<int> UnsubscribeAllAsync(SubscriptionType subscriptionType)
    {
        var topicsToRemove = _subscriptions
            .Where(kvp => kvp.Value.Info.Type == subscriptionType)
            .Select(kvp => kvp.Key)
            .ToList();

        var count = 0;
        foreach (var topic in topicsToRemove)
        {
            if (await UnsubscribeAsync(topic))
                count++;
        }

        _logger.LogInformation(
            "Unsubscribed {Count} subscriptions of type: {Type}",
            count, subscriptionType);

        return count;
    }

    public async Task UnsubscribeAllAsync()
    {
        var topics = _subscriptions.Keys.ToList();
        foreach (var topic in topics)
        {
            await UnsubscribeAsync(topic);
        }

        _logger.LogInformation("Unsubscribed from all {Count} topics", topics.Count);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing CloudSubscriptionManager...");

        await UnsubscribeAllAsync();

        _logger.LogInformation("CloudSubscriptionManager disposed");
    }

    private async Task RemoveSubscriptionAsync(ManagedSubscription subscription)
    {
        subscription.Info.IsActive = false;

        try
        {
            await subscription.Cts.CancelAsync();
        }
        catch
        {
            // Ignore cancellation errors
        }

        // Wait briefly for the task to complete
        try
        {
            await subscription.ProcessingTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Subscription task did not complete in time: Topic={Topic}",
                subscription.Info.Topic);
        }
        catch
        {
            // Ignore other errors during cleanup
        }
        finally
        {
            subscription.Cts.Dispose();
            subscription.LinkedCts.Dispose();
        }
    }

    private sealed record ManagedSubscription(
        SubscriptionInfo Info,
        CancellationTokenSource Cts,
        CancellationTokenSource LinkedCts,
        Task ProcessingTask);
}
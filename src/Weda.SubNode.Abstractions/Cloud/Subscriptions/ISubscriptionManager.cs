namespace Weda.SubNode.Abstractions.Cloud.Subscriptions;

/// <summary>
/// Manages multiple topic subscriptions with dynamic add/remove capability.
/// Supports configuration updates (system, device, custom) and command subscriptions.
/// </summary>
public interface ISubscriptionManager : IAsyncDisposable
{
    /// <summary>
    /// Gets all active subscriptions.
    /// </summary>
    IReadOnlyCollection<SubscriptionInfo> ActiveSubscriptions { get; }

    /// <summary>
    /// Checks if a subscription exists for the given topic.
    /// </summary>
    bool HasSubscription(string topic);

    /// <summary>
    /// Gets subscription info by topic.
    /// </summary>
    SubscriptionInfo? GetSubscription(string topic);

    /// <summary>
    /// Gets all subscriptions of a specific type.
    /// </summary>
    IReadOnlyCollection<SubscriptionInfo> GetSubscriptionsByType(SubscriptionType type);

    /// <summary>
    /// Subscribe to a topic with a message handler.
    /// If subscription already exists, it will be replaced.
    /// </summary>
    /// <typeparam name="TMessage">The message type to deserialize.</typeparam>
    /// <param name="topic">The topic to subscribe to.</param>
    /// <param name="handler">The async handler for received messages.</param>
    /// <param name="subscriptionType">The type of subscription (for categorization).</param>
    /// <param name="responseTopic">Optional response topic for request-response patterns.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The subscription info.</returns>
    Task<SubscriptionInfo> SubscribeAsync<TMessage>(
        string topic,
        Func<TMessage, Task> handler,
        SubscriptionType subscriptionType,
        string? responseTopic = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from a topic.
    /// </summary>
    /// <param name="topic">The topic to unsubscribe from.</param>
    /// <returns>True if subscription was removed, false if not found.</returns>
    Task<bool> UnsubscribeAsync(string topic);

    /// <summary>
    /// Unsubscribe from all topics of a specific type.
    /// </summary>
    /// <param name="subscriptionType">The subscription type to remove.</param>
    /// <returns>Number of subscriptions removed.</returns>
    Task<int> UnsubscribeAllAsync(SubscriptionType subscriptionType);

    /// <summary>
    /// Unsubscribe from all topics.
    /// </summary>
    Task UnsubscribeAllAsync();
}

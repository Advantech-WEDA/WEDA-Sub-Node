namespace Weda.SubNode.Abstractions.Cloud.Subscriptions;

/// <summary>
/// Information about an active subscription.
/// This is a snapshot of subscription state - values may change over time.
/// </summary>
public sealed class SubscriptionInfo
{
    /// <summary>
    /// Unique identifier for this subscription.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The topic being subscribed to (desired/delta topic).
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The response topic for publishing reports (reported topic).
    /// </summary>
    public string? ResponseTopic { get; init; }

    /// <summary>
    /// The type of subscription.
    /// </summary>
    public required SubscriptionType Type { get; init; }

    /// <summary>
    /// When the subscription was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Whether the subscription is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Number of messages received on this subscription.
    /// </summary>
    public long MessageCount { get; set; }

    /// <summary>
    /// Last message received timestamp.
    /// </summary>
    public DateTimeOffset? LastMessageAt { get; set; }
}

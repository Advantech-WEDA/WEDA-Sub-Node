namespace Weda.SubNode.Abstractions.Cloud.Subscriptions;

/// <summary>
/// Represents a configuration subscription with desired (subscribe) and reported (publish) topics.
/// </summary>
public sealed class ConfigSubscription
{
    /// <summary>
    /// The subscription type identifier.
    /// </summary>
    public required SubscriptionType Type { get; init; }

    /// <summary>
    /// The topic to subscribe for receiving desired state (delta) from cloud.
    /// </summary>
    public required string DesiredTopic { get; init; }

    /// <summary>
    /// The topic to publish reported state back to cloud.
    /// </summary>
    public required string ReportedTopic { get; init; }

    /// <summary>
    /// Whether this subscription is configured (both topics are set).
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrEmpty(DesiredTopic) && !string.IsNullOrEmpty(ReportedTopic);
}
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

namespace Weda.SubNode.Abstractions.Cloud.Subscriptions;

/// <summary>
/// Extension methods for NatsTopicAssignments to get config subscriptions.
/// </summary>
public static class NatsTopicAssignmentsExtensions
{
    /// <summary>
    /// Gets all configured config subscriptions (system, device, custom).
    /// Only returns subscriptions where both desired and reported topics are configured.
    /// </summary>
    public static IEnumerable<ConfigSubscription> GetConfigSubscriptions(this NatsTopicAssignments topics)
    {
        ArgumentNullException.ThrowIfNull(topics);

        if (!string.IsNullOrEmpty(topics.SystemConfigDesiredTopic) &&
            !string.IsNullOrEmpty(topics.SystemConfigReportedTopic))
        {
            yield return new ConfigSubscription
            {
                Type = SubscriptionType.Create("system-config"),
                DesiredTopic = topics.SystemConfigDesiredTopic,
                ReportedTopic = topics.SystemConfigReportedTopic
            };
        }

        if (!string.IsNullOrEmpty(topics.DeviceConfigDesiredTopic) &&
            !string.IsNullOrEmpty(topics.DeviceConfigReportedTopic))
        {
            yield return new ConfigSubscription
            {
                Type = SubscriptionType.Create("device-config"),
                DesiredTopic = topics.DeviceConfigDesiredTopic,
                ReportedTopic = topics.DeviceConfigReportedTopic
            };
        }

        if (!string.IsNullOrEmpty(topics.CustomConfigDesiredTopic) &&
            !string.IsNullOrEmpty(topics.CustomConfigReportedTopic))
        {
            yield return new ConfigSubscription
            {
                Type = SubscriptionType.Create("custom-config"),
                DesiredTopic = topics.CustomConfigDesiredTopic,
                ReportedTopic = topics.CustomConfigReportedTopic
            };
        }
    }

    /// <summary>
    /// Gets a specific config subscription by type.
    /// </summary>
    public static ConfigSubscription? GetConfigSubscription(this NatsTopicAssignments topics, SubscriptionType type)
    {
        return topics.GetConfigSubscriptions().FirstOrDefault(s => s.Type == type);
    }
}

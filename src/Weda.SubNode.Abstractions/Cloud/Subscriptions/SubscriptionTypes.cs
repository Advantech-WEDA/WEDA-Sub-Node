namespace Weda.SubNode.Abstractions.Cloud.Subscriptions;

/// <summary>
/// Well-known subscription types for SubNode shadow configurations.
/// </summary>
public static class SubscriptionTypes
{
    /// <summary>
    /// System configuration (systemConfigDesiredTopic → systemConfigReportedTopic).
    /// For uplink configs like Serilog, WedaNode settings.
    /// </summary>
    public static readonly SubscriptionType SystemConfig = SubscriptionType.Create("system-config");

    /// <summary>
    /// Device configuration (deviceConfigDesiredTopic → deviceConfigReportedTopic).
    /// For downlink configs like Sensor, Device settings.
    /// </summary>
    public static readonly SubscriptionType DeviceConfig = SubscriptionType.Create("device-config");

    /// <summary>
    /// Custom configuration (customConfigDesiredTopic → customConfigReportedTopic).
    /// For user-defined shadow configurations.
    /// </summary>
    public static readonly SubscriptionType CustomConfig = SubscriptionType.Create("custom-config");

    /// <summary>
    /// Command subscription (commandTopic → commandResponseTopic).
    /// </summary>
    public static readonly SubscriptionType Command = SubscriptionType.Create("command");

    /// <summary>
    /// Custom subscription for user-defined topics.
    /// </summary>
    public static readonly SubscriptionType Custom = SubscriptionType.Create("custom");
}

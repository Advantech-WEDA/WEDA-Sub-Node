using ErrorOr;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Cloud;

/// <summary>
/// WedaNode interface (abstraction for message brokers)
/// Provides device registration, telemetry, and subscription management
/// </summary>
public interface IWedaCloudService : IDisposable
{
    /// <summary>
    /// Gets the subscription manager for dynamic topic subscriptions.
    /// Use this to subscribe to system config, device config, custom config, and command topics.
    /// </summary>
    ISubscriptionManager Subscriptions { get; }

    /// <summary>
    /// Is connected to cloud
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to cloud service
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fired when the underlying connection is re-established after a drop.
    /// Not fired for the initial connection. Subscribers can use this to
    /// republish state that may have changed while offline.
    /// </summary>
    event Func<Task>? ConnectionRestored;

    /// <summary>
    /// Disconnect from cloud service
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    // ===== Internal methods (used by DeviceBase template method pattern) =====
    // These methods should not be called directly by users

    /// <summary>
    /// Get or register device ID (checks repository first, then registers with Cloud if needed)
    /// INTERNAL USE ONLY - Called by DeviceBase.InitializeAsync
    /// </summary>
    Task<string?> GetOrRegisterDeviceIdAsync(DeviceInfo info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configure NATS topic assignments for a specific device.
    /// Must be called after device registration to enable telemetry transmission.
    /// Each device has its own topic assignments.
    /// INTERNAL USE ONLY - Called by DeviceInitializer after registration
    /// </summary>
    /// <param name="deviceName">The device name (used as key for topic lookup)</param>
    /// <param name="topicAssignments">The NATS topic assignments for this device</param>
    void ConfigureTopics(string deviceName, NatsTopicAssignments topicAssignments);

    /// <summary>
    /// Get topic assignments for a specific device.
    /// Returns null if the device has not been configured.
    /// </summary>
    /// <param name="deviceName">The device name</param>
    /// <returns>Topic assignments or null if not configured</returns>
    NatsTopicAssignments? GetTopics(string deviceName);

    /// <summary>
    /// Upload device configuration to cloud
    /// INTERNAL USE ONLY - Called by DeviceBase.InitializeAsync
    /// </summary>
    Task<ErrorOr<bool>> UploadDeviceConfigurationsAsync(DeviceConfigurations configurations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send telemetry to cloud
    /// Topic: &lt;protoVer&gt;.&lt;groupID&gt;.&lt;deviceId&gt;.dm.dt.update.rl
    /// </summary>
    Task<bool> SendTelemetryAsync(string deviceId, TelemetryData telemetryData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Report device health to cloud
    /// </summary>
    Task<bool> ReportHealthAsync(string deviceId, DeviceHealth health, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to configuration updates from cloud
    /// Returns IDisposable to unsubscribe
    /// </summary>
    Task<IDisposable> SubscribeConfigurationUpdatesAsync(
        string deviceId,
        Func<UpdateConfigurationEvent, Task> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to commands from cloud
    /// Returns IDisposable to unsubscribe
    /// </summary>
    Task<IDisposable> SubscribeCommandsAsync(
        string deviceId,
        Func<ExecuteCommandEvent, Task> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish configuration report (reported state) to cloud.
    /// This is used to report the current device configuration state back to the cloud,
    /// including both the desired configuration (from cloud) and the reported configuration (actual device state).
    /// </summary>
    /// <param name="configType">The type of configuration (system-config, device-config, or custom-config)</param>
    /// <param name="report">The configuration report message containing desired and reported states</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if published successfully, false otherwise</returns>
    Task<bool> PublishConfigurationReportAsync(
        SubscriptionType configType,
        SubNodeConfigUpdateMessage report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send command response to cloud.
    /// Used to report command execution status (received/rejected/success/failed).
    /// </summary>
    /// <param name="responseTopic">The response topic from DeviceCommand.RespTopic</param>
    /// <param name="response">The command response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sent successfully, false otherwise</returns>
    Task<bool> SendCommandResponseAsync(
        string responseTopic,
        CommandResponse response,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send batch telemetry data to cloud.
    /// Used to report historical recording data during data backfill operations.
    /// </summary>
    /// <param name="deviceId">The device ID (SubNode ID)</param>
    /// <param name="message">The batch telemetry message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sent successfully, false otherwise</returns>
    Task<bool> SendBatchTelemetryAsync(
        string deviceId,
        BatchTelemetrySendMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete device registration from storage
    /// </summary>
    Task ResetRegistrationAsync(CancellationToken ct = default);
}

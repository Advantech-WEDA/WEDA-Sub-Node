using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Cloud;

/// <summary>
/// Weda cloud service interface (abstraction for NATS or other message brokers)
/// Internally uses IJetStreamClient for communication
/// </summary>
public interface IWedaCloudService : IDisposable
{
    /// <summary>
    /// Is connected to cloud
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to cloud service
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

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
    /// Upload device configuration to DMA
    /// INTERNAL USE ONLY - Called by DeviceBase.InitializeAsync
    /// </summary>
    Task<bool> UploadDeviceConfigurationAsync(DeviceConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current device configuration from cloud
    /// </summary>
    Task<DeviceConfiguration?> GetDeviceConfigurationAsync(string deviceId, CancellationToken cancellationToken = default);

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
    /// <param name="report">The configuration report message containing desired and reported states</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if published successfully, false otherwise</returns>
    Task<bool> PublishConfigurationReportAsync(
        SubNodeConfigurationUpdateMessage report,
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
}

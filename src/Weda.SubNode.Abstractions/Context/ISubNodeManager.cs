using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Manages SubNode-level operations that should only happen once for all devices.
/// Responsible for cloud connection, SubNode registration, and event subscription.
/// All devices within a SubNode share the same SubNodeId and cloud subscriptions.
/// </summary>
public interface ISubNodeManager
{
    /// <summary>
    /// Gets whether the SubNode has been initialized (connected, registered, subscribed).
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the SubNode's globally unique ID assigned by cloud after registration.
    /// This ID is shared by all devices within this SubNode.
    /// </summary>
    string? SubNodeId { get; }

    /// <summary>
    /// Initializes the SubNode: connects to cloud, registers SubNode, and subscribes to events.
    /// Should be called ONCE before any device initialization.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if initialization succeeded, false otherwise</returns>
    Task<bool> InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Uploads all device configurations to cloud and aggregated as a single request.
    /// Should be called after all devices have been initialized and enriched with ResourceIds.
    /// </summary>
    /// <param name="configurations">All configurations to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if upload succeed, false otherwise</returns>
    Task<bool> UploadDeviceConfigurationsAsync(DeviceConfigurations configurations, CancellationToken cancellationToken);

    /// <summary>
    /// Event fired when a configuration update is received from cloud.
    /// This is a general event for external subscribers.
    /// For device-specific handling, use RegisterDeviceHandler.
    /// </summary>
    event Func<UpdateConfigurationEvent, Task>? ConfigurationUpdateReceived;

    /// <summary>
    /// Event fired when a command is received from cloud.
    /// </summary>
    event Func<ExecuteCommandEvent, Task>? CommandReceived;

    /// <summary>
    /// Registers a device-specific handler for configuration updates.
    /// SubNodeManager will route updates to the appropriate device based on DeviceName in the message.
    /// SubNodeManager handles report publishing based on ConfigUpdateResult.
    /// </summary>
    /// <param name="deviceName">The device name to register handler for.</param>
    /// <param name="configHandler">Handler for configuration update events. Returns ConfigUpdateResult for SubNodeManager to publish report.</param>
    void RegisterDeviceHandler(
        string deviceName,
        Func<UpdateConfigurationEvent, Task<ConfigUpdateResult>> configHandler);

    /// <summary>
    /// Unregisters a device's event handlers.
    /// </summary>
    /// <param name="deviceName">The device name to unregister</param>
    void UnregisterDeviceHandler(string deviceName);
}

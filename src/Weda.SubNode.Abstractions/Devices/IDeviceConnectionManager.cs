using ErrorOr;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Interface for managing device connections (physical device + cloud service)
/// Extracted from DeviceBase to follow Single Responsibility Principle
/// </summary>
public interface IDeviceConnectionManager
{
    /// <summary>
    /// Establishes connections to physical device and cloud service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error result</returns>
    Task<ErrorOr<Success>> EstablishConnectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to cloud events (configuration updates, commands)
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error result</returns>
    Task<ErrorOr<Success>> SubscribeToCloudEventsAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to cloud events with granular control over which events to subscribe to
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="enableConfigUpdates">Enable configuration update subscription (downlink)</param>
    /// <param name="enableCommands">Enable command subscription (downlink)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error result</returns>
    Task<ErrorOr<Success>> SubscribeToCloudEventsAsync(
        string deviceId,
        bool enableConfigUpdates,
        bool enableCommands,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from physical device and cloud service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Current connection state
    /// </summary>
    CommunicationState CurrentState { get; }

    /// <summary>
    /// Event raised when configuration update is received from cloud
    /// </summary>
    event Func<UpdateConfigurationEvent, Task>? ConfigurationUpdateReceived;

    /// <summary>
    /// Event raised when command is received from cloud
    /// </summary>
    event Func<ExecuteCommandEvent, Task>? CommandReceived;
}

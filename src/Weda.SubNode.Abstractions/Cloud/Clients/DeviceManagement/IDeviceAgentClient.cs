using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;

/// <summary>
/// Device Agent Client for device registration and configuration management
/// Handles communication with DMA (Device Management Agent)
/// </summary>
public interface IDeviceAgentClient
{
    /// <summary>
    /// Register device with cloud
    /// Sends device name and type to cloud, receives device ID and NATS topic assignments
    /// </summary>
    /// <param name="info">Device info (DeviceName and DeviceType)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device registration response with device ID and topic assignments</returns>
    Task<DeviceRegistrationResponse> RegisterDeviceAsync(
        DeviceInfo info,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload device configuration to DMA
    /// Used for configuration synchronization
    /// </summary>
    /// <param name="configuration">Device configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration upload response</returns>
    Task<ConfigurationUploadResponse> UploadDeviceConfigurationAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current device configuration from DMA
    /// </summary>
    /// <param name="deviceId">Device ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device configuration or null if not found</returns>
    Task<DeviceConfiguration?> GetDeviceConfigurationAsync(
        string deviceId,
        CancellationToken cancellationToken = default);
}

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

namespace Weda.SubNode.Abstractions.Storage;

/// <summary>
/// Multi-device registration storage interface.
/// Persists complete device registration information for multiple devices.
/// Each device's registration is stored separately using deviceName as the key.
/// </summary>
public interface IMultiDeviceRegistrationStorage
{
    /// <summary>
    /// Get device registration data from storage for a specific device.
    /// </summary>
    /// <param name="deviceName">The device name (used as storage key)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration data or null if not found</returns>
    Task<DeviceRegistrationResponseData?> GetRegistrationAsync(
        string deviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save device registration data to storage for a specific device.
    /// </summary>
    /// <param name="deviceName">The device name (used as storage key)</param>
    /// <param name="registration">Registration data to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveRegistrationAsync(
        string deviceName,
        DeviceRegistrationResponseData registration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if device registration exists in storage for a specific device.
    /// </summary>
    /// <param name="deviceName">The device name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if registration exists</returns>
    Task<bool> ExistsAsync(
        string deviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete device registration from storage for a specific device.
    /// </summary>
    /// <param name="deviceName">The device name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteRegistrationAsync(
        string deviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all device names that have stored registrations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of device names</returns>
    Task<IReadOnlyList<string>> GetAllDeviceNamesAsync(
        CancellationToken cancellationToken = default);
}

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

namespace Weda.SubNode.Abstractions.Storage;

/// <summary>
/// Device registration storage interface
/// Persists complete device registration information from cloud
/// </summary>
public interface IDeviceRegistrationStorage
{
    /// <summary>
    /// Get device registration data from storage
    /// </summary>
    Task<DeviceRegistrationResponseData?> GetRegistrationAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save device registration data to storage
    /// </summary>
    Task SaveRegistrationAsync(
        DeviceRegistrationResponseData registration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if device registration exists in storage
    /// </summary>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete device registration from storage
    /// </summary>
    Task DeleteRegistrationAsync(CancellationToken cancellationToken = default);
}

namespace Weda.SubNode.Abstractions.Storage;

/// <summary>
/// Storage interface for persisting device ID
/// Device ID should be stored locally after first registration
/// </summary>
public interface IDeviceIdStorage
{
    /// <summary>
    /// Get stored device ID
    /// </summary>
    /// <returns>Device ID or null if not found</returns>
    Task<string?> GetDeviceIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save device ID to storage
    /// </summary>
    /// <param name="deviceId">Device ID to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if device ID exists in storage
    /// </summary>
    /// <returns>True if device ID exists</returns>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete stored device ID (for re-registration)
    /// </summary>
    Task DeleteDeviceIdAsync(CancellationToken cancellationToken = default);
}

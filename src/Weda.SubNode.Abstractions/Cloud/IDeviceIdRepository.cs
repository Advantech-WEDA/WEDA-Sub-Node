namespace Weda.SubNode.Abstractions.Cloud;

/// <summary>
/// Repository for storing and retrieving device ID
/// Persists device ID to local storage (.weda folder)
/// </summary>
public interface IDeviceIdRepository
{
    /// <summary>
    /// Get stored device ID for a device name
    /// </summary>
    Task<string?> GetDeviceIdAsync(string deviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save device ID for a device name
    /// </summary>
    Task SaveDeviceIdAsync(string deviceName, string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if device ID exists for a device name
    /// </summary>
    Task<bool> ExistsAsync(string deviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete stored device ID for a device name
    /// </summary>
    Task DeleteDeviceIdAsync(string deviceName, CancellationToken cancellationToken = default);
}

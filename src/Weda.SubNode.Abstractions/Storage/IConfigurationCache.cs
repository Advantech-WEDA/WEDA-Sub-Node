using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Storage;

/// <summary>
/// Configuration cache interface for persisting device configurations.
/// When configuration is updated from cloud (UC9868), the updated configuration
/// is cached locally so that device restart uses the latest cloud-provided config
/// instead of the original appsettings.json values.
///
/// Multi-Device Support:
/// Each device has its own cache file in .weda/{DeviceName}.config.json
/// This allows multiple devices to coexist with independent cloud-managed configurations.
///
/// Cache Priority:
/// 1. If cache exists → Use cached configuration (from cloud updates)
/// 2. If cache not exists → Use appsettings.json (initial configuration)
///
/// This allows cloud-driven configuration management while maintaining
/// appsettings.json as the baseline/fallback configuration.
/// </summary>
/// <example>
/// <code>
/// // Load configuration with cache priority for a specific device
/// var cache = new JsonConfigurationCache();
/// DeviceConfiguration config;
/// if (await cache.ExistsAsync("MyDevice"))
/// {
///     config = await cache.GetConfigurationAsync("MyDevice"); // Use cloud-updated config
/// }
/// else
/// {
///     config = LoadFromAppSettings(); // Use initial config
/// }
///
/// // After cloud update, save to cache
/// await cache.SaveConfigurationAsync(updatedConfig);
/// </code>
/// </example>
public interface IConfigurationCache
{
    /// <summary>
    /// Get device configuration from cache by device name.
    /// </summary>
    /// <param name="deviceName">Device name (used as cache file name)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached device configuration, or null if not found</returns>
    Task<DeviceConfiguration?> GetConfigurationAsync(
        string deviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save device configuration to cache.
    /// The device name from the configuration is used as the cache file name.
    /// </summary>
    /// <param name="configuration">Device configuration to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveConfigurationAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if configuration cache exists for a device.
    /// </summary>
    /// <param name="deviceName">Device name (used as cache file name)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache file exists and contains valid data</returns>
    Task<bool> ExistsAsync(
        string deviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete configuration cache for a device.
    /// Use this to reset device to use appsettings.json configuration.
    /// </summary>
    /// <param name="deviceName">Device name (used as cache file name)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteCacheAsync(
        string deviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the cache directory path (.weda/).
    /// </summary>
    string CacheDirectoryPath { get; }

    /// <summary>
    /// Get the cache file path for a specific device.
    /// </summary>
    /// <param name="deviceName">Device name</param>
    /// <returns>Full path to the cache file</returns>
    string GetCacheFilePath(string deviceName);

    /// <summary>
    /// Get the last modified time of the cache for a device.
    /// </summary>
    /// <param name="deviceName">Device name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Last modified time, or null if cache doesn't exist</returns>
    Task<DateTimeOffset?> GetLastModifiedAsync(
        string deviceName,
        CancellationToken cancellationToken = default);
}

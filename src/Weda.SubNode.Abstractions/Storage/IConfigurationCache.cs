using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Storage;

/// <summary>
/// Configuration cache interface for persisting device configuration.
/// When configuration is updated from cloud (UC9868), the updated configuration
/// is cached locally so that device restart uses the latest cloud-provided config
/// instead of the original appsettings.json values.
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
/// // Load configuration with cache priority
/// var cache = new JsonConfigurationCache();
/// DeviceConfiguration config;
/// if (await cache.ExistsAsync())
/// {
///     config = await cache.GetConfigurationAsync(); // Use cloud-updated config
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
    /// Get device configuration from cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached device configuration, or null if not found</returns>
    Task<DeviceConfiguration?> GetConfigurationAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save device configuration to cache.
    /// </summary>
    /// <param name="configuration">Device configuration to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveConfigurationAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if configuration cache exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache file exists and contains valid data</returns>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete configuration cache.
    /// Use this to reset device to use appsettings.json configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the cache file path.
    /// </summary>
    string CacheFilePath { get; }

    /// <summary>
    /// Get the last modified time of the cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Last modified time, or null if cache doesn't exist</returns>
    Task<DateTimeOffset?> GetLastModifiedAsync(CancellationToken cancellationToken = default);
}

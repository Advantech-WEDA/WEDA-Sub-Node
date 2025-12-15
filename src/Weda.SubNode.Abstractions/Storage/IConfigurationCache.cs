using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

namespace Weda.SubNode.Abstractions.Storage;

/// <summary>
/// Configuration cache interface for persisting cloud configuration updates.
/// When configuration is updated from cloud (UC9868), the raw cloud message
/// is cached locally so that device restart uses the latest cloud-provided config
/// instead of the original appsettings.json values.
///
/// Single Cache Design:
/// The entire application (which may contain multiple devices) is treated as
/// a single "virtual device" from the cloud's perspective. Therefore, only one
/// cache file (.weda/config.cache.json) is maintained.
///
/// Raw Message Storage:
/// The cache stores the raw SubNodeConfigurationUpdateMessage from cloud directly,
/// preserving the original JSON structure and data types. This avoids type conversion
/// issues (e.g., Port being stored as string instead of int) that can occur when
/// deserializing and re-serializing through intermediate types.
///
/// Cache Priority:
/// 1. If cache exists → Parse cached cloud message and merge with appsettings.json
/// 2. If cache not exists → Use appsettings.json (initial configuration)
///
/// This allows cloud-driven configuration management while maintaining
/// appsettings.json as the baseline/fallback configuration.
/// </summary>
/// <example>
/// <code>
/// // Load configuration with cache priority
/// var cache = new JsonConfigurationCache();
/// if (await cache.ExistsAsync())
/// {
///     var cloudMessage = await cache.GetRawConfigurationAsync();
///     // Apply cloud message to appsettings.json base configuration
/// }
/// else
/// {
///     // Use appsettings.json only
/// }
///
/// // After cloud update, save raw message directly
/// await cache.SaveRawConfigurationAsync(cloudMessage);
/// </code>
/// </example>
public interface IConfigurationCache
{
    /// <summary>
    /// Get the raw cloud configuration message from cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached cloud configuration message, or null if not found</returns>
    Task<SubNodeConfigurationUpdateMessage?> GetRawConfigurationAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save raw cloud configuration message to cache.
    /// The message is stored as-is to preserve original JSON structure and data types.
    /// </summary>
    /// <param name="message">Raw cloud configuration message to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveRawConfigurationAsync(
        SubNodeConfigurationUpdateMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if configuration cache exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache file exists and contains valid data</returns>
    Task<bool> ExistsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete configuration cache.
    /// Use this to reset to use appsettings.json configuration only.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteCacheAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the cache directory path (.weda/).
    /// </summary>
    string CacheDirectoryPath { get; }

    /// <summary>
    /// Get the cache file path.
    /// </summary>
    /// <returns>Full path to the cache file</returns>
    string CacheFilePath { get; }

    /// <summary>
    /// Get the last modified time of the cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Last modified time, or null if cache doesn't exist</returns>
    Task<DateTimeOffset?> GetLastModifiedAsync(
        CancellationToken cancellationToken = default);
}

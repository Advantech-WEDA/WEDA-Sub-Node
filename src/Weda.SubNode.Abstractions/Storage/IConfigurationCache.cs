using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;

namespace Weda.SubNode.Abstractions.Storage;

/// <summary>
/// Configuration cache interface for persisting cloud configuration updates.
/// Supports multiple config types (system, device, custom) with separate cache files.
///
/// Cache Files:
/// - .weda/systemcfg.cache.json - System configuration (Serilog, WedaNode)
/// - .weda/devicecfg.cache.json - Device configuration (SubNode, DeviceConfigs)
/// - .weda/customcfg.cache.json - Custom user configuration
///
/// Raw Message Storage:
/// The cache stores the raw SubNodeConfigUpdateMessage from cloud directly,
/// preserving the original JSON structure and data types.
///
/// Cache Priority:
/// 1. If cache exists → Parse cached cloud message and merge with config files
/// 2. If cache not exists → Use config files (systemcfg.json, devicecfg.json, etc.)
/// </summary>
public interface IConfigurationCache
{
    /// <summary>
    /// Get the raw cloud configuration message from cache for a specific config type.
    /// </summary>
    /// <param name="configType">The configuration type (system-config, device-config, custom-config)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached cloud configuration message, or null if not found</returns>
    Task<SubNodeConfigUpdateMessage?> GetRawConfigurationAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Save raw cloud configuration message to cache for a specific config type.
    /// </summary>
    /// <param name="configType">The configuration type (system-config, device-config, custom-config)</param>
    /// <param name="message">Raw cloud configuration message to cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveRawConfigurationAsync(
        SubscriptionType configType,
        SubNodeConfigUpdateMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if configuration cache exists for a specific config type.
    /// </summary>
    /// <param name="configType">The configuration type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cache file exists and contains valid data</returns>
    Task<bool> ExistsAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete configuration cache for a specific config type.
    /// </summary>
    /// <param name="configType">The configuration type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteCacheAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all configuration caches.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAllCachesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the cache directory path (.weda/).
    /// </summary>
    string CacheDirectoryPath { get; }

    /// <summary>
    /// Get the cache file path for a specific config type.
    /// </summary>
    /// <param name="configType">The configuration type</param>
    /// <returns>Full path to the cache file</returns>
    string GetCacheFilePath(SubscriptionType configType);

    /// <summary>
    /// Get the last modified time of the cache for a specific config type.
    /// </summary>
    /// <param name="configType">The configuration type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Last modified time, or null if cache doesn't exist</returns>
    Task<DateTimeOffset?> GetLastModifiedAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default);
}

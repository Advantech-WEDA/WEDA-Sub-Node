using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// JSON-based configuration cache implementation.
/// Stores cloud configuration messages in separate files per config type.
///
/// Cache Files:
/// - .weda/systemcfg.cache.json - System configuration
/// - .weda/devicecfg.cache.json - Device configuration
/// - .weda/customcfg.cache.json - Custom configuration
/// </summary>
public class JsonConfigurationCache : IConfigurationCache
{
    /// <summary>
    /// Default cache directory name.
    /// </summary>
    public const string DefaultCacheDirectory = ".weda";

    private readonly string _cacheDirectoryPath;
    private readonly ILogger<JsonConfigurationCache> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Create a new JSON-based configuration cache.
    /// </summary>
    /// <param name="cacheDirectoryPath">Path to cache directory. Default: .weda/ in project root.</param>
    /// <param name="logger">Logger instance</param>
    public JsonConfigurationCache(
        string? cacheDirectoryPath = null,
        ILogger<JsonConfigurationCache>? logger = null)
    {
        _cacheDirectoryPath = cacheDirectoryPath ?? Path.Combine(
            FindProjectRoot() ?? Directory.GetCurrentDirectory(),
            DefaultCacheDirectory);

        _logger = logger ?? NullLoggerFactory.Instance
            .CreateLogger<JsonConfigurationCache>();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        // Ensure cache directory exists
        if (!Directory.Exists(_cacheDirectoryPath))
        {
            Directory.CreateDirectory(_cacheDirectoryPath);
        }
    }

    /// <inheritdoc />
    public string CacheDirectoryPath => _cacheDirectoryPath;

    /// <inheritdoc />
    public string GetCacheFilePath(SubscriptionType configType)
    {
        var fileName = GetCacheFileName(configType);
        return Path.Combine(_cacheDirectoryPath, fileName);
    }

    /// <inheritdoc />
    public async Task<SubNodeConfigUpdateMessage?> GetRawConfigurationAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetCacheFilePath(configType);
        var lockObj = GetLock(configType);

        await lockObj.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Configuration cache not found: Type={Type}, Path={FilePath}",
                    configType.Value, filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Configuration cache exists but is empty: Type={Type}, Path={FilePath}",
                    configType.Value, filePath);
                return null;
            }

            var message = JsonSerializer.Deserialize<SubNodeConfigUpdateMessage>(
                json, _jsonOptions);

            if (message != null)
            {
                _logger.LogInformation(
                    "Configuration loaded from cache: Type={Type}, DeviceId={DeviceId}, Path={FilePath}",
                    configType.Value,
                    message.DeviceId,
                    filePath);
            }

            return message;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing configuration cache JSON: Type={Type}, Path={FilePath}",
                configType.Value, filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration cache: Type={Type}, Path={FilePath}",
                configType.Value, filePath);
            throw;
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveRawConfigurationAsync(
        SubscriptionType configType,
        SubNodeConfigUpdateMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configType);
        ArgumentNullException.ThrowIfNull(message);

        var filePath = GetCacheFilePath(configType);
        var lockObj = GetLock(configType);

        await lockObj.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_cacheDirectoryPath))
            {
                Directory.CreateDirectory(_cacheDirectoryPath);
            }

            var json = JsonSerializer.Serialize(message, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation(
                "Configuration saved to cache: Type={Type}, DeviceId={DeviceId}, Path={FilePath}",
                configType.Value,
                message.DeviceId,
                filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration cache: Type={Type}, Path={FilePath}",
                configType.Value, filePath);
            throw;
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetCacheFilePath(configType);
        var lockObj = GetLock(configType);

        await lockObj.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return false;

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return !string.IsNullOrWhiteSpace(content);
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteCacheAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetCacheFilePath(configType);
        var lockObj = GetLock(configType);

        await lockObj.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Configuration cache deleted: Type={Type}, Path={FilePath}",
                    configType.Value, filePath);
            }
            else
            {
                _logger.LogDebug("Configuration cache not found, nothing to delete: Type={Type}, Path={FilePath}",
                    configType.Value, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration cache: Type={Type}, Path={FilePath}",
                configType.Value, filePath);
            throw;
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAllCachesAsync(CancellationToken cancellationToken = default)
    {
        var configTypes = new[]
        {
            SubscriptionTypes.SystemConfig,
            SubscriptionTypes.DeviceConfig,
            SubscriptionTypes.CustomConfig
        };

        foreach (var configType in configTypes)
        {
            await DeleteCacheAsync(configType, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastModifiedAsync(
        SubscriptionType configType,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetCacheFilePath(configType);
        var lockObj = GetLock(configType);

        await lockObj.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return null;

            var fileInfo = new FileInfo(filePath);
            return new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
        }
        finally
        {
            lockObj.Release();
        }
    }

    /// <summary>
    /// Get the cache file name for a config type.
    /// </summary>
    private static string GetCacheFileName(SubscriptionType configType)
    {
        return configType.Value switch
        {
            "system-config" => "systemcfg.cache.json",
            "device-config" => "devicecfg.cache.json",
            "custom-config" => "customcfg.cache.json",
            _ => $"{configType.Value}.cache.json"
        };
    }

    /// <summary>
    /// Get or create a lock for a config type.
    /// </summary>
    private SemaphoreSlim GetLock(SubscriptionType configType)
    {
        return _locks.GetOrAdd(configType.Value, _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>
    /// Finds the project root directory by looking for .csproj file.
    /// </summary>
    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

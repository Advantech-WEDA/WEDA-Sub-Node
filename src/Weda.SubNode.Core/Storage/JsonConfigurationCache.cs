using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// JSON-based configuration cache implementation.
/// Stores device configurations in .weda/{DeviceName}.config.json files.
///
/// Purpose:
/// When cloud sends configuration updates (UC9868), the updated configuration
/// is persisted to this cache. On device restart, the cache takes priority
/// over appsettings.json, ensuring cloud-driven configuration persists.
///
/// Multi-Device Support:
/// Each device has its own cache file: .weda/{DeviceName}.config.json
/// This allows multiple devices to coexist with independent cloud-managed configurations.
/// </summary>
public class JsonConfigurationCache : IConfigurationCache
{
    /// <summary>
    /// Default cache directory name.
    /// </summary>
    public const string DefaultCacheDirectory = ".weda";

    /// <summary>
    /// Cache file extension.
    /// </summary>
    public const string CacheFileExtension = ".config.json";

    private readonly string _cacheDirectoryPath;
    private readonly ILogger<JsonConfigurationCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Create a new JSON-based configuration cache.
    /// </summary>
    /// <param name="cacheDirectoryPath">
    /// Path to cache directory.
    /// Default: .weda/ in project root directory.
    /// </param>
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
    public string GetCacheFilePath(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or whitespace", nameof(deviceName));
        }

        return Path.Combine(_cacheDirectoryPath, $"{deviceName}{CacheFileExtension}");
    }

    /// <inheritdoc />
    public async Task<DeviceConfiguration?> GetConfigurationAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or whitespace", nameof(deviceName));
        }

        var filePath = GetCacheFilePath(deviceName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Configuration cache not found for device '{DeviceName}': {FilePath}",
                    deviceName, filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Configuration cache exists but is empty for device '{DeviceName}': {FilePath}",
                    deviceName, filePath);
                return null;
            }

            var configuration = JsonSerializer.Deserialize<DeviceConfiguration>(
                json, _jsonOptions);

            if (configuration != null)
            {
                _logger.LogInformation(
                    "Configuration loaded from cache: DeviceName={DeviceName}, Path={FilePath}",
                    configuration.DeviceName,
                    filePath);
            }

            return configuration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing configuration cache JSON for device '{DeviceName}': {FilePath}",
                deviceName, filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration cache for device '{DeviceName}': {FilePath}",
                deviceName, filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveConfigurationAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(configuration.DeviceName))
        {
            throw new ArgumentException("DeviceConfiguration.DeviceName cannot be null or whitespace", nameof(configuration));
        }

        var filePath = GetCacheFilePath(configuration.DeviceName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_cacheDirectoryPath))
            {
                Directory.CreateDirectory(_cacheDirectoryPath);
            }

            var json = JsonSerializer.Serialize(configuration, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation(
                "Configuration saved to cache: DeviceName={DeviceName}, SensorCount={SensorCount}, Path={FilePath}",
                configuration.DeviceName,
                configuration.Sensors.Count,
                filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration cache for device '{DeviceName}': {FilePath}",
                configuration.DeviceName, filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or whitespace", nameof(deviceName));
        }

        var filePath = GetCacheFilePath(deviceName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return false;

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return !string.IsNullOrWhiteSpace(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteCacheAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or whitespace", nameof(deviceName));
        }

        var filePath = GetCacheFilePath(deviceName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Configuration cache deleted for device '{DeviceName}': {FilePath}",
                    deviceName, filePath);
            }
            else
            {
                _logger.LogDebug(
                    "Configuration cache not found for device '{DeviceName}', nothing to delete: {FilePath}",
                    deviceName, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration cache for device '{DeviceName}': {FilePath}",
                deviceName, filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastModifiedAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device name cannot be null or whitespace", nameof(deviceName));
        }

        var filePath = GetCacheFilePath(deviceName);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return null;

            var fileInfo = new FileInfo(filePath);
            return new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Finds the project root directory by looking for .csproj file.
    /// This ensures runtime-generated files are stored in the project directory (where Program.cs is),
    /// not in bin/Debug/net9.0/ during development.
    /// </summary>
    /// <returns>The project root directory path, or null if not found.</returns>
    private static string? FindProjectRoot()
    {
        // Start from the current directory (which may be bin/Debug/net9.0/)
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null)
        {
            // Only check for .csproj file - this uniquely identifies the project root
            // (appsettings.json gets copied to bin/Debug/net9.0/, so we can't rely on it)
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

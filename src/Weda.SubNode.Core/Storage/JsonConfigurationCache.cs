using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// JSON-based configuration cache implementation.
/// Stores the raw cloud configuration message in .weda/config.cache.json file.
///
/// Purpose:
/// When cloud sends configuration updates (UC9868), the raw message
/// is persisted to this cache. On device restart, the cache takes priority
/// over appsettings.json, ensuring cloud-driven configuration persists.
///
/// Single Cache Design:
/// The entire application (which may contain multiple devices) uses a single
/// cache file. From the cloud's perspective, the application is treated as
/// one "virtual device" regardless of how many physical devices it manages.
///
/// Raw Message Storage:
/// By storing the raw SubNodeConfigurationUpdateMessage directly, we preserve
/// the original JSON structure and data types, avoiding conversion issues.
/// </summary>
public class JsonConfigurationCache : IConfigurationCache
{
    /// <summary>
    /// Default cache directory name.
    /// </summary>
    public const string DefaultCacheDirectory = ".weda";

    /// <summary>
    /// Cache file name.
    /// </summary>
    public const string CacheFileName = "config.cache.json";

    private readonly string _cacheDirectoryPath;
    private readonly string _cacheFilePath;
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

        _cacheFilePath = Path.Combine(_cacheDirectoryPath, CacheFileName);

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
    public string CacheFilePath => _cacheFilePath;

    /// <inheritdoc />
    public async Task<SubNodeConfigurationUpdateMessage?> GetRawConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                _logger.LogDebug("Configuration cache not found: {FilePath}", _cacheFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_cacheFilePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Configuration cache exists but is empty: {FilePath}", _cacheFilePath);
                return null;
            }

            var message = JsonSerializer.Deserialize<SubNodeConfigurationUpdateMessage>(
                json, _jsonOptions);

            if (message != null)
            {
                _logger.LogInformation(
                    "Configuration loaded from cache: DeviceId={DeviceId}, Path={FilePath}",
                    message.DeviceId,
                    _cacheFilePath);
            }

            return message;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing configuration cache JSON: {FilePath}", _cacheFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration cache: {FilePath}", _cacheFilePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveRawConfigurationAsync(
        SubNodeConfigurationUpdateMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_cacheDirectoryPath))
            {
                Directory.CreateDirectory(_cacheDirectoryPath);
            }

            var json = JsonSerializer.Serialize(message, _jsonOptions);
            await File.WriteAllTextAsync(_cacheFilePath, json, cancellationToken);

            var deviceCount = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs?.Count ?? 0;
            _logger.LogInformation(
                "Configuration saved to cache: DeviceId={DeviceId}, DeviceCount={DeviceCount}, Path={FilePath}",
                message.DeviceId,
                deviceCount,
                _cacheFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration cache: {FilePath}", _cacheFilePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_cacheFilePath))
                return false;

            var content = await File.ReadAllTextAsync(_cacheFilePath, cancellationToken);
            return !string.IsNullOrWhiteSpace(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteCacheAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
                _logger.LogInformation("Configuration cache deleted: {FilePath}", _cacheFilePath);
            }
            else
            {
                _logger.LogDebug("Configuration cache not found, nothing to delete: {FilePath}", _cacheFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration cache: {FilePath}", _cacheFilePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastModifiedAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_cacheFilePath))
                return null;

            var fileInfo = new FileInfo(_cacheFilePath);
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
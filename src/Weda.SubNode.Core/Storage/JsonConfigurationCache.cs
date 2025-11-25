using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// JSON-based configuration cache implementation.
/// Stores device configuration in a local JSON file (.device-config-cache.json).
///
/// Purpose:
/// When cloud sends configuration updates (UC9868), the updated configuration
/// is persisted to this cache. On device restart, the cache takes priority
/// over appsettings.json, ensuring cloud-driven configuration persists.
///
/// File naming convention follows .device-registration.json pattern.
/// </summary>
public class JsonConfigurationCache : IConfigurationCache
{
    /// <summary>
    /// Default cache file name.
    /// </summary>
    public const string DefaultFileName = ".device-config-cache.json";

    private readonly string _filePath;
    private readonly ILogger<JsonConfigurationCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Create a new JSON-based configuration cache.
    /// </summary>
    /// <param name="filePath">
    /// Path to cache file.
    /// Default: .device-config-cache.json in current directory.
    /// </param>
    /// <param name="logger">Logger instance</param>
    public JsonConfigurationCache(
        string? filePath = null,
        ILogger<JsonConfigurationCache>? logger = null)
    {
        _filePath = filePath ?? Path.Combine(
            Directory.GetCurrentDirectory(),
            DefaultFileName);

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
    }

    /// <inheritdoc />
    public string CacheFilePath => _filePath;

    /// <inheritdoc />
    public async Task<DeviceConfiguration?> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("Configuration cache not found: {FilePath}", _filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Configuration cache exists but is empty: {FilePath}",
                    _filePath);
                return null;
            }

            var configuration = JsonSerializer.Deserialize<DeviceConfiguration>(
                json, _jsonOptions);

            if (configuration != null)
            {
                _logger.LogInformation(
                    "Configuration loaded from cache: DeviceName={DeviceName}, Path={FilePath}",
                    configuration.DeviceName,
                    _filePath);
            }

            return configuration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing configuration cache JSON: {FilePath}",
                _filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configuration cache: {FilePath}",
                _filePath);
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

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(configuration, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken);

            _logger.LogInformation(
                "Configuration saved to cache: DeviceName={DeviceName}, SensorCount={SensorCount}, Path={FilePath}",
                configuration.DeviceName,
                configuration.Sensors.Count,
                _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration cache: {FilePath}",
                _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
                return false;

            var content = await File.ReadAllTextAsync(_filePath, cancellationToken);
            return !string.IsNullOrWhiteSpace(content);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteCacheAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                _logger.LogInformation("Configuration cache deleted: {FilePath}",
                    _filePath);
            }
            else
            {
                _logger.LogDebug(
                    "Configuration cache not found, nothing to delete: {FilePath}",
                    _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting configuration cache: {FilePath}",
                _filePath);
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
            if (!File.Exists(_filePath))
                return null;

            var fileInfo = new FileInfo(_filePath);
            return new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
        }
        finally
        {
            _lock.Release();
        }
    }
}

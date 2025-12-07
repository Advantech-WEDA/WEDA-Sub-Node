using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// JSON-based multi-device registration storage implementation.
/// Stores each device's registration in a separate file under .weda/ directory.
/// File format: .weda/{sanitizedDeviceName}.registration.json
/// Thread-safe for concurrent access to different devices.
/// </summary>
public class JsonMultiDeviceRegistrationStorage : IMultiDeviceRegistrationStorage
{
    private readonly string _storageDirectory;
    private readonly ILogger<JsonMultiDeviceRegistrationStorage> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private const string FileExtension = ".registration.json";

    /// <summary>
    /// Create a new JSON-based multi-device registration storage.
    /// </summary>
    /// <param name="storageDirectory">Directory for storage files (default: .weda in project root)</param>
    /// <param name="logger">Logger instance</param>
    public JsonMultiDeviceRegistrationStorage(
        string? storageDirectory = null,
        ILogger<JsonMultiDeviceRegistrationStorage>? logger = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(
            FindProjectRoot() ?? Directory.GetCurrentDirectory(),
            ".weda");

        _logger = logger ?? NullLoggerFactory.Instance
            .CreateLogger<JsonMultiDeviceRegistrationStorage>();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        // Ensure storage directory exists
        EnsureDirectoryExists();
    }

    public async Task<DeviceRegistrationResponseData?> GetRegistrationAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        var filePath = GetFilePath(deviceName);
        var deviceLock = GetLock(deviceName);

        await deviceLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogDebug(
                    "Device registration file not found: Device={DeviceName}, Path={FilePath}",
                    deviceName, filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning(
                    "Device registration file exists but is empty: Device={DeviceName}, Path={FilePath}",
                    deviceName, filePath);
                return null;
            }

            var registration = JsonSerializer.Deserialize<DeviceRegistrationResponseData>(
                json, _jsonOptions);

            if (registration != null)
            {
                _logger.LogDebug(
                    "Device registration loaded: Device={DeviceName}, DeviceId={DeviceId}",
                    deviceName, registration.DeviceId);
            }

            return registration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Error parsing device registration JSON: Device={DeviceName}, Path={FilePath}",
                deviceName, filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error reading device registration: Device={DeviceName}, Path={FilePath}",
                deviceName, filePath);
            throw;
        }
        finally
        {
            deviceLock.Release();
        }
    }

    public async Task SaveRegistrationAsync(
        string deviceName,
        DeviceRegistrationResponseData registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(registration);

        var filePath = GetFilePath(deviceName);
        var deviceLock = GetLock(deviceName);

        await deviceLock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectoryExists();

            var json = JsonSerializer.Serialize(registration, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation(
                "Device registration saved: Device={DeviceName}, DeviceId={DeviceId}, Path={FilePath}",
                deviceName, registration.DeviceId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error saving device registration: Device={DeviceName}, Path={FilePath}",
                deviceName, filePath);
            throw;
        }
        finally
        {
            deviceLock.Release();
        }
    }

    public async Task<bool> ExistsAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        var filePath = GetFilePath(deviceName);
        var deviceLock = GetLock(deviceName);

        await deviceLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
                return false;

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            return !string.IsNullOrWhiteSpace(content);
        }
        finally
        {
            deviceLock.Release();
        }
    }

    public async Task DeleteRegistrationAsync(
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

        var filePath = GetFilePath(deviceName);
        var deviceLock = GetLock(deviceName);

        await deviceLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation(
                    "Device registration deleted: Device={DeviceName}, Path={FilePath}",
                    deviceName, filePath);
            }
            else
            {
                _logger.LogDebug(
                    "Device registration file not found, nothing to delete: Device={DeviceName}",
                    deviceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting device registration: Device={DeviceName}, Path={FilePath}",
                deviceName, filePath);
            throw;
        }
        finally
        {
            deviceLock.Release();
        }
    }

    public Task<IReadOnlyList<string>> GetAllDeviceNamesAsync(
        CancellationToken cancellationToken = default)
    {
        var deviceNames = new List<string>();

        if (!Directory.Exists(_storageDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>(deviceNames);
        }

        var files = Directory.GetFiles(_storageDirectory, $"*{FileExtension}");
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var deviceName = fileName.Replace(FileExtension, "");
            deviceNames.Add(deviceName);
        }

        return Task.FromResult<IReadOnlyList<string>>(deviceNames.AsReadOnly());
    }

    private string GetFilePath(string deviceName)
    {
        // Sanitize device name for file system
        var sanitizedName = string.Join("_", deviceName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_storageDirectory, $"{sanitizedName}{FileExtension}");
    }

    private SemaphoreSlim GetLock(string deviceName)
    {
        return _locks.GetOrAdd(deviceName, _ => new SemaphoreSlim(1, 1));
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
            _logger.LogDebug("Created storage directory: {Directory}", _storageDirectory);
        }
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

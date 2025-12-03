using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// JSON-based device registration storage implementation
/// Stores registration data in appsettings.json compatible format
/// </summary>
public class JsonDeviceRegistrationStorage : IDeviceRegistrationStorage
{
    private readonly string _filePath;
    private readonly ILogger<JsonDeviceRegistrationStorage> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Create a new JSON-based device registration storage
    /// </summary>
    /// <param name="filePath">Path to storage file (default: .device-registration.json in project root directory)</param>
    /// <param name="logger">Logger instance</param>
    public JsonDeviceRegistrationStorage(
        string? filePath = null,
        ILogger<JsonDeviceRegistrationStorage>? logger = null)
    {
        _filePath = filePath ?? Path.Combine(
            FindProjectRoot() ?? Directory.GetCurrentDirectory(),
            ".device-registration.json");

        _logger = logger ?? NullLoggerFactory.Instance
            .CreateLogger<JsonDeviceRegistrationStorage>();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public async Task<DeviceRegistrationResponseData?> GetRegistrationAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("Device registration file not found: {FilePath}", _filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Device registration file exists but is empty: {FilePath}",
                    _filePath);
                return null;
            }

            var registration = JsonSerializer.Deserialize<DeviceRegistrationResponseData>(
                json, _jsonOptions);

            if (registration != null)
            {
                _logger.LogDebug("Device registration loaded from storage: DeviceId={DeviceId}",
                    registration.DeviceId);
            }

            return registration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing device registration JSON from file: {FilePath}",
                _filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading device registration from file: {FilePath}",
                _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveRegistrationAsync(
        DeviceRegistrationResponseData registration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(registration, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken);

            _logger.LogInformation(
                "Device registration saved to storage: DeviceId={DeviceId}, Path={FilePath}",
                registration.DeviceId, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving device registration to file: {FilePath}",
                _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

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

    public async Task DeleteRegistrationAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                _logger.LogInformation("Device registration deleted from storage: {FilePath}",
                    _filePath);
            }
            else
            {
                _logger.LogDebug(
                    "Device registration file not found, nothing to delete: {FilePath}",
                    _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device registration file: {FilePath}",
                _filePath);
            throw;
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

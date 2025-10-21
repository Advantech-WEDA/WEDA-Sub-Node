using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// AppSettings.json integrated device registration storage
/// Stores registration data directly in appsettings.json under "DeviceRegistration" section
/// </summary>
public class AppSettingsDeviceRegistrationStorage : IDeviceRegistrationStorage
{
    private readonly string _appSettingsPath;
    private readonly ILogger<AppSettingsDeviceRegistrationStorage> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private const string RegistrationSectionName = "DeviceRegistration";

    /// <summary>
    /// Create a new appsettings.json integrated registration storage
    /// </summary>
    /// <param name="appSettingsPath">Path to appsettings.json (default: appsettings.json in current directory)</param>
    /// <param name="logger">Logger instance</param>
    public AppSettingsDeviceRegistrationStorage(
        string? appSettingsPath = null,
        ILogger<AppSettingsDeviceRegistrationStorage>? logger = null)
    {
        _appSettingsPath = appSettingsPath ?? Path.Combine(
            Directory.GetCurrentDirectory(),
            "appsettings.json");

        _logger = logger ?? NullLoggerFactory.Instance
            .CreateLogger<AppSettingsDeviceRegistrationStorage>();

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
            if (!File.Exists(_appSettingsPath))
            {
                _logger.LogDebug("AppSettings file not found: {FilePath}", _appSettingsPath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
            var root = JsonNode.Parse(json);

            if (root == null)
            {
                _logger.LogWarning("Failed to parse appsettings.json: {FilePath}",
                    _appSettingsPath);
                return null;
            }

            var registrationSection = root[RegistrationSectionName];
            if (registrationSection == null)
            {
                _logger.LogDebug("DeviceRegistration section not found in appsettings.json");
                return null;
            }

            var registration = registrationSection.Deserialize<DeviceRegistrationResponseData>(
                _jsonOptions);

            if (registration != null)
            {
                _logger.LogDebug("Device registration loaded from appsettings.json: DeviceId={DeviceId}",
                    registration.DeviceId);
            }

            return registration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing appsettings.json: {FilePath}", _appSettingsPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading appsettings.json: {FilePath}", _appSettingsPath);
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
            JsonObject root;

            // Read existing appsettings.json or create new
            if (File.Exists(_appSettingsPath))
            {
                var json = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
                root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            // Serialize registration to JsonNode
            var registrationJson = JsonSerializer.Serialize(registration, _jsonOptions);
            var registrationNode = JsonNode.Parse(registrationJson);

            // Update or add DeviceRegistration section
            root[RegistrationSectionName] = registrationNode;

            // Write back to file
            var outputJson = root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_appSettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_appSettingsPath, outputJson, cancellationToken);

            _logger.LogInformation(
                "Device registration saved to appsettings.json: DeviceId={DeviceId}, Path={FilePath}",
                registration.DeviceId, _appSettingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving device registration to appsettings.json: {FilePath}",
                _appSettingsPath);
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
            if (!File.Exists(_appSettingsPath))
                return false;

            var json = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
            var root = JsonNode.Parse(json);

            return root?[RegistrationSectionName] != null;
        }
        catch
        {
            return false;
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
            if (!File.Exists(_appSettingsPath))
            {
                _logger.LogDebug(
                    "AppSettings file not found, nothing to delete: {FilePath}",
                    _appSettingsPath);
                return;
            }

            var json = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
            var root = JsonNode.Parse(json)?.AsObject();

            if (root == null)
            {
                _logger.LogWarning("Failed to parse appsettings.json: {FilePath}",
                    _appSettingsPath);
                return;
            }

            if (root.Remove(RegistrationSectionName))
            {
                var outputJson = root.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_appSettingsPath, outputJson, cancellationToken);

                _logger.LogInformation(
                    "Device registration removed from appsettings.json: {FilePath}",
                    _appSettingsPath);
            }
            else
            {
                _logger.LogDebug(
                    "DeviceRegistration section not found in appsettings.json: {FilePath}",
                    _appSettingsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting device registration from appsettings.json: {FilePath}",
                _appSettingsPath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}

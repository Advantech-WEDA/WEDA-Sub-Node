using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud;

namespace Weda.SubNode.Cloud.Storage;

/// <summary>
/// File-based device ID repository
/// Stores device IDs in .weda folder
/// File format: {deviceName}.weda containing deviceId as plain text
/// Enables device registration persistence across service restarts
/// </summary>
public class FileDeviceIdRepository : IDeviceIdRepository
{
    private readonly string _storageDirectory;
    private readonly ILogger<FileDeviceIdRepository> _logger;

    public FileDeviceIdRepository(string? storageDirectory = null, ILogger<FileDeviceIdRepository>? logger = null)
    {
        _storageDirectory = storageDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), ".weda");
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<FileDeviceIdRepository>();

        // Ensure storage directory exists
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
            _logger.LogDebug("Created storage directory: {Directory}", _storageDirectory);
        }
    }

    public async Task<string?> GetDeviceIdAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(deviceName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var deviceId = await File.ReadAllTextAsync(filePath, cancellationToken);
            deviceId = deviceId.Trim();

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            _logger.LogDebug("Retrieved device ID: {DeviceId} for device: {DeviceName}", deviceId, deviceName);
            return deviceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading device ID for device: {DeviceName}", deviceName);
            return null;
        }
    }

    public async Task SaveDeviceIdAsync(string deviceName, string deviceId, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(deviceName);

        try
        {
            await File.WriteAllTextAsync(filePath, deviceId, cancellationToken);

            _logger.LogInformation("Saved device ID: {DeviceId} for device: {DeviceName}", deviceId, deviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving device ID for device: {DeviceName}", deviceName);
            throw;
        }
    }

    public Task<bool> ExistsAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(deviceName);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task DeleteDeviceIdAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(deviceName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted device ID file for device: {DeviceName}", deviceName);
        }

        return Task.CompletedTask;
    }

    private string GetFilePath(string deviceName)
    {
        // Sanitize device name for file system
        var sanitizedName = string.Join("_", deviceName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_storageDirectory, $"{sanitizedName}.weda");
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// File-based device ID storage implementation
/// Stores device ID in a simple text file
/// </summary>
public class FileDeviceIdStorage : IDeviceIdStorage
{
    private readonly string _filePath;
    private readonly ILogger<FileDeviceIdStorage> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Create a new file-based device ID storage
    /// </summary>
    /// <param name="filePath">Path to storage file (default: .device-id in current directory)</param>
    /// <param name="logger">Logger instance</param>
    public FileDeviceIdStorage(
        string? filePath = null,
        ILogger<FileDeviceIdStorage>? logger = null)
    {
        _filePath = filePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".device-id");
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<FileDeviceIdStorage>();
    }

    public async Task<string?> GetDeviceIdAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("Device ID file not found: {FilePath}", _filePath);
                return null;
            }

            var deviceId = await File.ReadAllTextAsync(_filePath, cancellationToken);
            deviceId = deviceId.Trim();

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                _logger.LogWarning("Device ID file exists but is empty: {FilePath}", _filePath);
                return null;
            }

            _logger.LogDebug("Device ID loaded from storage: {DeviceId}", deviceId);
            return deviceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading device ID from file: {FilePath}", _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_filePath, deviceId, cancellationToken);

            _logger.LogInformation("Device ID saved to storage: {DeviceId}, Path: {FilePath}",
                deviceId, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving device ID to file: {FilePath}", _filePath);
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

    public async Task DeleteDeviceIdAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                _logger.LogInformation("Device ID deleted from storage: {FilePath}", _filePath);
            }
            else
            {
                _logger.LogDebug("Device ID file not found, nothing to delete: {FilePath}", _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device ID file: {FilePath}", _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}

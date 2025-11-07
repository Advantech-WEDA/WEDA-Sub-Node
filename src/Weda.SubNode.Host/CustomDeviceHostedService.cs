using System.Reflection;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Host;

/// <summary>
/// Hosted service wrapper for custom device instances
/// Manages the lifecycle (initialization, start, stop) of a custom device
/// </summary>
internal class CustomDeviceHostedService : IHostedService, IDisposable
{
    private readonly IDevice _device;
    private readonly ILogger? _logger;

    public CustomDeviceHostedService(IDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));

        // Try to get logger from device if it has one
        var loggerProperty = device.GetType().GetField("_logger",
            BindingFlags.NonPublic | BindingFlags.Instance);
        _logger = loggerProperty?.GetValue(device) as ILogger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Initializing custom device: {DeviceName} ({DeviceType})",
            _device.Configuration.DeviceName,
            _device.DeviceType);

        if (!await _device.InitializeAsync(cancellationToken))
        {
            var errorMsg = $"Failed to initialize device: {_device.Configuration.DeviceName}";
            _logger?.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger?.LogInformation("Starting custom device: {DeviceName}",
            _device.Configuration.DeviceName);

        await _device.StartAsync(cancellationToken);

        _logger?.LogInformation("Custom device started successfully: {DeviceName}",
            _device.Configuration.DeviceName);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Stopping custom device: {DeviceName}",
            _device.Configuration.DeviceName);

        await _device.StopAsync(cancellationToken);

        _logger?.LogInformation("Custom device stopped: {DeviceName}",
            _device.Configuration.DeviceName);
    }

    public void Dispose()
    {
        _device.Dispose();
    }
}

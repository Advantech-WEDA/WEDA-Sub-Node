using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Host;

/// <summary>
/// Hosted service that manages the lifecycle of one or more devices.
/// Automatically initializes, starts, and stops all registered devices.
/// </summary>
internal class DeviceHostedService : IHostedService
{
    private readonly ILogger<DeviceHostedService> _logger;
    private readonly List<IDevice> _devices;

    /// <summary>
    /// Constructor that accepts a list of devices to manage.
    /// Supports both single device and multiple devices.
    /// </summary>
    public DeviceHostedService(
        ILogger<DeviceHostedService> logger,
        IEnumerable<IDevice> devices)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _devices = [.. devices ?? throw new ArgumentNullException(nameof(devices))];
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_devices.Count == 0)
        {
            _logger.LogInformation("No devices configured");
            return;
        }

        _logger.LogInformation("Starting Device Hosted Service with {DeviceCount} device(s)", _devices.Count);

        foreach (var device in _devices)
        {
            try
            {
                _logger.LogInformation("Initializing device: {DeviceName} ({DeviceType})",
                    device.Configuration.DeviceName,
                    device.DeviceType);

                // Initialize device (connect + register)
                var initialized = await device.InitializeAsync(cancellationToken);
                if (!initialized)
                {
                    _logger.LogError("Failed to initialize device: {DeviceName}", device.Configuration.DeviceName);
                    continue;
                }

                // Start background tasks (telemetry, health reporting)
                await device.StartAsync(cancellationToken);

                _logger.LogInformation("Device started successfully: {DeviceName} (DeviceId: {DeviceId})",
                    device.Configuration.DeviceName, device.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting device: {DeviceName}", device.Configuration.DeviceName);
            }
        }

        _logger.LogInformation("Device Hosted Service started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Device Hosted Service");

        foreach (var device in _devices)
        {
            try
            {
                _logger.LogInformation("Stopping device: {DeviceId}", device.DeviceId);
                await device.StopAsync(cancellationToken);
                _logger.LogInformation("Device stopped: {DeviceId}", device.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping device: {DeviceId}", device.DeviceId);
            }
        }

        _logger.LogInformation("Device Hosted Service stopped");
    }
}

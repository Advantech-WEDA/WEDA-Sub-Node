using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Host;

/// <summary>
/// Factory implementation for creating device instances with dependency injection
/// </summary>
internal class DeviceFactory : IDeviceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeviceFactory> _logger;

    public DeviceFactory(IServiceProvider serviceProvider, ILogger<DeviceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public TDevice CreateDevice<TDevice>(
        DeviceConfiguration configuration,
        ICommunication communication)
        where TDevice : class, IDevice
    {
        try
        {
            // Resolve cloud service from DI
            var cloudService = _serviceProvider.GetRequiredService<IWedaCloudService>();

            // Resolve logger for device
            var deviceLogger = _serviceProvider.GetService<ILogger<TDevice>>();

            // Create device instance using Activator with injected dependencies
            var device = Activator.CreateInstance(
                typeof(TDevice),
                configuration,
                communication,
                cloudService,
                deviceLogger) as TDevice;

            if (device == null)
            {
                throw new InvalidOperationException(
                    $"Failed to create device instance of type {typeof(TDevice).Name}. " +
                    $"Ensure the device has a constructor with parameters: " +
                    $"(DeviceConfiguration, ICommunication, IWedaCloudService, ILogger<DeviceBase>?)");
            }

            _logger.LogInformation("Created device instance: {DeviceType}", typeof(TDevice).Name);
            return device;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create device of type {DeviceType}", typeof(TDevice).Name);
            throw;
        }
    }
}
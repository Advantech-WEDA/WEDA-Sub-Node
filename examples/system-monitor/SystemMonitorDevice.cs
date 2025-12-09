using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Devices;

namespace SystemMonitorExample;

/// <summary>
/// System resource monitoring device that collects CPU, memory, disk, and network metrics.
/// Inherits from RequestResponseDeviceBase for Request/Response communication pattern.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: SystemMonitorDevice -> RequestResponseDeviceBase -> DeviceBase
/// </summary>
public class SystemMonitorDevice : RequestResponseDeviceBase
{
    /// <summary>
    /// Initializes a new instance of SystemMonitorDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    public SystemMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateParser(context, configuration))
    {
        _logger.LogDebug(
            "SystemMonitorDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);

        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;      
    }

    /// <summary>
    /// Creates the SystemMetricsParser for this device.
    /// </summary>
    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var loggerFactory = context.LoggerFactory;
        var communication = new LocalSystemCommunication(
            configuration.ConnectionSettings,
            loggerFactory.CreateLogger<LocalSystemCommunication>());

        return new SystemMetricsParser(
            configuration,
            communication,
            loggerFactory.CreateLogger<SystemMetricsParser>());
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// Prints all sensor values from configuration (channel.0~3)
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("MyFirstDevice: Data received, Count={Count}", e.Data.Count);

        // Print all sensor values based on configuration
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value} (Enabled={Enabled})",
                    sensor.Name,
                    measure.Value,
                    sensor.Config.Enabled);
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using SystemMonitorExample.Communication;
using SystemMonitorExample.Devices;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Communication.Common;

namespace SystemMonitorExample;

/// <summary>
/// Pre-configured system monitor device for local system resource monitoring.
/// Automatically creates LocalSystemCommunication for accessing local OS APIs.
/// </summary>
public class LocalSystemMonitorDevice : SystemMonitorDevice
{
    /// <summary>
    /// Creates a local system monitor device with config key.
    /// Automatically retrieves configuration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from appsettings.json DeviceConfigs section</param>
    public LocalSystemMonitorDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    /// <summary>
    /// Creates a local system monitor device with explicit configuration.
    /// Automatically creates LocalSystemCommunication for local OS API access.
    /// </summary>
    public LocalSystemMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateLocalCommunication(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Creates the LocalSystemCommunication for accessing local system APIs.
    /// </summary>
    private static LocalSystemCommunication CreateLocalCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();
        var logger = context.LoggerFactory.CreateLogger<CommunicationBase>();
        return new LocalSystemCommunication(connectionSettings, logger);
    }

    /// <summary>
    /// Event handler for telemetry data received from device.
    /// Prints all sensor values from configuration.
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("LocalSystemMonitorDevice: Data received, Count={Count}", e.Data.Count);

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

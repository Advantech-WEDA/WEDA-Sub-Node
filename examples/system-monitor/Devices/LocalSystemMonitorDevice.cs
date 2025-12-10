using Microsoft.Extensions.Logging;
using SystemMonitorExample.Communication;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Communication;

namespace SystemMonitorExample.Devices;

/// <summary>
/// Pre-configured system monitor device for local system resource monitoring.
/// Automatically creates LocalSystemCommunication for accessing local OS APIs.
///
/// This class is responsible for creating the Communication layer (LocalSystemCommunication).
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: LocalSystemMonitorDevice -> SystemMonitorDevice -> RequestResponseDeviceBase -> DeviceBase
///
/// Usage:
/// <code>
/// var device = new LocalSystemMonitorDevice(context, configuration);
/// await device.StartAsync();
/// </code>
///
/// For remote system monitoring (e.g., via SSH), create a new subclass:
/// <code>
/// public class RemoteSystemMonitorDevice : SystemMonitorDevice
/// {
///     public RemoteSystemMonitorDevice(IWedaApplicationContext context, DeviceConfiguration config)
///         : base(context, config, CreateSshCommunication(context, config)) { }
/// }
/// </code>
/// </summary>
public class LocalSystemMonitorDevice : SystemMonitorDevice
{
    /// <summary>
    /// Creates a local system monitor device with ApplicationContext only.
    /// Automatically retrieves configuration from context and creates local communication.
    /// </summary>
    public LocalSystemMonitorDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
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

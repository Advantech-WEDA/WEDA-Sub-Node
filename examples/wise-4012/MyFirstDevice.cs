using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace Wise4012Example;

/// <summary>
/// MyFirstDevice - A custom device implementation
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support with automatic communication setup.
///
/// Demonstrates UC9868: Configuration update from cloud.
///
/// The framework (DeviceBase) automatically handles:
/// - Base configuration updates (sensors, periods)
/// - Persisting configuration to cache (.device-config-cache.json)
///
/// This custom device only needs to:
/// - Report configuration update status back to cloud (updating/success/failed)
/// - Handle any device-specific configuration if needed
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    /// <summary>
    /// Creates MyFirstDevice using ApplicationContext and config key (for WedaApplicationBuilder pattern).
    /// Configuration is retrieved from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from devicecfg.json DeviceConfigs section</param>
    public MyFirstDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        // Subscribe to DataReceived event to process telemetry
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Creates MyFirstDevice using DeviceConfiguration directly (for SubNode pattern).
    /// </summary>
    public MyFirstDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
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
                    sensor.Report.Enabled);
            }
        }
    }

    /// <summary>
    /// UC9868: Handle configuration update from cloud.
    ///
    /// Note: Base configuration (sensors, periods) is already applied and cached
    /// by the framework before this hook is called. The framework also handles
    /// validation, "updating" status, and "success/failed" status reports.
    ///
    /// This hook is used for:
    /// - Handle any device-specific custom configuration that the framework doesn't know about
    /// </summary>
    protected override Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        _logger.LogInformation("Configuration update applied for device: {SubNodeId}", SubNodeId);

        // Get the strongly-typed message from the event
        var message = e.Message;
        if (message?.Data?.Cfg?.Desired == null)
        {
            _logger.LogWarning("Configuration update event does not contain valid desired configuration");
            return Task.CompletedTask;
        }

        // Handle device-specific custom configuration here
        // Base sensors and periods are already applied by the framework
        // Example: Apply custom properties specific to this device type
        // var customConfig = message.Data?.Cfg?.Desired?.CustomProperties;
        // ApplyCustomConfiguration(customConfig);

        _logger.LogInformation("Custom configuration processing completed");
        return Task.CompletedTask;
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}

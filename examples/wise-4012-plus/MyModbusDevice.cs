using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace Wise4012PlusExample;

/// <summary>
/// MyModbusDevice - A Modbus TCP device connecting to WISE-4012.
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support with automatic communication setup.
///
/// This example demonstrates:
/// - Reading 4 AI channels from WISE-4012 via Modbus TCP
/// - Using the new split configuration files (devicecfg.json, systemcfg.json)
/// - Cloud telemetry and configuration sync
/// </summary>
public class MyModbusDevice : TcpModbusDevice
{
    /// <summary>
    /// Creates MyModbusDevice using ApplicationContext and DeviceConfiguration.
    /// Required constructor for WedaApplicationBuilder.AddDevice&lt;TDevice&gt;(sectionName).
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configuration">The device configuration</param>
    public MyModbusDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to DataReceived event to process telemetry
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Event handler for telemetry data received from device.
    /// Prints all sensor values from configuration (channel.0~3).
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("MyModbusDevice: Data received, Count={Count}", e.Data.Count);

        // Print all sensor values based on configuration
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("[{SensorName}] Value: {Value} (Enabled={Enabled})",
                    sensor.Name,
                    measure.Value,
                    sensor.Config.Enabled);
            }
        }
    }

    /// <summary>
    /// Hook: Called when custom configuration update is received from cloud.
    /// Override this to handle application-specific configuration changes.
    /// </summary>
    protected override Task<CustomConfigUpdateResult> OnCustomConfigUpdateAsync(
        Dictionary<string, object> customConfig,
        CancellationToken ct)
    {
        _logger.LogInformation("Custom config update received with {Count} properties", customConfig.Count);

        // Example: Handle custom properties
        foreach (var (key, value) in customConfig)
        {
            _logger.LogInformation("  Custom property: {Key} = {Value}", key, value);
        }

        return Task.FromResult(CustomConfigUpdateResult.Success());
    }

    /// <summary>
    /// Hook: Called after configuration update is applied.
    /// Base configuration (sensors, periods) is already applied and cached by the framework.
    /// </summary>
    protected override Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        _logger.LogInformation("Configuration update applied for device: {DeviceId}", DeviceId);
        _logger.LogInformation("Current sensor count: {Count}", Configuration.Sensors.Count);

        foreach (var sensor in Configuration.Sensors)
        {
            _logger.LogInformation("  Sensor: {Name}, Enabled={Enabled}, Interval={Interval}ms",
                sensor.Name, sensor.Config.Enabled, sensor.Config.Interval);
        }

        return Task.CompletedTask;
    }

    ~MyModbusDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}

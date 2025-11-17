using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace Wise4012Example;

/// <summary>
/// MyFirstDevice - A custom device implementation
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support with automatic communication setup
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    /// <summary>
    /// Creates MyFirstDevice using ApplicationContext.
    /// Configuration is automatically retrieved from context.
    /// </summary>
    public MyFirstDevice(IWedaApplicationContext context)
        : base(context)
    {
        // Subscribe to DataReceived event to process telemetry
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
                _logger.LogInformation("{SensorName}: {Value}",
                    sensor.Name,
                    measure.Value);
            }
        }
    }

    // Note: GetHealthAsync is no longer overridable in new DeviceBase
    // Health monitoring is handled by DeviceHealthMonitor component

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}

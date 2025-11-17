using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace BasicModbusBuilderExample;

/// <summary>
/// Custom Modbus device implementation using Builder pattern
/// Demonstrates basic event handling and telemetry processing
/// </summary>
public class MyModbusDevice : TcpModbusDevice
{
    public MyModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to DataReceived event to process telemetry
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("Data received from device, Count={Count}", e.Data.Count);

        // Print all sensor values
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value} {Unit}",
                    sensor.Name,
                    measure.Value,
                    sensor.Config.Unit ?? "");
            }
        }

        // Add your custom logic here:
        // - Apply business rules
        // - Trigger alerts based on thresholds
        // - Store data to local database
        // - Send commands to device
        // - etc.
    }

    ~MyModbusDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}

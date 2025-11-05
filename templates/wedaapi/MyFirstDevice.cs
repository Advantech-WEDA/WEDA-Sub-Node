using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Devices.Generic;

namespace WedaApi;

/// <summary>
/// MyFirstDevice - Example Modbus TCP device implementation
/// This demonstrates how to create a custom device by inheriting from TcpModbusDevice
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to DataReceived event to process telemetry
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// Logs all sensor values in a user-friendly format
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("MyFirstDevice: Data received from device, Count={Count}", e.Data.Count);

        // Process and log each sensor value
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                var displayName = GetDisplayName(sensor.Name);
                var unit = GetUnitForSensor(sensor.SensorGroup);
                var formattedValue = FormatValue(measure.Value);

                _logger.LogInformation(
                    "{SensorName}: {Value} {Unit}",
                    displayName,
                    formattedValue,
                    unit);
            }
        }

        // Add your custom business logic here
        // Examples:
        // - Apply business rules
        // - Trigger alerts based on threshold values
        // - Send notifications
        // - Store data to database
        // - Forward to other systems
    }

    private static string GetDisplayName(string sensorName)
    {
        // Convert "temperature.sensor" to "Temperature Sensor"
        var parts = sensorName.Split('.');
        var displayParts = parts.Select(part =>
            char.ToUpper(part[0]) + part.Substring(1).ToLower());
        return string.Join(" ", displayParts);
    }

    private static string GetUnitForSensor(SensorGroup sensorGroup)
    {
        return sensorGroup switch
        {
            SensorGroup.TEMP => "°C",
            SensorGroup.PWR => "V/A",
            SensorGroup.AI => "",
            SensorGroup.AO => "",
            SensorGroup.DI => "",
            SensorGroup.DO => "",
            _ => ""
        };
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "null";

        // Format numeric values with 2 decimal places
        if (value is double d) return d.ToString("F2");
        if (value is float f) return f.ToString("F2");
        if (value is decimal dec) return dec.ToString("F2");

        // Try to convert to double
        if (double.TryParse(value.ToString(), out var dblValue))
            return dblValue.ToString("F2");

        return value.ToString() ?? "null";
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}

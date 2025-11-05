using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Devices.Generic;

namespace WedaApiC;

/// <summary>
/// MyFirstDevice - Example Modbus TCP device implementation (Advanced Control Pattern)
/// This demonstrates how to create a custom device with full control over:
/// - Event subscriptions
/// - Data processing logic
/// - Custom business rules
/// - Alert triggering
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    private int _dataReceivedCount = 0;

    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to all device events for full observability
        DataReceived += OnDataReceived;
        TelemetrySent += OnTelemetrySent;
        ConnectionStateChanged += OnConnectionStateChanged;
        DeviceStatusChanged += OnDeviceStatusChanged;
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// Demonstrates advanced data processing and business logic
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _dataReceivedCount++;

        _logger.LogDebug(
            "MyFirstDevice: Data batch #{Count} received from device, Measures={MeasureCount}",
            _dataReceivedCount,
            e.Data.Count);

        // Process each sensor value
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                var displayName = GetDisplayName(sensor.Name);
                var unit = GetUnitForSensor(sensor.SensorGroup);
                var formattedValue = FormatValue(measure.Value);

                _logger.LogInformation(
                    "{SensorName}: {Value} {Unit} (Timestamp: {Timestamp})",
                    displayName,
                    formattedValue,
                    unit,
                    DateTimeOffset.FromUnixTimeMilliseconds(measure.Timestamp).ToString("HH:mm:ss"));

                // Example: Apply business rules based on sensor values
                ApplyBusinessRules(sensor.Name, measure.Value, sensor.SensorGroup);
            }
        }
    }

    /// <summary>
    /// Example business rule processing
    /// You can customize this based on your application requirements
    /// </summary>
    private void ApplyBusinessRules(string sensorName, object value, SensorGroup sensorGroup)
    {
        // Example: Temperature threshold alert
        if (sensorGroup == SensorGroup.TEMP && value is double temperature)
        {
            if (temperature > 30.0)
            {
                _logger.LogWarning("High temperature detected: {Temp}°C from {Sensor}", temperature, sensorName);
            }
            else if (temperature < 20.0)
            {
                _logger.LogWarning("Low temperature detected: {Temp}°C from {Sensor}", temperature, sensorName);
            }
        }

        // Add more business rules here:
        // - Power threshold monitoring
        // - Anomaly detection
        // - Predictive maintenance alerts
        // - Data quality checks
        // - etc.
    }

    private void OnTelemetrySent(object? sender, TelemetrySentEvent e)
    {
        if (e.Success)
        {
            _logger.LogDebug(
                "Telemetry sent successfully: {Count} measures",
                e.MeasureCount);
        }
        else
        {
            _logger.LogError(
                "Failed to send telemetry: {Error}",
                e.Error ?? "Unknown error");
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEvent e)
    {
        _logger.LogInformation(
            "Connection state changed: {Previous} → {Current}",
            e.PreviousState,
            e.CurrentState);
    }

    private void OnDeviceStatusChanged(object? sender, DeviceStatusChangedEvent e)
    {
        _logger.LogInformation(
            "Device status changed: {Previous} → {Current}",
            e.PreviousStatus,
            e.CurrentStatus);
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
        // Unsubscribe from all events
        DataReceived -= OnDataReceived;
        TelemetrySent -= OnTelemetrySent;
        ConnectionStateChanged -= OnConnectionStateChanged;
        DeviceStatusChanged -= OnDeviceStatusChanged;
    }
}

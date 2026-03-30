using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Core.Transforms;
using Weda.SubNode.Devices.Generic;

namespace TransformDspProgrammatic;

/// <summary>
/// MyFirstDevice - Demonstrates programmatic Transform and DSP Filter usage
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Apply transforms and DSP filters programmatically
        ConfigureTransformsAndFilters();

        // Subscribe to DataReceived event to process telemetry
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Configure transforms and DSP filters programmatically for each sensor
    /// Uses the new ConfigureTransforms/ConfigureDspFilters API for clear separation
    /// </summary>
    private void ConfigureTransformsAndFilters()
    {
        // Find the temperature sensor
        var tempSensor = Configuration.Sensors.FirstOrDefault(s => s.Name == "temperature_sensor");
        if (tempSensor != null)
        {
            _logger.LogInformation("Configuring Transform and DSP Filter for {SensorName}", tempSensor.Name);

            // Configure Transform Pipeline (executed first)
            tempSensor.Report.ConfigureTransforms(transforms =>
            {
                // 1. Calibration: Convert raw value (example: scale and offset)
                transforms.Add(new CalibrationTransform(scale: 1.0, offset: 0.0));

                // 2. Unit Conversion: Convert Celsius to Fahrenheit
                transforms.Add(new UnitConversionTransform(
                    fromUnit: "celsius",
                    toUnit: "fahrenheit"));
            });

            // Configure DSP Filter Pipeline (executed after transforms)
            tempSensor.Report.ConfigureDspFilters(filters =>
            {
                // 1. Moving Average Filter: Smooth out noise with window size of 5
                filters.Add(new MovingAverageFilter(5));  // Constructor takes window size
            });

            _logger.LogInformation("Applied: Calibration → UnitConversion (°C→°F) → MovingAverage(5)");
        }
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("Data received from device, Count={Count}", e.Data.Count);

        // Print all sensor values (after transforms and filters have been applied)
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value:F2}°F (after calibration, unit conversion, and moving average)",
                    sensor.Name,
                    measure.Value);
            }
        }

        // Add your custom logic here:
        // - Apply business rules
        // - Trigger alerts based on thresholds
        // - Store data to local database
        // - etc.
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}

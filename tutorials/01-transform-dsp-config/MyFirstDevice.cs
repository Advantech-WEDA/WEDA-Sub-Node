using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace TransformDspConfig;

/// <summary>
/// MyFirstDevice - Demonstrates config-based Transform and DSP Filter usage
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support
/// All transforms and filters are configured in appsettings.json
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // No programmatic configuration needed!
        // All transforms and DSP filters are loaded from appsettings.json
        
        LogPipelineConfiguration();

        // Subscribe to DataReceived event to process telemetry
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Log the pipeline configuration loaded from appsettings.json
    /// </summary>
    private void LogPipelineConfiguration()
    {
        var tempSensor = Configuration.Sensors.FirstOrDefault(s => s.Name == "temperature.sensor");
        if (tempSensor != null)
        {
            _logger.LogInformation("Transform and DSP Filter loaded from appsettings.json for {SensorName}", tempSensor.Name);
            
            if (tempSensor.Config.TransformPipeline.Count > 0)
            {
                _logger.LogInformation("TransformPipeline: {Count} transforms configured",
                    tempSensor.Config.TransformPipeline.Count);
                for (int i = 0; i < tempSensor.Config.TransformPipeline.Count; i++)
                {
                    var transform = tempSensor.Config.TransformPipeline[i];
                    _logger.LogInformation("  - [Index={Index}] {Type} (Enabled={Enabled})",
                        i, transform.Type, transform.Enabled);
                }
            }

            if (tempSensor.Config.DspPipeline.Count > 0)
            {
                _logger.LogInformation("DspPipeline: {Count} filters configured",
                    tempSensor.Config.DspPipeline.Count);
                for (int i = 0; i < tempSensor.Config.DspPipeline.Count; i++)
                {
                    var filter = tempSensor.Config.DspPipeline[i];
                    _logger.LogInformation("  - [Index={Index}] {Type} (Enabled={Enabled})",
                        i, filter.Type, filter.Enabled);
                }
            }

            _logger.LogInformation("Pipeline: Calibration → UnitConversion (°C→°F) → MovingAverage(5)");
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
                _logger.LogInformation("{SensorName}: {Value:F2}°F (after config-based pipeline)",
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

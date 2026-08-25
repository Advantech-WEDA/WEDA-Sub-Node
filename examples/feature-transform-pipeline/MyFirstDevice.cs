using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace testdevice;

/// <summary>
/// MyFirstDevice - A simple custom device implementation
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Enable and subscribe to DataReceived event
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;

        // Enable value change tracking for debugging transform/filter pipeline
        EnableValueChangeTracking = true;
        ValueChanged += OnValueChanged;
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
                _logger.LogInformation("{SensorName}: {Value}",
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

    /// <summary>
    /// Event handler for value changes through transform/filter pipeline
    /// </summary>
    private void OnValueChanged(object? sender, TelemetryValueChangedEvent e)
    {
        var stageType = e.Stage == ValueChangeStage.Transform ? "Transform" : "Filter";

        _logger.LogDebug(
            "[{StageType}] {StageName} (#{StageIndex}) for {ResourceId}: {InputCount} -> {OutputCount} values ({Duration:F2}ms)",
            stageType,
            e.StageName,
            e.StageIndex,
            e.ResourceId,
            e.InputValues.Count,
            e.OutputValues.Count,
            e.Duration?.TotalMilliseconds ?? 0);

        // Log detailed value changes if values were modified
        if (e.ValuesChanged)
        {
            for (int i = 0; i < Math.Max(e.InputValues.Count, e.OutputValues.Count); i++)
            {
                var inputVal = i < e.InputValues.Count ? e.InputValues[i].Value?.ToString() : "(none)";
                var outputVal = i < e.OutputValues.Count ? e.OutputValues[i].Value?.ToString() : "(filtered)";

                if (inputVal != outputVal)
                {
                    _logger.LogInformation(
                        "  [{Index}] {Input} -> {Output}",
                        i,
                        inputVal,
                        outputVal);
                }
            }
        }
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
        ValueChanged -= OnValueChanged;
    }
}

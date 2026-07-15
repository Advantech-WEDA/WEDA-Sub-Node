using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace opcua_device;

/// <summary>
/// MyOpcUaDevice - OPC-UA device using Request-Response pattern (polling).
/// Inherits from TcpOpcUaRequestResponseDevice for timer-driven batch ReadNodesAsync.
///
/// To use Subscription (server-push) instead, change the base class to:
///   TcpOpcUaPubSubDevice
/// No other code changes needed — the parser supports both patterns.
/// </summary>
[DeviceType(Sensors.OpcUaDevice.DeviceTypeName)]
public class MyOpcUaDevice : TcpOpcUaRequestResponseDevice
{
    public MyOpcUaDevice(
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
    /// Event handler for telemetry data received from OPC-UA server
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("Data received from OPC-UA server, Count={Count}", e.Data.Count);

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

    ~MyOpcUaDevice()
    {
        DataReceived -= OnDataReceived;
        ValueChanged -= OnValueChanged;
    }
}

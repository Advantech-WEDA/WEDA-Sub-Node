using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace PowerAggregationExample;

/// <summary>
/// Voltage sensor device that reads voltage values from Modbus simulator.
/// Enables DataReceivedTracking for local monitoring of raw data.
/// PowerAggregatorDevice subscribes to DataProcessed events (processed data).
/// </summary>
public class VoltageSensorDevice : TcpModbusDevice
{
    public VoltageSensorDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    public VoltageSensorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            _logger.LogInformation(
                "[VoltageSensor] DataReceived: ResourceId={ResourceId}, Value={Value}, Timestamp={Timestamp}",
                measure.ResourceId, measure.Value, e.Timestamp);
        }
    }
}

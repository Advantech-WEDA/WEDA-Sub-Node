using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Devices.Generic;

namespace PowerAggregationExample.Devices;

public class CurrentSensorDevice : TcpModbusDevice
{
    public CurrentSensorDevice(IWedaApplicationContext context)
        : base(context)
    {
    }

    public CurrentSensorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
    }
}
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Devices.Generic;

namespace PowerAggregationExample.Devices;

public class VoltageSensorDevice : TcpModbusDevice
{
    public VoltageSensorDevice(IWedaApplicationContext context)
        : base(context)
    {
    }

    public VoltageSensorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
    }
}

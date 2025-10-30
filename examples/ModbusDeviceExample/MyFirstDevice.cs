using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Devices.Generic;

namespace ModbusDeviceExample;

/// <summary>
/// Custom Modbus device implementation using ApplicationContext pattern
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
    }
}
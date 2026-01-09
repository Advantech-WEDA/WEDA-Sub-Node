namespace Weda.SubNode.Abstractions.Devices;

public class DeviceConfigurations() : Dictionary<string, DeviceConfiguration>(StringComparer.OrdinalIgnoreCase)
{
    public DeviceConfigurations(List<IDevice> devices)
        : this()
    {
        foreach (var device in devices)
        {
            this[device.DeviceName] = device.Configuration;       
        }   
    }
}
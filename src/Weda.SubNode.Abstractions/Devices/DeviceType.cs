namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device type enumeration for WEDA SubNode platform
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// ADAM Ethernet-based devices
    /// Communication Protocol: Modbus TCP/IP, HTTP/HTTPS
    /// </summary>
    AdamEthernet,

    /// <summary>
    /// Serial communication devices
    /// Communication Protocol: Modbus RTU/ASCII, Custom Protocols
    /// </summary>
    SerialDevice,

    /// <summary>
    /// High-speed data acquisition cards
    /// Communication Protocol: PCI/PCIe drivers
    /// </summary>
    DaqDevice,

    /// <summary>
    /// Built-in system monitoring
    /// Communication Protocol: System APIs
    /// </summary>
    SystemMonitor,

    /// <summary>
    /// User-defined custom devices
    /// Communication Protocol: Extensible protocols
    /// </summary>
    CustomDevice
}

/// <summary>
/// Extension methods for DeviceType
/// </summary>
public static class DeviceTypeExtensions
{
    /// <summary>
    /// Convert DeviceType enum to string representation
    /// </summary>
    public static string ToStringValue(this DeviceType deviceType)
    {
        return deviceType switch
        {
            DeviceType.AdamEthernet => "adamEthernet",
            DeviceType.SerialDevice => "serialDevice",
            DeviceType.DaqDevice => "daqDevice",
            DeviceType.SystemMonitor => "systemMonitor",
            DeviceType.CustomDevice => "customDevice",
            _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, null)
        };
    }

    /// <summary>
    /// Parse string to DeviceType enum
    /// </summary>
    public static DeviceType ParseDeviceType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "adamethernet" => DeviceType.AdamEthernet,
            "serialdevice" => DeviceType.SerialDevice,
            "daqdevice" => DeviceType.DaqDevice,
            "systemmonitor" => DeviceType.SystemMonitor,
            "customdevice" => DeviceType.CustomDevice,
            _ => throw new ArgumentException($"Unknown device type: {value}", nameof(value))
        };
    }
}
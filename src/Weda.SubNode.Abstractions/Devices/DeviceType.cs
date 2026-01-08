namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device type enumeration for WEDA SubNode platform
/// </summary>
public enum SubNodeType
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
/// Extension methods for SubNodeType
/// </summary>
public static class SubNodeTypeExtensions
{
    /// <summary>
    /// Convert SubNodeType enum to string representation
    /// </summary>
    public static string ToStringValue(this SubNodeType deviceType)
    {
        return deviceType switch
        {
            SubNodeType.AdamEthernet => "adamEthernet",
            SubNodeType.SerialDevice => "serialDevice",
            SubNodeType.DaqDevice => "daqDevice",
            SubNodeType.SystemMonitor => "systemMonitor",
            SubNodeType.CustomDevice => "customDevice",
            _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, null)
        };
    }

    /// <summary>
    /// Parse string to SubNodeType enum
    /// </summary>
    public static SubNodeType ParseSubNodeType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "adamethernet" => SubNodeType.AdamEthernet,
            "serialdevice" => SubNodeType.SerialDevice,
            "daqdevice" => SubNodeType.DaqDevice,
            "systemmonitor" => SubNodeType.SystemMonitor,
            "customdevice" => SubNodeType.CustomDevice,
            _ => throw new ArgumentException($"Unknown device type: {value}", nameof(value))
        };
    }
}
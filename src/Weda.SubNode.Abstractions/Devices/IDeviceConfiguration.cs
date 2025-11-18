namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Interface for strongly-typed device configurations that can be converted to the generic DeviceConfiguration format.
/// All device-specific configuration classes (e.g., TcpModbusDeviceConfiguration, MqttISensingDeviceConfiguration)
/// should implement this interface to provide a consistent programmatic configuration API.
/// </summary>
public interface IDeviceConfiguration
{
    /// <summary>
    /// Converts this strongly-typed configuration to the generic DeviceConfiguration
    /// used by the DeviceBase framework.
    /// </summary>
    /// <returns>A DeviceConfiguration instance that can be used to initialize devices.</returns>
    DeviceConfiguration ToDeviceConfiguration();
}
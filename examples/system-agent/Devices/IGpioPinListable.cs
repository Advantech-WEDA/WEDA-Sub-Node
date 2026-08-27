using SystemAgentExample.Models;

using Weda.SubNode.Abstractions.Devices;

namespace SystemAgentExample.Devices;

/// <summary>
/// Capability interface for devices that can enumerate their GPIO pins.
/// Mirrors the SDK capability pattern (e.g. IDigitalOutputControllable) at the
/// example level: the gpio.list handler discovers devices through this contract
/// instead of a concrete device type.
/// </summary>
public interface IGpioPinListable : IDevice
{
    /// <summary>
    /// Lists all GPIO pins with their direction and current level.
    /// </summary>
    List<GpioPinDescriptor> ListGpioPins();
}

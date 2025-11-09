using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Resolves DeviceType to concrete IDevice instance.
/// Allows SDK users to register custom device types without modifying SDK code.
/// </summary>
public interface IDeviceTypeResolver
{
    /// <summary>
    /// Check if this resolver can handle the specified DeviceType
    /// </summary>
    bool CanResolve(DeviceType deviceType);

    /// <summary>
    /// Create device instance from configuration
    /// </summary>
    /// <param name="context">Application context providing framework services</param>
    /// <param name="configuration">Device configuration</param>
    /// <returns>Created device instance</returns>
    IDevice CreateDevice(IWedaApplicationContext context, DeviceConfiguration configuration);
}

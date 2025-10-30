using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Factory interface for creating device instances with dependency injection
/// </summary>
public interface IDeviceFactory
{
    /// <summary>
    /// Create a device instance with specified communication
    /// </summary>
    /// <typeparam name="TDevice">The concrete device type that implements IDevice</typeparam>
    /// <param name="configuration">Device configuration</param>
    /// <param name="communication">Communication implementation</param>
    /// <returns>Device instance with all dependencies injected</returns>
    TDevice CreateDevice<TDevice>(
        DeviceConfiguration configuration,
        ICommunication communication)
        where TDevice : class, IDevice;
}

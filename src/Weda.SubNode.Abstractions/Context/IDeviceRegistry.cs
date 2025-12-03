using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Registry for managing device instances within an application context.
/// Enables device discovery and cross-device communication.
/// </summary>
public interface IDeviceRegistry
{
    /// <summary>
    /// Registers a device with the registry.
    /// </summary>
    /// <param name="device">The device to register</param>
    void Register(IDevice device);

    /// <summary>
    /// Unregisters a device from the registry.
    /// </summary>
    /// <param name="device">The device to unregister</param>
    void Unregister(IDevice device);

    /// <summary>
    /// Gets a device by name. Throws if not found.
    /// </summary>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance</returns>
    /// <exception cref="KeyNotFoundException">Thrown when device is not found</exception>
    IDevice GetDevice(string deviceName);

    /// <summary>
    /// Gets a device by name with specific type. Throws if not found or type mismatch.
    /// </summary>
    /// <typeparam name="TDevice">The expected device type</typeparam>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance cast to TDevice</returns>
    /// <exception cref="KeyNotFoundException">Thrown when device is not found</exception>
    /// <exception cref="InvalidCastException">Thrown when device cannot be cast to TDevice</exception>
    TDevice GetDevice<TDevice>(string deviceName) where TDevice : IDevice;

    /// <summary>
    /// Finds a device by name. Returns null if not found.
    /// </summary>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance or null</returns>
    IDevice? FindDevice(string deviceName);

    /// <summary>
    /// Finds a device by name with specific type. Returns null if not found or type mismatch.
    /// </summary>
    /// <typeparam name="TDevice">The expected device type</typeparam>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance cast to TDevice, or null</returns>
    TDevice? FindDevice<TDevice>(string deviceName) where TDevice : class, IDevice;

    /// <summary>
    /// Gets all registered devices.
    /// </summary>
    IReadOnlyCollection<IDevice> GetAllDevices();

    /// <summary>
    /// Gets all registered devices of a specific type.
    /// </summary>
    /// <typeparam name="TDevice">The device type to filter by</typeparam>
    IReadOnlyCollection<TDevice> GetAllDevices<TDevice>() where TDevice : IDevice;

    /// <summary>
    /// Checks if a device with the given name is registered.
    /// </summary>
    /// <param name="deviceName">The device name to check</param>
    bool Contains(string deviceName);

    /// <summary>
    /// Gets the number of registered devices.
    /// </summary>
    int Count { get; }
}

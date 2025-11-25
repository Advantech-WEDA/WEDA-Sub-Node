using System.Collections.Concurrent;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Core.Context;

/// <summary>
/// Thread-safe implementation of IDeviceRegistry for managing device instances.
/// </summary>
public class DeviceRegistry : IDeviceRegistry
{
    private readonly ConcurrentDictionary<string, IDevice> _devices = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var deviceName = device.Configuration.DeviceName;
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("Device must have a valid DeviceName", nameof(device));
        }

        if (!_devices.TryAdd(deviceName, device))
        {
            throw new InvalidOperationException($"Device with name '{deviceName}' is already registered");
        }
    }

    /// <inheritdoc />
    public void Unregister(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var deviceName = device.Configuration.DeviceName;
        _devices.TryRemove(deviceName, out _);
    }

    /// <inheritdoc />
    public IDevice GetDevice(string deviceName)
    {
        return FindDevice(deviceName)
            ?? throw new KeyNotFoundException($"Device '{deviceName}' not found in registry");
    }

    /// <inheritdoc />
    public TDevice GetDevice<TDevice>(string deviceName) where TDevice : IDevice
    {
        var device = GetDevice(deviceName);

        if (device is not TDevice typedDevice)
        {
            throw new InvalidCastException(
                $"Device '{deviceName}' is of type '{device.GetType().Name}', " +
                $"but expected type '{typeof(TDevice).Name}'");
        }

        return typedDevice;
    }

    /// <inheritdoc />
    public IDevice? FindDevice(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return null;

        _devices.TryGetValue(deviceName, out var device);
        return device;
    }

    /// <inheritdoc />
    public TDevice? FindDevice<TDevice>(string deviceName) where TDevice : class, IDevice
    {
        var device = FindDevice(deviceName);
        return device as TDevice;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IDevice> GetAllDevices()
    {
        return _devices.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<TDevice> GetAllDevices<TDevice>() where TDevice : IDevice
    {
        return _devices.Values.OfType<TDevice>().ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public bool Contains(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return false;

        return _devices.ContainsKey(deviceName);
    }

    /// <inheritdoc />
    public int Count => _devices.Count;
}
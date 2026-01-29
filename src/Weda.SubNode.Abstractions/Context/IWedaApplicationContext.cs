using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Application context for Weda SubNode SDK.
/// Manages the lifecycle of framework-level services (cloud, logging, etc.).
/// Provides device registry for cross-device communication.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sub-Node Architecture:</b>
/// A Sub-Node is a single dotnet program that may manage multiple internal devices.
/// From the cloud's perspective, the entire Sub-Node is treated as a single "virtual device"
/// with one globally unique DeviceId (see <see cref="SubNodeInfo"/>).
/// </para>
/// <para>
/// Internal devices are identified by their DeviceName (unique within the Sub-Node).
/// Sensor ResourceIds are generated using: sha1(SubNode.DeviceId + DeviceName + SensorName).
/// </para>
/// </remarks>
public interface IWedaApplicationContext : IDisposable
{
    /// <summary>
    /// Gets the Sub-Node information including name and cloud-assigned DeviceId.
    /// The Sub-Node represents this entire application as a single device to the cloud.
    /// </summary>
    SubNodeInfo SubNodeInfo { get; }

    /// <summary>
    /// Gets the cloud service instance.
    /// </summary>
    IWedaCloudService CloudService { get; }

    /// <summary>
    /// Gets the recording service for local data storage.
    /// </summary>
    IRecordingService? RecordingService { get; }
    
    /// <summary>
    /// Gets the recording service for local data storage.
    /// </summary>
    RecordingOptions? RecordingOptions { get; }

    /// <summary>
    /// Gets the logger factory instance.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets the connection options for device connection manager (Communication Layer).
    /// </summary>
    ConnectionOptions ConnectionOptions { get; }

    /// <summary>
    /// Gets the device feature options (Application Layer).
    /// </summary>
    DeviceOptions DeviceOptions { get; }

    /// <summary>
    /// Gets the configuration instance.
    /// </summary>
    IConfiguration? Configuration { get; }

    /// <summary>
    /// Gets all device configurations loaded from devicecfg.json "DeviceConfigs" section.
    /// Key is the config key (e.g., "MyFirstDevice"), value is the DeviceConfiguration.
    /// </summary>
    /// <example>
    /// <code>
    /// // List all available configs
    /// foreach (var key in context.DeviceConfigs.Keys)
    ///     Console.WriteLine($"Available: {key}");
    ///
    /// // Access specific config
    /// var config = context.DeviceConfigs["MyFirstDevice"];
    /// </code>
    /// </example>
    IReadOnlyDictionary<string, DeviceConfiguration> DeviceConfigs { get; }

    /// <summary>
    /// Gets a device configuration by config key.
    /// Shortcut for DeviceConfigs[configKey].
    /// </summary>
    /// <param name="configKey">The configuration key from devicecfg.json DeviceConfigs section</param>
    /// <returns>The device configuration</returns>
    /// <exception cref="KeyNotFoundException">Thrown when config key is not found</exception>
    /// <example>
    /// <code>
    /// var device = new TcpModbusDevice(context, context["MyFirstDevice"]);
    /// </code>
    /// </example>
    DeviceConfiguration this[string configKey] { get; }

    /// <summary>
    /// Gets the device registry for managing and discovering devices.
    /// All devices created with this context are automatically registered.
    /// </summary>
    IDeviceRegistry DeviceRegistry { get; }

    /// <summary>
    /// Gets the configuration cache for persisting cloud-updated configurations.
    /// When configuration is updated from cloud, changes are cached locally
    /// so device restart uses the latest cloud-provided config instead of the local config files.
    /// </summary>
    IConfigurationCache ConfigurationCache { get; }

    /// <summary>
    /// Gets the SubNode manager for centralized SubNode-level operations.
    /// Manages cloud connection, SubNode registration, and event subscriptions.
    /// All devices within this SubNode share the same SubNodeId and subscriptions.
    /// </summary>
    ISubNodeManager SubNodeManager { get; }

    /// <summary>
    /// Gets a typed logger for the specified type.
    /// </summary>
    ILogger<T> GetLogger<T>();

    // ===== Device Registry Convenience Methods =====

    /// <summary>
    /// Gets a device by name. Throws if not found.
    /// Convenience method that delegates to DeviceRegistry.GetDevice.
    /// </summary>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance</returns>
    /// <exception cref="KeyNotFoundException">Thrown when device is not found</exception>
    IDevice GetDevice(string deviceName);

    /// <summary>
    /// Gets a device by name with specific type. Throws if not found or type mismatch.
    /// Convenience method that delegates to DeviceRegistry.GetDevice&lt;TDevice&gt;.
    /// </summary>
    /// <typeparam name="TDevice">The expected device type</typeparam>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance cast to TDevice</returns>
    /// <exception cref="KeyNotFoundException">Thrown when device is not found</exception>
    /// <exception cref="InvalidCastException">Thrown when device cannot be cast to TDevice</exception>
    TDevice GetDevice<TDevice>(string deviceName) where TDevice : IDevice;

    /// <summary>
    /// Finds a device by name. Returns null if not found.
    /// Convenience method that delegates to DeviceRegistry.FindDevice.
    /// </summary>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance or null</returns>
    IDevice? FindDevice(string deviceName);

    /// <summary>
    /// Finds a device by name with specific type. Returns null if not found or type mismatch.
    /// Convenience method that delegates to DeviceRegistry.FindDevice&lt;TDevice&gt;.
    /// </summary>
    /// <typeparam name="TDevice">The expected device type</typeparam>
    /// <param name="deviceName">The device name to search for</param>
    /// <returns>The device instance cast to TDevice, or null</returns>
    TDevice? FindDevice<TDevice>(string deviceName) where TDevice : class, IDevice;

    /// <summary>
    /// Gets all registered devices of a specific type.
    /// Convenience method that delegates to DeviceRegistry.GetAllDevices&lt;TDevice&gt;.
    /// </summary>
    /// <typeparam name="TDevice">The device type to filter by</typeparam>
    /// <returns>A collection of devices matching the specified type</returns>
    /// <example>
    /// <code>
    /// // Get all Modbus devices and set DO
    /// var modbusDevices = context.GetAllDevices&lt;TcpModbusDevice&gt;();
    /// foreach (var device in modbusDevices)
    /// {
    ///     await device.SetDO("do0", true);
    /// }
    /// </code>
    /// </example>
    IReadOnlyCollection<TDevice> GetAllDevices<TDevice>() where TDevice : IDevice;
}

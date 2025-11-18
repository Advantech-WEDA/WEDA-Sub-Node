using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Application context for Weda SubNode SDK.
/// Manages the lifecycle of framework-level services (cloud, logging, etc.).
/// </summary>
public interface IWedaApplicationContext : IDisposable
{
    /// <summary>
    /// Gets the cloud service instance.
    /// </summary>
    IWedaCloudService CloudService { get; }

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
    /// Gets the device configuration loaded from IConfiguration.
    /// Returns null if no configuration was provided or device config not found.
    /// </summary>
    DeviceConfiguration? DeviceConfiguration { get; }

    /// <summary>
    /// Gets a typed logger for the specified type.
    /// </summary>
    ILogger<T> GetLogger<T>();
}

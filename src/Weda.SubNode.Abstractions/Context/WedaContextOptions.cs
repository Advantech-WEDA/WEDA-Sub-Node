using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Configuration options for WedaApplicationContext.
/// </summary>
public class WedaContextOptions
{
    /// <summary>
    /// Gets or sets the cloud service instance.
    /// If not set, a default cloud service will be created.
    /// </summary>
    public IWedaCloudService? CloudService { get; set; }

    /// <summary>
    /// Gets or sets the logger factory instance.
    /// If not set, NullLoggerFactory will be used.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Gets or sets the connection options for DeviceConnectionManager (Communication Layer).
    /// </summary>
    public ConnectionOptions ConnectionOptions { get; set; } = ConnectionOptions.Default;

    /// <summary>
    /// Gets or sets the device feature options (Application Layer).
    /// Controls which features are enabled for devices.
    /// </summary>
    public DeviceOptions DeviceOptions { get; set; } = DeviceOptions.Default;

    /// <summary>
    /// Gets or sets the NATS connection settings.
    /// If not set, defaults to "nats://localhost:4222".
    /// </summary>
    public NatsConnectionSettings NatsConnectionSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to dispose services on context disposal.
    /// Default is true.
    /// </summary>
    public bool DisposeServices { get; set; } = true;

    /// <summary>
    /// Gets or sets the configuration instance.
    /// Used to access application settings.
    /// </summary>
    public IConfiguration? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the device configuration key in the configuration section.
    /// Default is the first key in DeviceConfigs
    /// </summary>
    public string DeviceConfigurationKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to automatically load DTDL from configured path.
    /// Default is true.
    /// </summary>
    public bool AutoLoadDtdl { get; set; } = true;

    /// <summary>
    /// Gets or sets the SubNode configuration.
    /// If set, this takes priority over appsettings.json configuration.
    /// </summary>
    public SubNodeConfiguration? SubNode { get; set; }

    /// <summary>
    /// Configures SubNode settings using fluent API.
    /// </summary>
    /// <param name="configure">Action to configure SubNode</param>
    /// <returns>This options instance for chaining</returns>
    /// <example>
    /// <code>
    /// var context = new WedaApplicationContext(options => options
    ///     .ConfigureSubNode(subnode =>
    ///     {
    ///         subnode.Name = "MySubNode";
    ///         subnode.SubNodeType = SubNodeType.CustomDevice;
    ///         subnode.Manufacturer = "Advantech";
    ///     }));
    /// </code>
    /// </example>
    public WedaContextOptions ConfigureSubNode(Action<SubNodeConfiguration> configure)
    {
        SubNode ??= new SubNodeConfiguration();
        configure(SubNode);
        return this;
    }
}

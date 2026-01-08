using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.Abstractions.Context;

/// <summary>
/// Configuration options for WedaApplicationContext.
/// Supports both programmatic configuration and loading from configuration files.
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
    /// This is equivalent to SystemCfg.WedaNode.
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

    // ===== Programmatic Configuration Objects =====

    /// <summary>
    /// Gets or sets the system configuration (systemcfg.json equivalent).
    /// If set, this takes priority over systemcfg.json configuration.
    /// </summary>
    public SystemCfg? SystemCfg { get; set; }

    /// <summary>
    /// Gets or sets the device configuration (devicecfg.json equivalent).
    /// If set, this takes priority over devicecfg.json configuration.
    /// </summary>
    public DeviceCfg? DeviceCfg { get; set; }

    /// <summary>
    /// Gets or sets the custom configuration (customcfg.json equivalent).
    /// If set, this takes priority over customcfg.json configuration.
    /// </summary>
    public CustomCfg? CustomCfg { get; set; }

    /// <summary>
    /// Gets or sets the SubNode configuration.
    /// If set, this takes priority over devicecfg.json SubNode section.
    /// This is equivalent to DeviceCfg.SubNode.
    /// </summary>
    public SubNodeConfig? SubNode { get; set; }

    // ===== Storage Services (Optional, for DI Integration) =====

    /// <summary>
    /// Gets or sets the device registration storage service.
    /// If not set, a default JsonDeviceRegistrationStorage instance will be created.
    /// This allows sharing storage instances between WedaApplication (DI) and direct device instantiation.
    /// </summary>
    public IDeviceRegistrationStorage? RegistrationStorage { get; set; }

    /// <summary>
    /// Gets or sets the configuration cache service.
    /// If not set, a default JsonConfigurationCache instance will be created.
    /// This allows sharing cache instances between WedaApplication (DI) and direct device instantiation.
    /// </summary>
    public IConfigurationCache? ConfigurationCache { get; set; }

    // ===== Fluent Configuration Methods =====

    /// <summary>
    /// Configures system settings using fluent API.
    /// </summary>
    /// <param name="configure">Action to configure SystemCfg</param>
    /// <returns>This options instance for chaining</returns>
    /// <example>
    /// <code>
    /// var context = new WedaApplicationContext(options => options
    ///     .ConfigureSystem(system =>
    ///     {
    ///         system.WedaNode.Url = "nats://localhost:4222";
    ///         system.WedaNode.AuthStrategy = NatsAuthStrategy.UserPassword;
    ///         system.WedaNode.Username = "user";
    ///         system.WedaNode.Password = "pass";
    ///     }));
    /// </code>
    /// </example>
    public WedaContextOptions ConfigureSystem(Action<SystemCfg> configure)
    {
        SystemCfg ??= new SystemCfg();
        configure(SystemCfg);
        // Keep NatsConnectionSettings in sync for backward compatibility
        NatsConnectionSettings = SystemCfg.WedaNode;
        return this;
    }

    /// <summary>
    /// Configures device settings using fluent API.
    /// </summary>
    /// <param name="configure">Action to configure DeviceCfg</param>
    /// <returns>This options instance for chaining</returns>
    /// <example>
    /// <code>
    /// var context = new WedaApplicationContext(options => options
    ///     .ConfigureDevice(device =>
    ///     {
    ///         device.SubNode.Name = "MySubNode";
    ///         device.SubNode.SubNodeType = SubNodeType.AdamEthernet;
    ///         device.DeviceConfigs["MyDevice"] = new DeviceConfiguration { Enabled = true };
    ///     }));
    /// </code>
    /// </example>
    public WedaContextOptions ConfigureDevice(Action<DeviceCfg> configure)
    {
        DeviceCfg ??= new DeviceCfg();
        configure(DeviceCfg);
        // Keep SubNode in sync for backward compatibility
        SubNode = DeviceCfg.SubNode;
        return this;
    }

    /// <summary>
    /// Configures custom settings using fluent API.
    /// </summary>
    /// <param name="configure">Action to configure CustomCfg</param>
    /// <returns>This options instance for chaining</returns>
    /// <example>
    /// <code>
    /// var context = new WedaApplicationContext(options => options
    ///     .ConfigureCustom(custom =>
    ///     {
    ///         custom["MyCustomSetting"] = "value1";
    ///         custom["FeatureFlags"] = new Dictionary&lt;string, bool&gt;
    ///         {
    ///             ["EnableFeatureX"] = true
    ///         };
    ///     }));
    /// </code>
    /// </example>
    public WedaContextOptions ConfigureCustom(Action<CustomCfg> configure)
    {
        CustomCfg ??= new CustomCfg();
        configure(CustomCfg);
        return this;
    }

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
    public WedaContextOptions ConfigureSubNode(Action<SubNodeConfig> configure)
    {
        SubNode ??= new SubNodeConfig();
        configure(SubNode);
        return this;
    }
}

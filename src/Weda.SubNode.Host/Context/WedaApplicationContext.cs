using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;
using Serilog;
using Serilog.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Cloud;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Cloud.Serialization;
using Weda.SubNode.Core.Context;
using Weda.SubNode.Core.Storage;

namespace Weda.SubNode.Host.Context;

/// <summary>
/// Application context for Weda SubNode SDK.
/// Manages the lifecycle of framework-level services and provides factory methods for creating devices.
/// This is the top-level composition root that assembles all dependencies.
/// </summary>
/// <example>
/// <code>
/// // Simplest usage with default singleton (recommended for single-device scenarios)
/// var device = new TcpModbusDevice(WedaApplicationContext.Default, config);
/// await device.StartAsync();
///
/// // Create new instance with defaults
/// using var context = new WedaApplicationContext();
/// var device = new TcpModbusDevice(context, config);
///
/// // Custom configuration
/// using var context = new WedaApplicationContext(options =>
/// {
///     options.CloudService = myCloudService;
///     options.LoggerFactory = myLoggerFactory;
/// });
/// </code>
/// </example>
public class WedaApplicationContext : IWedaApplicationContext
{
    private static readonly Lazy<WedaApplicationContext> _default = new(
        () => new WedaApplicationContext(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the default singleton instance of WedaApplicationContext.
    /// Uses lazy initialization with thread-safety.
    /// Auto-loads configuration from appsettings.json.
    /// </summary>
    /// <remarks>
    /// The default instance is suitable for simple single-device scenarios.
    /// For multi-device applications or custom configuration, create instances manually
    /// or use WedaApplicationBuilder.
    ///
    /// Note: The default instance should NOT be disposed manually as it is shared.
    /// It will be disposed when the application exits.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple usage with default singleton
    /// var device = new TcpModbusDevice(WedaApplicationContext.Default, config);
    /// await device.StartAsync();
    /// </code>
    /// </example>
    public static WedaApplicationContext Default => _default.Value;

    private readonly WedaContextOptions _options;
    private readonly IWedaCloudService _cloudService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly NatsClient? _natsClient;
    private readonly IConfiguration? _configuration;
    private readonly DeviceConfiguration? _deviceConfiguration;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IConfigurationCache _configurationCache;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of WedaApplicationContext with default options.
    /// </summary>
    public WedaApplicationContext()
        : this(options => { })
    {
    }

    /// <summary>
    /// Initializes a new instance of WedaApplicationContext with custom configuration.
    /// </summary>
    /// <param name="configure">Configuration action for setting up options.</param>
    public WedaApplicationContext(Action<WedaContextOptions> configure)
    {
        _options = new WedaContextOptions();
        configure(_options);

        // Initialize device registry
        _deviceRegistry = new DeviceRegistry();

        // Initialize configuration cache (for UC9868 cloud-driven config updates)
        _configurationCache = new JsonConfigurationCache(
            logger: null); // Logger not available yet

        // Auto-load configuration from appsettings.json if not provided
        if (_options.Configuration == null)
        {
            try
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();
            }
            catch
            {
                // If appsettings.json doesn't exist or fails to load, continue with null configuration
                _configuration = null;
            }
        }
        else
        {
            _configuration = _options.Configuration;
        }

        // Auto-load logger factory from Configuration if not provided
        if (_options.LoggerFactory == null && _configuration != null)
        {
            try
            {
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(_configuration)
                    .CreateLogger();
                _loggerFactory = new SerilogLoggerFactory(logger);
            }
            catch
            {
                // If Serilog configuration fails, use NullLoggerFactory
                _loggerFactory = NullLoggerFactory.Instance;
            }
        }
        else
        {
            _loggerFactory = _options.LoggerFactory ?? NullLoggerFactory.Instance;
        }

        // Auto-load NATS settings from Configuration if not explicitly set
        if (_configuration != null && _options.NatsConnectionSettings == NatsConnectionSettings.Default)
        {
            var natsSection = _configuration.GetSection(NatsConnectionSettings.SectionName);
            if (natsSection.Exists())
            {
                _options.NatsConnectionSettings = new NatsConnectionSettings
                {
                    Url = natsSection["Url"] ?? "nats://localhost:4222",
                    Name = natsSection["Name"] ?? "default",
                    NatsSerializerRegistry = natsSection["SerializerType"]?.ToLower() switch
                    {
                        "json" => WedaNatsSerializerRegistry.Default,
                        _ => WedaNatsSerializerRegistry.Default
                    },
                    AuthStrategy = Enum.TryParse<NatsAuthStrategy>(natsSection["AuthStrategy"], true, out var strategy)
                        ? strategy
                        : NatsAuthStrategy.None,
                    Username = natsSection["Username"],
                    Password = natsSection["Password"],
                    Token = natsSection["Token"],
                    CredFile = natsSection["CredFile"],
                    TlsCertPath = natsSection["TlsCertPath"],
                    TlsKeyPath = natsSection["TlsKeyPath"],
                    TlsCaPath = natsSection["TlsCaPath"]
                };
            }
        }

        // Load device configuration if Configuration is provided
        _deviceConfiguration = LoadDeviceConfiguration();

        // Setup cloud service
        if (_options.CloudService != null)
        {
            _cloudService = _options.CloudService;
        }
        else
        {
            // Create default cloud service with real NATS connection
            (_cloudService, _natsClient) = CreateDefaultCloudService();
        }
    }

    /// <summary>
    /// Initializes a new instance of WedaApplicationContext using IConfiguration.
    /// Reads configuration from "Nats" section and loads device configuration.
    ///
    /// Device configuration selection:
    /// - If deviceConfigKey is empty (default): Automatically selects the first device configuration found under "DeviceConfigs" section
    /// - If deviceConfigKey is specified (e.g., "MyFirstDevice"): Uses "DeviceConfigs:{deviceConfigKey}" path
    /// </summary>
    /// <param name="configuration">Configuration instance.</param>
    /// <param name="deviceConfigKey">
    /// Device configuration key name (without "DeviceConfigs:" prefix).
    /// If empty (default), auto-selects the first device configuration found.
    /// If specified (e.g., "MyFirstDevice"), uses "DeviceConfigs:MyFirstDevice" path.
    /// </param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public WedaApplicationContext(
        IConfiguration configuration,
        ILoggerFactory? loggerFactory = null,
        string deviceConfigKey = "")
        : this(options =>
        {
            options.Configuration = configuration;
            options.LoggerFactory = loggerFactory;

            // Determine the actual device configuration key
            string actualConfigKey;
            if (string.IsNullOrEmpty(deviceConfigKey))
            {
                // Auto-select first device config under DeviceConfigs
                var deviceConfigsSection = configuration.GetSection("DeviceConfigs");
                var firstKey = deviceConfigsSection.GetChildren().FirstOrDefault()?.Key;

                if (firstKey == null)
                {
                    throw new InvalidOperationException(
                        "No device configurations found under 'DeviceConfigs' section in appsettings.json");
                }

                actualConfigKey = $"DeviceConfigs:{firstKey}";
            }
            else
            {
                // Use specified key with DeviceConfigs prefix
                actualConfigKey = $"DeviceConfigs:{deviceConfigKey}";
            }

            options.DeviceConfigurationKey = actualConfigKey;

            // Bind NATS configuration from "Nats" section
            var natsSection = configuration.GetSection("Nats");
            if (natsSection.Exists())
            {
                options.NatsConnectionSettings = new NatsConnectionSettings
                {
                    Url = natsSection["Url"] ?? "nats://localhost:4222",
                    Name = natsSection["Name"] ?? "default",
                    NatsSerializerRegistry = natsSection["SerializerType"]?.ToLower() switch
                    {
                        "json" => WedaNatsSerializerRegistry.Default,
                        _ => WedaNatsSerializerRegistry.Default
                    },
                    AuthStrategy = Enum.TryParse<NatsAuthStrategy>(natsSection["AuthStrategy"], true, out var strategy)
                        ? strategy
                        : NatsAuthStrategy.None,
                    Username = natsSection["Username"],
                    Password = natsSection["Password"],
                    Token = natsSection["Token"],
                    CredFile = natsSection["CredFile"],
                    TlsCertPath = natsSection["TlsCertPath"],
                    TlsKeyPath = natsSection["TlsKeyPath"],
                    TlsCaPath = natsSection["TlsCaPath"]
                };
            }
        })
    {
    }

    /// <inheritdoc />
    public IWedaCloudService CloudService => _cloudService;

    /// <inheritdoc />
    public ILoggerFactory LoggerFactory => _loggerFactory;

    /// <inheritdoc />
    public ILogger<T> GetLogger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>
    /// Gets the connection options for device connection manager (Communication Layer).
    /// </summary>
    public ConnectionOptions ConnectionOptions => _options.ConnectionOptions;

    /// <summary>
    /// Gets the device feature options (Application Layer).
    /// </summary>
    public DeviceOptions DeviceOptions => _options.DeviceOptions;

    /// <summary>
    /// Gets the configuration instance.
    /// </summary>
    public IConfiguration? Configuration => _configuration;

    /// <summary>
    /// Gets the device configuration loaded from IConfiguration.
    /// Returns null if no configuration was provided or device config not found.
    /// </summary>
    public DeviceConfiguration? DeviceConfiguration => _deviceConfiguration;

    /// <inheritdoc />
    public IDeviceRegistry DeviceRegistry => _deviceRegistry;

    /// <inheritdoc />
    public IConfigurationCache ConfigurationCache => _configurationCache;

    // ===== Device Registry Convenience Methods =====

    /// <inheritdoc />
    public IDevice GetDevice(string deviceName) => _deviceRegistry.GetDevice(deviceName);

    /// <inheritdoc />
    public TDevice GetDevice<TDevice>(string deviceName) where TDevice : IDevice
        => _deviceRegistry.GetDevice<TDevice>(deviceName);

    /// <inheritdoc />
    public IDevice? FindDevice(string deviceName) => _deviceRegistry.FindDevice(deviceName);

    /// <inheritdoc />
    public TDevice? FindDevice<TDevice>(string deviceName) where TDevice : class, IDevice
        => _deviceRegistry.FindDevice<TDevice>(deviceName);

    #region Private Methods

    private const string DeviceConfigurationSectionName = "DeviceConfigs";
    private DeviceConfiguration? LoadDeviceConfiguration()
    {
        var logger = _loggerFactory.CreateLogger<WedaApplicationContext>();

        // Priority 1: Check configuration cache (cloud-updated config)
        // This ensures cloud-driven configuration updates persist across restarts
        try
        {
            if (_configurationCache.ExistsAsync().GetAwaiter().GetResult())
            {
                var cachedConfig = _configurationCache.GetConfigurationAsync().GetAwaiter().GetResult();
                if (cachedConfig != null)
                {
                    logger.LogInformation(
                        "Using cached configuration (cloud-updated): DeviceName={DeviceName}, CachePath={CachePath}",
                        cachedConfig.DeviceName,
                        _configurationCache.CacheFilePath);

                    // Auto-load DTDL if enabled
                    LoadDtdlIfEnabled(cachedConfig, logger);

                    return cachedConfig;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to load configuration from cache, falling back to appsettings.json");
        }

        // Priority 2: Load from appsettings.json (initial/fallback config)
        if (_configuration == null)
            return null;

        var configs = _configuration.GetSection(DeviceConfigurationSectionName).GetChildren().FirstOrDefault();
        if (configs == null)
            return null;

        _options.DeviceConfigurationKey = configs.Key;

        try
        {
            var deviceConfig = _configuration
                .GetSection(DeviceConfigurationSectionName)
                .GetSection(_options.DeviceConfigurationKey)
                .Get<DeviceConfiguration>();

            if (deviceConfig == null)
            {
                return null;
            }

            logger.LogInformation(
                "Using appsettings.json configuration: DeviceName={DeviceName}, ConfigKey={ConfigKey}",
                deviceConfig.DeviceName,
                _options.DeviceConfigurationKey);

            // Auto-load DTDL if enabled
            LoadDtdlIfEnabled(deviceConfig, logger);

            return deviceConfig;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load device configuration from: {ConfigKey}", _options.DeviceConfigurationKey);
            return null;
        }
    }

    private void LoadDtdlIfEnabled(DeviceConfiguration deviceConfig, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (_options.AutoLoadDtdl && !string.IsNullOrEmpty(deviceConfig.DtdlPath))
        {
            try
            {
                deviceConfig.LoadDtdl();
                logger.LogInformation("DTDL loaded from: {DtdlPath}", deviceConfig.DtdlPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load DTDL from: {DtdlPath}", deviceConfig.DtdlPath);
            }
        }
    }

    private (IWedaCloudService, NatsClient?) CreateDefaultCloudService()
    {
        var settings = _options.NatsConnectionSettings;
        var natsOpts = NatsOpts.Default with
        {
            Url = settings.Url,
            Name = settings?.Name ?? "default",
            SerializerRegistry = settings!.NatsSerializerRegistry,
            AuthOpts = settings.BuildAuthOpts(),
            TlsOpts = BuildTlsOpts(settings)
        };
        var natsClient = new NatsClient(natsOpts);

        var deviceAgentClient = new DeviceAgentClient(
            natsClient,
            _loggerFactory.CreateLogger<DeviceAgentClient>());

        var telemetryClient = new TelemetryClient(
            natsClient,
            _loggerFactory.CreateLogger<TelemetryClient>());

        var cloudService = new WedaCloudService(
            natsClient,
            deviceAgentClient,
            telemetryClient,
            registrationStorage: null,
            _loggerFactory.CreateLogger<WedaCloudService>());

        return (cloudService, natsClient);
    }

    private static NatsTlsOpts BuildTlsOpts(NatsConnectionSettings settings)
    {
        // Only configure TLS if using TlsCert strategy or TLS paths are provided
        if (settings.AuthStrategy != NatsAuthStrategy.TlsCert &&
            string.IsNullOrEmpty(settings.TlsCertPath) &&
            string.IsNullOrEmpty(settings.TlsCaPath))
        {
            return NatsTlsOpts.Default;
        }

        return new NatsTlsOpts
        {
            CertFile = settings.TlsCertPath,
            KeyFile = settings.TlsKeyPath,
            CaFile = settings.TlsCaPath
        };
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the context and optionally disposes managed services.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && _options.DisposeServices)
        {
            // Dispose cloud service if we created it
            if (_options.CloudService == null)
            {
                _cloudService?.Dispose();
                _natsClient?.DisposeAsync().AsTask().Wait();
            }

            // Dispose logger factory if we created it
            if (_options.LoggerFactory == null)
            {
                _loggerFactory?.Dispose();
            }
        }

        _disposed = true;
    }

    #endregion
}

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
using Weda.SubNode.Core.Cloud;
using Weda.SubNode.Core.Configuration;
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
        () => new WedaApplicationContext(options =>
        {
            // Default singleton enables all features for convenience
            options.DeviceOptions = new DeviceOptions
            {
                EnableCommands = true,
                EnableConfigUpdates = true,
                EnableTelemetry = true,
                EnableHealthReporting = true
            };
        }),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the default singleton instance of WedaApplicationContext.
    /// Uses lazy initialization with thread-safety.
    /// Auto-loads configuration from appsettings.json.
    /// All device features (Commands, ConfigUpdates, Telemetry, HealthReporting) are enabled by default.
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
    private readonly IReadOnlyDictionary<string, DeviceConfiguration> _deviceConfigs;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IConfigurationCache _configurationCache;
    private readonly IDeviceRegistrationStorage _registrationStorage;
    private readonly SubNodeInfo _subNodeInfo;
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

        // Configure WedaFactory to use the logger factory
        // This must be done before any WedaFactory.Cloud.Mock calls
        Core.WedaFactory.UseLoggerFactory(_loggerFactory);

        // If CloudService was set via WedaFactory.Cloud.Mock before logger was configured,
        // log the warning now since the MockCloudService constructor couldn't log it
        if (_options.CloudService is MockCloudService)
        {
            var mockLogger = _loggerFactory.CreateLogger<MockCloudService>();
            mockLogger.LogWarning("╔═════════════════════════════════════════════════════════════════════╗");
            mockLogger.LogWarning("║  MOCK CLOUD SERVICE ACTIVE - No cloud connection established        ║");
            mockLogger.LogWarning("║  All cloud operations will be simulated locally                     ║");
            mockLogger.LogWarning("║                                                                     ║");
            mockLogger.LogWarning("║  To connect to Weda.Node, please remove:                            ║");
            mockLogger.LogWarning("║    options.CloudService = WedaFactory.Cloud.Mock in subnode mode or ║");
            mockLogger.LogWarning("║    UseMockCloud() in wedabuilder mode                               ║"); 
            mockLogger.LogWarning("╚═════════════════════════════════════════════════════════════════════╝");
        }

        // Initialize device registry
        _deviceRegistry = new DeviceRegistry();

        // Initialize configuration cache (for UC9868 cloud-driven config updates)
        _configurationCache = new JsonConfigurationCache(
            logger: _loggerFactory.CreateLogger<JsonConfigurationCache>());

        // Initialize registration storage (for SubNode registration persistence)
        _registrationStorage = new JsonDeviceRegistrationStorage(
            logger: _loggerFactory.CreateLogger<JsonDeviceRegistrationStorage>());

        // Auto-load NATS settings from Configuration if not explicitly set
        if (_configuration != null && _options.NatsConnectionSettings == NatsConnectionSettings.Default)
        {
            var natsSection = _configuration.GetSection(NatsConnectionSettings.SectionName);
            if (natsSection.Exists())
            {
                _options.NatsConnectionSettings = new NatsConnectionSettings
                {
                    Url = natsSection["Url"] ?? "nats://localhost:4222",
                    CredFile = natsSection["CredFile"] ?? string.Empty,
                    Password = natsSection["Password"] ?? string.Empty,
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
                    Token = natsSection["Token"],
                    TlsCertPath = natsSection["TlsCertPath"],
                    TlsKeyPath = natsSection["TlsKeyPath"],
                    TlsCaPath = natsSection["TlsCaPath"]
                };
            }
        }

        // Load SubNode configuration
        _subNodeInfo = LoadSubNodeInfo();

        // Load all device configurations from "DeviceConfigs" section
        _deviceConfigs = LoadAllDeviceConfigurations();

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
                    CredFile = natsSection["CredFile"] ?? string.Empty,
                    Password = natsSection["Password"] ?? string.Empty,
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
                    Token = natsSection["Token"],
                    TlsCertPath = natsSection["TlsCertPath"],
                    TlsKeyPath = natsSection["TlsKeyPath"],
                    TlsCaPath = natsSection["TlsCaPath"]
                };
            }
        })
    {
    }

    /// <inheritdoc />
    public SubNodeInfo SubNodeInfo => _subNodeInfo;

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

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DeviceConfiguration> DeviceConfigs => _deviceConfigs;

    /// <inheritdoc />
    public DeviceConfiguration this[string configKey] =>
        _deviceConfigs.TryGetValue(configKey, out var config)
            ? config
            : throw new KeyNotFoundException($"Device configuration '{configKey}' not found. Available keys: {string.Join(", ", _deviceConfigs.Keys)}");

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

    /// <summary>
    /// Loads SubNode configuration from registration cache first, then appsettings.json.
    /// Priority:
    /// 1. Registration cache (.weda/subnode.registration.json) - for DeviceId
    /// 2. appsettings.json SubNode section - for Name, SubNodeType, Manufacturer, etc.
    /// 3. Default values
    /// </summary>
    private SubNodeInfo LoadSubNodeInfo()
    {
        var assemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "SubNode";
        SubNodeInfo subNodeInfo;

        // Step 1: Load base configuration from appsettings.json
        if (_configuration != null)
        {
            var subNodeSection = _configuration.GetSection(SubNodeConfiguration.SectionName);
            if (subNodeSection.Exists())
            {
                var config = subNodeSection.Get<SubNodeConfiguration>() ?? new SubNodeConfiguration();
                subNodeInfo = config.ToSubNodeInfo(assemblyName);
            }
            else
            {
                // Default: use entry assembly name as SubNode name
                subNodeInfo = new SubNodeInfo
                {
                    Name = assemblyName,
                    Manufacturer = "Advantech",
                    Model = "SubNode-SDK",
                    SwVersion = "1.0.0"
                };
            }
        }
        else
        {
            subNodeInfo = new SubNodeInfo
            {
                Name = assemblyName,
                Manufacturer = "Advantech",
                Model = "SubNode-SDK",
                SwVersion = "1.0.0"
            };
        }

        // Step 2: Try to load DeviceId from registration cache
        try
        {
            var registration = _registrationStorage.GetRegistrationAsync().GetAwaiter().GetResult();
            if (registration != null && !string.IsNullOrEmpty(registration.DeviceId))
            {
                subNodeInfo.DeviceId = registration.DeviceId;
                // Log will be available after logger factory is initialized
            }
        }
        catch
        {
            // If registration cache read fails, continue without DeviceId
            // Device will register on first connection
        }

        return subNodeInfo;
    }

    private const string DeviceConfigurationSectionName = "DeviceConfigs";

    /// <summary>
    /// Loads all device configurations from the "DeviceConfigs" section.
    /// Each configuration is enriched with DeviceName (from key) and SubNodeInfo.
    /// </summary>
    private IReadOnlyDictionary<string, DeviceConfiguration> LoadAllDeviceConfigurations()
    {
        var logger = _loggerFactory.CreateLogger<WedaApplicationContext>();
        var configs = new Dictionary<string, DeviceConfiguration>(StringComparer.OrdinalIgnoreCase);

        if (_configuration == null)
        {
            logger.LogDebug("No configuration provided, DeviceConfigs will be empty");
            return configs;
        }

        var deviceConfigsSection = _configuration.GetSection(DeviceConfigurationSectionName);
        if (!deviceConfigsSection.Exists())
        {
            logger.LogDebug("No '{SectionName}' section found in configuration", DeviceConfigurationSectionName);
            return configs;
        }

        foreach (var configSection in deviceConfigsSection.GetChildren())
        {
            var configKey = configSection.Key;

            try
            {
                var deviceConfig = configSection.Get<DeviceConfiguration>();
                if (deviceConfig == null)
                {
                    logger.LogWarning("Failed to bind configuration for key '{ConfigKey}'", configKey);
                    continue;
                }

                // Auto-enrich: DeviceName defaults to config key
                if (string.IsNullOrWhiteSpace(deviceConfig.DeviceName))
                {
                    deviceConfig.DeviceName = configKey;
                }

                // Auto-enrich: Attach SubNodeInfo
                deviceConfig.SubNodeInfo = _subNodeInfo;

                // Try applying cached cloud configuration (PATCH semantics)
                ApplyCachedConfigurationIfExists(deviceConfig, logger);

                // Auto-load DTDL if enabled
                LoadDtdlIfEnabled(deviceConfig, logger);

                configs[configKey] = deviceConfig;

                logger.LogDebug(
                    "Loaded device configuration: Key={ConfigKey}, DeviceName={DeviceName}, Enabled={Enabled}, Sensors={SensorCount}",
                    configKey,
                    deviceConfig.DeviceName,
                    deviceConfig.Enabled,
                    deviceConfig.Sensors.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load device configuration for key '{ConfigKey}'", configKey);
            }
        }

        logger.LogInformation("Loaded {Count} device configuration(s): [{Keys}]",
            configs.Count,
            string.Join(", ", configs.Keys));

        return configs;
    }

    /// <summary>
    /// Applies cached cloud configuration to a device config if cache exists.
    /// </summary>
    private void ApplyCachedConfigurationIfExists(DeviceConfiguration deviceConfig, Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            if (!_configurationCache.ExistsAsync().GetAwaiter().GetResult())
                return;

            var cachedMessage = _configurationCache.GetRawConfigurationAsync().GetAwaiter().GetResult();
            if (cachedMessage == null)
                return;

            var applied = ConfigurationUpdateHelper.ApplyCachedConfiguration(deviceConfig, cachedMessage);
            if (applied)
            {
                logger.LogInformation(
                    "Applied cached cloud configuration to '{DeviceName}' from {CachePath}",
                    deviceConfig.DeviceName,
                    _configurationCache.CacheFilePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to apply cached configuration to '{DeviceName}'",
                deviceConfig.DeviceName);
        }
    }

    private void LoadDtdlIfEnabled(DeviceConfiguration deviceConfig, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (!_options.AutoLoadDtdl)
            return;

        try
        {
            // Use the new unified DTDL initialization
            // This handles both auto-generation (AutoGenDtdl=true) and file loading (AutoGenDtdl=false)
            deviceConfig.InitializeDtdl(basePath: null, logger);
        }
        catch (InvalidOperationException ex)
        {
            // Validation error - re-throw as this is a configuration issue
            logger.LogError(ex, "DTDL configuration error for device '{DeviceName}'", deviceConfig.DeviceName);
            throw;
        }
        catch (FileNotFoundException ex)
        {
            // File not found - warn but don't fail (backwards compatibility)
            logger.LogWarning(ex, "Failed to load DTDL from: {DtdlPath}", deviceConfig.DtdlPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to initialize DTDL for device '{DeviceName}'", deviceConfig.DeviceName);
        }
    }

    private (IWedaCloudService, NatsClient?) CreateDefaultCloudService()
    {
        var settings = _options.NatsConnectionSettings;
        var natsOpts = NatsOpts.Default with
        {
            Url = settings.Url,
            Name = settings.Name ?? "default",
            SerializerRegistry = settings.NatsSerializerRegistry,
            AuthOpts = GetAuthOpts(settings)
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

    /// <summary>
    /// Get NATS authentication options based on configuration settings.
    /// Passes all configured auth options to NatsAuthOpts, letting NATS client
    /// use its internal priority to select the appropriate authentication method.
    /// </summary>
    private static NatsAuthOpts GetAuthOpts(NatsConnectionSettings settings)
    {
        // If AuthStrategy is explicitly set, use BuildAuthOpts for that specific strategy
        if (settings.AuthStrategy != NatsAuthStrategy.None)
        {
            return settings.BuildAuthOpts();
        }

        // Pass all configured auth options, let NATS client decide priority
        return NatsAuthOpts.Default with
        {
            CredsFile = settings.CredFile,
            Username = settings.Username,
            Password = settings.Password,
            Token = settings.Token
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

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Net;
using Serilog;
using Serilog.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Cloud;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Cloud.Serialization;
using Weda.SubNode.Core;
using Weda.SubNode.Core.Cloud;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Configuration;
using Weda.SubNode.Core.Context;
using Weda.SubNode.Core.Storage;
using Weda.SubNode.Host.Configuration;
using SensorNameValidator = Weda.SubNode.Core.Configuration.SensorNameValidator;

namespace Weda.SubNode.Host.Context;

/// <summary>
/// Application context for Weda SubNode SDK.
/// Manages the lifecycle of framework-level services and provides factory methods for creating devices.
/// This is the top-level composition root that assembles all dependencies.
/// </summary>
/// <example>
/// <code>
/// // Simplest usage with default singleton (recommended for single-device scenarios)
/// var device = new TcpModbusDevice(new WedaApplicationContext(args), config);
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
        () => new WedaApplicationContext(args: Environment.GetCommandLineArgs(), configure: options =>
        {
            // Default singleton enables all features for convenience
            options.DeviceOptions = new DeviceOptions
            {
                EnableCommands = true,
                EnableConfigUpdates = true,
                EnableTelemetry = true,
                EnableHealthReporting = true,
                EnableRecording = true
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
    /// var device = new TcpModbusDevice(new WedaApplicationContext(args), config);
    /// await device.StartAsync();
    /// </code>
    /// </example>
    public static WedaApplicationContext Default => _default.Value;

    private readonly WedaContextOptions _options;
    private readonly IWedaCloudService _cloudService;
    private readonly IRecordingService? _recordingService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly NatsClient? _natsClient;
    private readonly IConfiguration? _configuration;
    private readonly DeviceConfigurations _deviceConfigs;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IConfigurationCache _configurationCache;
    private readonly IDeviceRegistrationStorage _registrationStorage;
    private readonly SubNodeInfo _subNodeInfo;
    private readonly ISubNodeManager _subNodeManager;
    private readonly SystemCfg _systemCfg;
    private readonly DeviceCfg _deviceCfg;
    private readonly CustomCfg _customCfg;
    private readonly Timer? _cleanupTimer;
    private readonly RecordingOptions? _recordingOptions;
    private volatile bool _disposed;

    public RecordingOptions? RecordingOptions => _recordingOptions;
    
    /// <summary>
    /// Initializes a new instance of WedaApplicationContext with default options.
    /// </summary>
    public WedaApplicationContext()
        : this(args: null, configure: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of WedaApplicationContext with command-line arguments.
    /// Supports --no-cache argument to disable configuration cache loading.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public WedaApplicationContext(string[]? args)
        : this(args: args, configure: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of WedaApplicationContext with custom configuration.
    /// </summary>
    /// <param name="configure">Configuration action for setting up options.</param>
    public WedaApplicationContext(Action<WedaContextOptions> configure)
        : this(args: null, configure: configure)
    {
    }

    /// <summary>
    /// Initializes a new instance of WedaApplicationContext with command-line arguments and custom configuration.
    /// Configuration priority (lowest to highest):
    /// 1. appsettings.json
    /// 2. systemcfg.json, devicecfg.json, customcfg.json
    /// 3. Environment variables
    /// 4. Command-line arguments
    /// </summary>
    /// <param name="args">Command-line arguments for configuration override.</param>
    /// <param name="configure">Optional configuration action for setting up options.</param>
    public WedaApplicationContext(string[]? args, Action<WedaContextOptions>? configure)
    {
        _options = new WedaContextOptions();
        configure?.Invoke(_options);

        // Check for --no-cache argument
        if (args != null && HasNoCacheArgument(args))
        {
            _options.UseCache = false;
        }

        // Auto-load configuration from appsettings.json if not provided
        if (_options.Configuration == null)
        {
            try
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    // Legacy config (backward compatibility)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    // Cloud-synced config files loaded into separate sections
                    .AddJsonFileToSection("systemcfg.json", "SystemConfig", optional: true, reloadOnChange: true)
                    .AddJsonFileToSection("devicecfg.json", "DeviceConfig", optional: true, reloadOnChange: true)
                    .AddJsonFileToSection("customcfg.json", "CustomConfig", optional: true, reloadOnChange: true)
                    // Environment variables and command-line arguments (highest priority)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args ?? [])
                    .Build();
            }
            catch
            {
                // If config files don't exist or fail to load, continue with null configuration
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

        // Log if --no-cache argument was passed
        if (!_options.UseCache)
        {
            var startupLogger = _loggerFactory.CreateLogger<WedaApplicationContext>();
            startupLogger.LogInformation("--no-cache argument detected: Configuration cache will be ignored, using devicecfg.json only");
        }

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
        // Use provided instance from options if available (DI scenario), otherwise create new instance
        _configurationCache = _options.ConfigurationCache ?? new JsonConfigurationCache(
            logger: _loggerFactory.CreateLogger<JsonConfigurationCache>());

        // Initialize registration storage (for SubNode registration persistence)
        // Use provided instance from options if available (DI scenario), otherwise create new instance
        _registrationStorage = _options.RegistrationStorage ?? new JsonDeviceRegistrationStorage(
            logger: _loggerFactory.CreateLogger<JsonDeviceRegistrationStorage>());

        // Use provided recording service from options (DI scenario), or create new instance if EnableRecording is true
        if (_options.RecordingService != null)
        {
            _recordingService = _options.RecordingService;
            _recordingOptions = _options.RecordingOptions
                ?? BindConfiguration<RecordingOptions>(RecordingOptions.SectionName);
            _recordingOptions.Validate();

            // Start daily cleanup timer
            _cleanupTimer = new Timer(
                callback: _ => ExecuteCleanup(),
                state: null,
                dueTime: TimeSpan.Zero,
                period: TimeSpan.FromDays(1));
        }
        else if (_options.DeviceOptions.EnableRecording)
        {
            _recordingOptions = _options.RecordingOptions
                ?? BindConfiguration<RecordingOptions>(RecordingOptions.SectionName);
            _recordingOptions.Validate();

            var recordStorage = new BinaryRecordStorage(_loggerFactory.CreateLogger<BinaryRecordStorage>(), Options.Create(_recordingOptions));
            _recordingService = new RecordingService(_loggerFactory.CreateLogger<RecordingService>(), recordStorage, _deviceRegistry, Options.Create(_recordingOptions));

            // Start daily cleanup timer
            _cleanupTimer = new Timer(
                callback: _ => ExecuteCleanup(),
                state: null,
                dueTime: TimeSpan.Zero,
                period: TimeSpan.FromDays(1));
        }

        // Bind configuration objects using Options Pattern
        _systemCfg = BindConfiguration<SystemCfg>(SystemCfg.SectionName);
        _deviceCfg = BindConfiguration<DeviceCfg>(DeviceCfg.SectionName);
        _customCfg = BindConfiguration<CustomCfg>(CustomCfg.SectionName);

        // Auto-load NATS settings from SystemCfg (includes command-line args override)
        // SystemCfg.WedaNode is bound from configuration which includes --SystemConfig:WedaNode:Url=xxx
        // Only override if user didn't explicitly set NatsConnectionSettings via configure callback
        // Use value comparison (record equality) to check if it's still the default value
        if (_systemCfg.WedaNode != null && _options.NatsConnectionSettings == new NatsConnectionSettings())
        {
            _options.NatsConnectionSettings = _systemCfg.WedaNode;
        }

        // Load SubNode configuration from DeviceCfg
        _subNodeInfo = LoadSubNodeInfo();

        // Load all device configurations from DeviceCfg
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

        // Create Command Registry and auto-scan handlers
        // Pipeline behaviors are now configured via attributes on handler classes:
        // - [Validation(typeof(...))] adds ValidatorBehavior
        // - [Logging] adds LoggingBehavior
        var commandRegistry = new CommandRegistry(_loggerFactory.CreateLogger<CommandRegistry>());

        // 1. Scan SDK assembly (Weda.SubNode.Core) for built-in handlers
        commandRegistry.ScanAssembly(typeof(CommandRegistry).Assembly);

        // 2. Scan User's Entry assembly for custom handlers
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null && entryAssembly != typeof(CommandRegistry).Assembly)
        {
            commandRegistry.ScanAssembly(entryAssembly);
        }

        // Create Command Dispatcher
        var commandDispatcher = new CommandDispatcher(commandRegistry, this);

        // Create SubNodeManager (handles cloud connection, registration, and event subscription)
        _subNodeManager = new SubNodeManager(
            _cloudService,
            _subNodeInfo,
            _options.ConnectionOptions,
            _deviceRegistry,
            _loggerFactory.CreateLogger<SubNodeManager>(),
            commandDispatcher,
            _recordingService);
    }

    /// <summary>
    /// Initializes a new instance of WedaApplicationContext using IConfiguration.
    /// Reads configuration from "WedaNode" section and loads device configuration.
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

            // Bind NATS configuration from "WedaNode" section
            var configSection = configuration.GetSection(NatsConnectionSettings.SectionName);

            if (configSection.Exists())
            {
                options.NatsConnectionSettings = new NatsConnectionSettings
                {
                    Url = configSection["Url"] ?? "nats://localhost:4222",
                    CredFile = configSection["CredFile"] ?? string.Empty,
                    Password = configSection["Password"] ?? string.Empty,
                    Name = configSection["Name"] ?? "default",
                    NatsSerializerRegistry = configSection["SerializerType"]?.ToLower() switch
                    {
                        "json" => WedaNatsSerializerRegistry.Default,
                        _ => WedaNatsSerializerRegistry.Default
                    },
                    AuthStrategy = Enum.TryParse<NatsAuthStrategy>(configSection["AuthStrategy"], true, out var strategy)
                        ? strategy
                        : NatsAuthStrategy.None,
                    Username = configSection["Username"],
                    Token = configSection["Token"],
                    TlsCertPath = configSection["TlsCertPath"],
                    TlsKeyPath = configSection["TlsKeyPath"],
                    TlsCaPath = configSection["TlsCaPath"]
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
    public IRecordingService? RecordingService => _recordingService;

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

    /// <inheritdoc />
    public ISubNodeManager SubNodeManager => _subNodeManager;

    /// <summary>
    /// Gets the system configuration (from systemcfg.json).
    /// Contains NATS connection settings and other system-wide configuration.
    /// </summary>
    public SystemCfg SystemConfig => _systemCfg;

    /// <summary>
    /// Gets the device configuration (from devicecfg.json).
    /// Contains SubNode identity and all device configurations.
    /// </summary>
    public DeviceCfg DeviceConfig => _deviceCfg;

    /// <summary>
    /// Gets the custom configuration (from customcfg.json).
    /// Contains application-specific settings as a flexible dictionary.
    /// </summary>
    public CustomCfg CustomConfig => _customCfg;

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

    /// <inheritdoc />
    public IReadOnlyCollection<TDevice> GetAllDevices<TDevice>() where TDevice : IDevice
        => _deviceRegistry.GetAllDevices<TDevice>();

    #region Private Methods

    /// <summary>
    /// Binds a configuration section to a strongly-typed object using Options Pattern.
    /// Returns a new instance with default values if configuration is null or section doesn't exist.
    /// </summary>
    /// <typeparam name="T">The configuration type to bind to</typeparam>
    /// <param name="sectionName">The configuration section name</param>
    /// <returns>The bound configuration object</returns>
    private T BindConfiguration<T>(string sectionName) where T : new()
    {
        if (_configuration == null)
            return new T();

        var section = _configuration.GetSection(sectionName);
        if (!section.Exists())
            return new T();

        return section.Get<T>() ?? new T();
    }

    /// <summary>
    /// Loads SubNode configuration with the following priority:
    /// 1. Programmatic configuration (options.SubNode) - highest priority
    /// 2. DeviceCfg.SubNode (from devicecfg.json)
    /// 3. Default values (assembly name as SubNode name)
    /// 4. Registration cache (.weda/subnode.registration.json) - for DeviceId only
    /// </summary>
    private SubNodeInfo LoadSubNodeInfo()
    {
        var assemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "SubNode";
        SubNodeInfo subNodeInfo;

        // Priority 1: Check programmatic configuration first (highest priority)
        if (_options.SubNode != null)
        {
            subNodeInfo = _options.SubNode.ToSubNodeInfo(assemblyName);
        }
        // Priority 2: Use DeviceCfg.SubNode (already bound from devicecfg.json)
        else if (!string.IsNullOrEmpty(_deviceCfg.SubNode.Name))
        {
            subNodeInfo = _deviceCfg.SubNode.ToSubNodeInfo(assemblyName);
        }
        // Priority 3: Default values
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

        // Try to load DeviceId from registration cache
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

    private const string DeviceConfigurationSectionName = "DeviceConfig:DeviceConfigs";
    private const string DeviceCfgFileName = "devicecfg.json";

    /// <summary>
    /// Loads all device configurations from the "DeviceConfig:DeviceConfigs" section.
    /// Located under DeviceConfig section (loaded from devicecfg.json into DeviceConfig section).
    /// Each configuration is enriched with DeviceName (from key) and SubNodeInfo.
    /// Also stores the raw JSON from devicecfg.json for Report content.
    /// </summary>
    private DeviceConfigurations LoadAllDeviceConfigurations()
    {
        var logger = _loggerFactory.CreateLogger<WedaApplicationContext>();
        var configs = new DeviceConfigurations();

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

        // Load raw devicecfg.json for RawDeviceCfgJson (used in Report)
        var rawDeviceCfgJson = LoadRawDeviceCfgJson(logger);

        foreach (var configSection in deviceConfigsSection.GetChildren())
        {
            var configKey = configSection.Key;

            try
            {
                // Check for duplicate keys (case-insensitive)
                if (configs.ContainsKey(configKey))
                {
                    var existingKey = configs.Keys.First(k => k.Equals(configKey, StringComparison.OrdinalIgnoreCase));
                    throw new InvalidOperationException(
                        $"Duplicate device configuration key detected (case-insensitive): '{configKey}' conflicts with existing key '{existingKey}'. " +
                        $"Device configuration keys must be unique regardless of case.");
                }

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

                // Store raw devicecfg.json (for Report content)
                deviceConfig.RawDeviceCfgJson = rawDeviceCfgJson;

                // Try applying cached cloud configuration (PATCH semantics)
                // Note: If cache is applied, RawDeviceCfgJson will be updated
                ApplyCachedConfigurationIfExists(deviceConfig, logger);

                // Validate sensor names for IoTDB compatibility (fail-fast on startup)
                ValidateSensorNames(deviceConfig, logger);

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
            catch (InvalidOperationException)
            {
                // Re-throw validation errors (sensor name, duplicate keys, etc.) to fail-fast on startup
                throw;
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
    /// Loads the raw JSON content from devicecfg.json for Report content.
    /// This preserves the original file content for accurate reporting.
    /// </summary>
    private System.Text.Json.JsonElement? LoadRawDeviceCfgJson(Microsoft.Extensions.Logging.ILogger logger)
    {
        try
        {
            var deviceCfgPath = Path.Combine(Directory.GetCurrentDirectory(), DeviceCfgFileName);
            if (!File.Exists(deviceCfgPath))
            {
                logger.LogDebug("devicecfg.json not found at {Path}, RawDeviceCfgJson will be null", deviceCfgPath);
                return null;
            }

            var jsonContent = File.ReadAllText(deviceCfgPath);
            var jsonDocument = System.Text.Json.JsonDocument.Parse(jsonContent);

            logger.LogDebug("Loaded raw devicecfg.json from {Path}", deviceCfgPath);
            return jsonDocument.RootElement.Clone();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load raw devicecfg.json");
            return null;
        }
    }

    /// <summary>
    /// Applies cached cloud configuration to a device config if cache exists.
    /// Device configurations are stored in the device-config cache.
    /// Also updates RawDeviceCfgJson from the cache's desired state.
    /// </summary>
    private void ApplyCachedConfigurationIfExists(DeviceConfiguration deviceConfig, Microsoft.Extensions.Logging.ILogger logger)
    {
        // Skip cache if --no-cache argument was passed
        if (!_options.UseCache)
        {
            logger.LogDebug("Cache disabled (--no-cache), skipping cached configuration for '{DeviceName}'", deviceConfig.DeviceName);
            return;
        }

        try
        {
            if (!_configurationCache.ExistsAsync(SubscriptionTypes.DeviceConfig).GetAwaiter().GetResult())
                return;

            var cachedMessage = _configurationCache.GetRawConfigurationAsync(SubscriptionTypes.DeviceConfig).GetAwaiter().GetResult();
            if (cachedMessage == null)
                return;

            // Log before applying
            var desiredSensorCount = cachedMessage.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs
                ?.GetValueOrDefault(deviceConfig.DeviceName)?.Sensors?.Count ?? 0;
            var currentSensorCount = deviceConfig.Sensors.Count;
            logger.LogDebug(
                "Applying cached configuration to '{DeviceName}': current sensors={CurrentCount}, desired sensors={DesiredCount}",
                deviceConfig.DeviceName, currentSensorCount, desiredSensorCount);

            var applied = ConfigurationUpdateHelper.ApplyCachedConfiguration(deviceConfig, cachedMessage);
            if (applied)
            {
                // Update RawDeviceCfgJson from cache's desired state (use raw JSON to preserve structure)
                // This ensures Report reflects the last applied cloud configuration with all fields
                var rawDeviceCfg = cachedMessage.Data?.Cfg?.Desired?.RawDeviceCfg;
                if (rawDeviceCfg.HasValue)
                {
                    deviceConfig.RawDeviceCfgJson = rawDeviceCfg.Value.Clone();
                }

                logger.LogInformation(
                    "Applied cached cloud configuration to '{DeviceName}' from {CachePath}, final sensor count={SensorCount}",
                    deviceConfig.DeviceName,
                    _configurationCache.GetCacheFilePath(SubscriptionTypes.DeviceConfig),
                    deviceConfig.Sensors.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to apply cached configuration to '{DeviceName}'",
                deviceConfig.DeviceName);
        }
    }

    /// <summary>
    /// Validates all sensor names in a device configuration for IoTDB compatibility.
    /// Throws InvalidOperationException if any sensor name is invalid, causing application to fail-fast on startup.
    /// </summary>
    private static void ValidateSensorNames(DeviceConfiguration deviceConfig, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (deviceConfig.Sensors.Count == 0)
            return;

        logger.LogDebug("Validating {Count} sensor names for IoTDB compatibility in device '{DeviceName}'",
            deviceConfig.Sensors.Count, deviceConfig.DeviceName);

        var result = SensorNameValidator.ValidateAll(deviceConfig.Sensors, deviceConfig.DeviceName);
        if (result.IsError)
        {
            var errorMessages = string.Join(Environment.NewLine, result.Errors.Select(e => $"  - {e.Description}"));
            throw new InvalidOperationException(
                $"Invalid sensor name(s) detected in device '{deviceConfig.DeviceName}':{Environment.NewLine}{errorMessages}");
        }
    }

    private void LoadDtdlIfEnabled(DeviceConfiguration deviceConfig, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (!_options.AutoLoadDtdl)
            return;

        try
        {
            // Use the new unified DTDL initialization
            // This handles both auto-generation (AutoGenEnabled=true) and file loading (AutoGenEnabled=false)
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
            logger.LogWarning(ex, "Failed to load DTDL from: {DtdlPath}", deviceConfig.Dtdl.DtdlPath);
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

    #region Recording Cleanup

    private void ExecuteCleanup()
    {
        if (_recordingService == null || _recordingOptions == null)
            return;

        // RetentionDays = 0 means retention-based cleanup is disabled
        if (_recordingOptions.RetentionDays <= 0)
            return;

        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-_recordingOptions.RetentionDays);
            _recordingService.CleanupAsync(cutoff, CancellationToken.None).Wait();
        }
        catch (Exception ex)
        {
            _loggerFactory.CreateLogger<WedaApplicationContext>()
                .LogError(ex, "Recording cleanup failed");
        }
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
            // Dispose cleanup timer
            _cleanupTimer?.Dispose();

            // Flush recording buffers before shutdown
            if (_recordingService != null)
            {
                try
                {
                    _recordingService.FlushAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore flush errors during disposal
                }
            }

            // Dispose SubNodeManager first (handles cloud disconnect)
            if (_subNodeManager is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().AsTask().Wait();
            }

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

    #region Helper Methods

    /// <summary>
    /// Checks if the --no-cache argument is present in the command-line arguments.
    /// </summary>
    private static bool HasNoCacheArgument(string[] args)
    {
        return args.Any(arg =>
            arg.Equals("--no-cache", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-no-cache", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}

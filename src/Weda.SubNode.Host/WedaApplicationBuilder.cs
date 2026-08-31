using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Net;
using Serilog;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Cloud;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Cloud.Serialization;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Storage;

namespace Weda.SubNode.Host;

/// <summary>
/// Builder for configuring and creating a WedaApplication.
/// Similar to WebApplicationBuilder in ASP.NET Core.
///
/// Configuration loading priority (default):
/// 1. .device-config-cache.json (if exists) - preserves cloud-driven configuration updates
/// 2. appsettings.json (fallback) - initial configuration
///
/// Use --no-cache argument to force loading from appsettings.json only.
/// </summary>
public class WedaApplicationBuilder
{
    private readonly HostApplicationBuilder _hostBuilder;
    private readonly List<Func<IWedaApplicationContext, IDevice>> _deviceFactories = [];
    private readonly Dictionary<string, Type> _deviceClassesBySection
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _useCache;
    private readonly IConfigurationCache _configurationCache;
    private string? _projectName;
    private string? _projectDescription;

    internal WedaApplicationBuilder(HostApplicationBuilder hostBuilder, bool useCache = true)
    {
        _hostBuilder = hostBuilder;
        _useCache = useCache;
        _configurationCache = new JsonConfigurationCache();
    }

    /// <summary>
    /// Gets the service collection for dependency injection
    /// </summary>
    public IServiceCollection Services => _hostBuilder.Services;

    /// <summary>
    /// Gets the configuration for the application
    /// </summary>
    public IConfigurationManager Configuration => _hostBuilder.Configuration;

    /// <summary>
    /// Gets the logging builder for configuring logging
    /// </summary>
    public ILoggingBuilder Logging => _hostBuilder.Logging;

    /// <summary>
    /// Gets the environment information
    /// </summary>
    public IHostEnvironment Environment => _hostBuilder.Environment;

    /// <summary>
    /// Set the SubNode project's display name. Surfaces in the v1.2 upload payload
    /// as the wrapper Interface's <c>displayName</c>; cloud / front-end use it as
    /// the human-readable label for this SubNode.
    /// </summary>
    /// <param name="name">Project name (e.g., <c>"MyFirstApplication"</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public WedaApplicationBuilder WithName(string name)
    {
        _projectName = name;
        return this;
    }

    /// <summary>
    /// Set the SubNode project's description. Surfaces in the v1.2 upload payload
    /// as the wrapper Interface's <c>description</c>.
    /// </summary>
    /// <param name="description">Free-form description.</param>
    /// <returns>The builder for chaining.</returns>
    public WedaApplicationBuilder WithDescription(string description)
    {
        _projectDescription = description;
        return this;
    }

    /// <summary>
    /// Add a custom device with strongly-typed configuration (e.g., TcpModbusDeviceConfiguration).
    /// The device will be automatically managed with proper lifecycle:
    /// - InitializeAsync called on startup
    /// - StartAsync called after successful initialization
    /// - StopAsync called on shutdown
    ///
    /// IMPORTANT: TDevice must have a public constructor with the following signature:
    ///   public TDevice(IWedaApplicationContext context, DeviceConfiguration config)
    ///
    /// This convention ensures all devices can be created uniformly without custom factories.
    /// </summary>
    /// <typeparam name="TDevice">The custom device type (e.g., MyFirstDevice : TcpModbusDevice)</typeparam>
    /// <param name="deviceConfiguration">Strongly-typed device configuration</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// // Define device with required constructor
    /// public class MyFirstDevice : TcpModbusDevice
    /// {
    ///     public MyFirstDevice(IWedaApplicationContext context, DeviceConfiguration config)
    ///         : base(context, config) { }
    /// }
    ///
    /// // Register device
    /// var config = new TcpModbusDeviceConfiguration { DeviceName = "Device1", Host = "127.0.0.1" };
    /// builder.AddDevice&lt;MyFirstDevice&gt;(config);
    /// </example>
    public WedaApplicationBuilder AddDevice<TDevice>(IDeviceConfiguration deviceConfiguration)
        where TDevice : IDevice
    {
        var config = deviceConfiguration.ToDeviceConfiguration();
        return AddDevice(context => (TDevice)Activator.CreateInstance(typeof(TDevice), context, config)!);
    }

    /// <summary>
    /// Add a device by type, automatically loading configuration from devicecfg.json.
    /// The device configuration will be read from DeviceConfig:DeviceConfigs section using the type name as key.
    ///
    /// Example for AddDevice&lt;MyCustomDevice&gt;():
    /// devicecfg.json:
    /// {
    ///   "SubNode": { ... },
    ///   "DeviceConfigs": {
    ///     "MyCustomDevice": {  // Uses typeof(TDevice).Name as key
    ///       "Enabled": true,
    ///       "DeviceName": "My Device",
    ///       "Communication": { "Host": "192.168.1.100", "Port": 502 },
    ///       ...
    ///     }
    ///   }
    /// }
    /// </summary>
    /// <typeparam name="TDevice">The custom device type (e.g., MyFirstDevice : TcpModbusDevice)</typeparam>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddDevice<TDevice>()
        where TDevice : IDevice
    {
        return AddDevice<TDevice>(typeof(TDevice).Name);
    }

    /// <summary>
    /// Add a device by type with explicit configuration section name.
    ///
    /// Configuration loading priority:
    /// 1. .weda/device-config.cache.json (if exists and --no-cache not specified) - merges with devicecfg.json
    /// 2. devicecfg.json DeviceConfigs section (fallback)
    ///
    /// Example for AddDevice&lt;MyCustomDevice&gt;("MyFirstDevice"):
    /// devicecfg.json:
    /// {
    ///   "SubNode": { ... },
    ///   "DeviceConfigs": {
    ///     "MyFirstDevice": {  // Uses the specified sectionName as key
    ///       "Enabled": true,
    ///       "DeviceName": "Another Device",
    ///       "Communication": { "Host": "127.0.0.1", "Port": 5020 },
    ///       ...
    ///     }
    ///   }
    /// }
    /// </summary>
    /// <typeparam name="TDevice">The custom device type (e.g., MyFirstDevice : TcpModbusDevice)</typeparam>
    /// <param name="sectionName">The configuration section name in DeviceConfigs</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddDevice<TDevice>(string sectionName)
        where TDevice : IDevice
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            throw new ArgumentException("Section name cannot be null or whitespace", nameof(sectionName));
        }

        // Validate that the configuration section exists
        var deviceConfigSection = Configuration.GetSection($"DeviceConfig:DeviceConfigs:{sectionName}");
        if (!deviceConfigSection.Exists())
        {
            throw new InvalidOperationException(
                $"Device configuration section 'DeviceConfig:DeviceConfigs:{sectionName}' not found in devicecfg.json. " +
                $"Please ensure the configuration exists in devicecfg.json under DeviceConfigs.");
        }

        Log.Information("Using device '{SectionName}' configuration from devicecfg.json", sectionName);

        // Record sectionName -> TDevice mapping so WedaApplicationContext's loader can
        // probe IConfigurableDevice<,> on TDevice and stash DeviceConfiguration.DeviceTypeName,
        // enabling typed sensor-dtmi dispatch in DeviceConfiguration.InitializeDtdl.
        _deviceClassesBySection[sectionName] = typeof(TDevice);

        // Create factory that retrieves the configuration from WedaApplicationContext
        // This ensures we use the same configuration instance that was already initialized in WedaApplicationContext
        return AddDevice<TDevice>(context =>
        {
            // Get the configuration from WedaApplicationContext (already initialized with DTDL, cache, etc.)
            var config = context.DeviceConfigs.TryGetValue(sectionName, out var deviceConfig)
                ? deviceConfig
                : throw new InvalidOperationException(
                    $"Device configuration '{sectionName}' not found in WedaApplicationContext. " +
                    $"Available configurations: {string.Join(", ", context.DeviceConfigs.Keys)}");

            return (TDevice)Activator.CreateInstance(typeof(TDevice), context, config)!;
        });
    }

    /// <summary>
    /// Add a custom device with a factory function that creates the device instance.
    /// Use this overload when your device needs custom construction logic beyond the standard convention.
    ///
    /// The device will be automatically managed with proper lifecycle:
    /// - InitializeAsync called on startup
    /// - StartAsync called after successful initialization
    /// - StopAsync called on shutdown
    /// Multiple devices added via AddDevice will be managed by a single DeviceHostedService.
    /// </summary>
    /// <typeparam name="TDevice">The custom device type that inherits from IDevice</typeparam>
    /// <param name="deviceFactory">Factory function that creates the device from IWedaApplicationContext</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// // Use factory when device needs special construction
    /// var config = new TcpModbusDeviceConfiguration { ... };
    /// var specialService = new MySpecialService();
    /// builder.AddDevice(context => new MySpecialDevice(
    ///     context,
    ///     config.ToDeviceConfiguration(),
    ///     specialService));  // Extra dependency
    /// </example>
    public WedaApplicationBuilder AddDevice<TDevice>(Func<IWedaApplicationContext, TDevice> deviceFactory)
        where TDevice : IDevice
    {
        // Store the factory, will create devices in Build()
        _deviceFactories.Add(context => deviceFactory(context));
        return this;
    }

    /// <summary>
    /// Configure NATS connection settings
    /// </summary>
    /// <param name="configureAction">Action to configure NATS settings</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder ConfigureNats(Action<NatsConnectionSettings> configureAction)
    {
        Services.Configure(configureAction);
        return this;
    }

    /// <summary>
    /// Enable telemetry sending to cloud (uplink)
    /// Devices will send telemetry data to the cloud platform
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddTelemetry()
    {
        Services.Configure<DeviceOptions>(options =>
        {
            options.EnableTelemetry = true;
        });
        return this;
    }

    /// <summary>
    /// Enable health reporting to cloud (uplink)
    /// Devices will send health status reports to the cloud platform
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddHealthReporting()
    {
        Services.Configure<DeviceOptions>(options =>
        {
            options.EnableHealthReporting = true;
        });
        return this;
    }

    /// <summary>
    /// Enable command receiving from cloud (downlink)
    /// Devices will be able to receive and execute commands sent from the cloud platform
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddCommands()
    {
        Services.Configure<DeviceOptions>(options =>
        {
            options.EnableCommands = true;
        });
        return this;
    }

    /// <summary>
    /// Enable configuration update receiving from cloud (downlink)
    /// Devices will be able to receive and apply configuration updates sent from the cloud platform
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddConfigUpdates()
    {
        Services.Configure<DeviceOptions>(options =>
        {
            options.EnableConfigUpdates = true;
        });
        return this;
    }

    public WedaApplicationBuilder AddRecording(Action<RecordingOptions>? configure = null)
    {
        if (configure == null)
        {
            Services.Configure<RecordingOptions>(Configuration.GetSection($"SystemConfig:{RecordingOptions.SectionName}"));
        }
        else
        {
            Services.Configure(configure);
        }

        // Enable recording flag for WedaApplicationContext to create RecordingService
        Services.Configure<DeviceOptions>(options => options.EnableRecording = true);

        return this;
    }

    /// <summary>
    /// Configure connection retry policy for device and cloud connections.
    /// Controls how the SDK handles connection failures and reconnection attempts.
    /// </summary>
    /// <param name="configureAction">Action to configure connection options</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// builder.ConfigureConnectionPolicy(options =>
    /// {
    ///     options.MaxRetryAttempts = 5;           // Limit to 5 retries (-1 for unlimited)
    ///     options.RetryDelayMs = 2000;            // Start with 2 second delay
    ///     options.MaxRetryDelayMs = 30000;        // Cap delay at 30 seconds
    ///     options.ConnectionTimeoutMs = 60000;   // 60 second timeout per attempt
    /// });
    /// </code>
    /// </example>
    public WedaApplicationBuilder ConfigureConnectionPolicy(Action<ConnectionOptions> configureAction)
    {
        Services.Configure(configureAction);
        return this;
    }

    /// <summary>
    /// Add cloud service to the application
    /// </summary>
    /// <param name="cloudServiceFactory">Factory function to create cloud service</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddCloudService(Func<IServiceProvider, IWedaCloudService> cloudServiceFactory)
    {
        Services.AddSingleton(cloudServiceFactory);
        return this;
    }

    /// <summary>
    /// Use mock cloud service (for testing and offline scenarios).
    /// Does not connect to any cloud, generates mock deviceId locally.
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder UseMockCloud()
    {
        // Remove existing IWedaCloudService registration first
        var existing = Services.FirstOrDefault(s => s.ServiceType == typeof(IWedaCloudService));
        if (existing != null)
        {
            Services.Remove(existing);
        }

        Services.AddSingleton(_ => WedaFactory.Cloud.Mock);
        return this;
    }

    /// <summary>
    /// Use default cloud service (NATS-based EdgeSync cloud service).
    /// This is the default in CreateDefaultBuilder().
    ///
    /// Supports multiple authentication strategies configured via appsettings.json:
    /// - None: Anonymous connection (default)
    /// - UserPassword: Username and password authentication
    /// - Token: Token-based authentication
    /// - TlsCert: TLS client certificate authentication
    /// - CredFile: Credential file authentication (JWT + NKey)
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder UseDefaultCloud()
    {
        // Remove existing IWedaCloudService registration first
        var existing = Services.FirstOrDefault(s => s.ServiceType == typeof(IWedaCloudService));
        if (existing != null)
        {
            Services.Remove(existing);
        }

        // Configure NatsConnectionSettings from "SystemConfig:WedaNode" section
        // systemcfg.json is loaded into "SystemConfig" section via AddJsonFileToSection
        Services.Configure<NatsConnectionSettings>(_hostBuilder.Configuration.GetSection($"SystemConfig:{NatsConnectionSettings.SectionName}"));

        // Register NatsClient as singleton with configurable authentication strategy
        Services.AddSingleton(sp =>
        {
            var settings = sp.GetService<IOptions<NatsConnectionSettings>>()?.Value
                ?? NatsConnectionSettings.Default;
            var logger = sp.GetService<ILogger<NatsClient>>();

            var natsOpts = NatsOpts.Default with
            {
                Url = settings.Url,
                Name = settings.Name,
                SerializerRegistry = WedaNatsSerializerRegistry.Default,
                AuthOpts = settings.BuildAuthOpts(),
                TlsOpts = BuildTlsOpts(settings)
            };

            logger?.LogInformation(
                "Creating NATS connection to {Url} with auth strategy: {AuthStrategy}",
                settings.Url, settings.AuthStrategy);

            return new NatsClient(natsOpts);
        });

        // Register CommandRegistry as a singleton so DeviceAgentClient can pick it
        // up for capability upload. Scans the SDK assembly plus the entry assembly
        // for user-defined handlers — matching what WedaApplicationContext does on
        // the non-DI path.
        Services.AddSingleton<CommandRegistry>(sp =>
        {
            var logger = sp.GetService<ILogger<CommandRegistry>>();
            var registry = new CommandRegistry(logger);
            registry.ScanAssembly(typeof(CommandRegistry).Assembly);

            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null && entryAssembly != typeof(CommandRegistry).Assembly)
            {
                registry.ScanAssembly(entryAssembly);
            }

            // Capability-gate exposure: commands marked with
            // [RequiresDeviceCapability] are only uploaded to cloud when one of
            // this SubNode's AddDevice<TDevice>() classes implements the
            // capability. Resolved lazily, so all AddDevice calls have run.
            registry.SetDeviceClasses(_deviceClassesBySection.Values);
            return registry;
        });

        // Register NATS-based clients
        Services.AddSingleton<IDeviceAgentClient>(sp =>

        {
            var natsClient = sp.GetRequiredService<NatsClient>();
            var logger = sp.GetService<ILogger<DeviceAgentClient>>();
            var commandRegistry = sp.GetService<CommandRegistry>();
            var projectInfo = sp.GetService<ProjectInfo>();
            return new DeviceAgentClient(natsClient, logger, commandRegistry, projectInfo);
        });

        Services.AddSingleton<ITelemetryClient>(sp =>
        {
            var natsClient = sp.GetRequiredService<NatsClient>();
            var logger = sp.GetService<ILogger<TelemetryClient>>();
            return new TelemetryClient(natsClient, logger);
        });

        // Register storage services as singletons to ensure consistent paths and avoid duplicate initialization
        Services.AddSingleton<IDeviceRegistrationStorage>(sp =>
        {
            var logger = sp.GetService<ILogger<JsonDeviceRegistrationStorage>>();
            return new JsonDeviceRegistrationStorage(logger: logger);
        });

        Services.AddSingleton<IConfigurationCache>(sp =>
        {
            var logger = sp.GetService<ILogger<JsonConfigurationCache>>();
            return new JsonConfigurationCache(logger: logger);
        });

        // Register WedaCloudService
        Services.AddSingleton<IWedaCloudService>(sp =>
        {
            var natsClient = sp.GetRequiredService<NatsClient>();
            var deviceAgentClient = sp.GetRequiredService<IDeviceAgentClient>();
            var telemetryClient = sp.GetRequiredService<ITelemetryClient>();
            var registrationStorage = sp.GetRequiredService<IDeviceRegistrationStorage>();
            var logger = sp.GetService<ILogger<WedaCloudService>>();
            return new WedaCloudService(natsClient, deviceAgentClient, telemetryClient, registrationStorage, logger);
        });

        return this;
    }

    /// <summary>
    /// Build NatsTlsOpts from NatsConnectionSettings for TLS certificate authentication.
    /// </summary>
    private static NatsTlsOpts BuildTlsOpts(NatsConnectionSettings settings)
    {
        // Only configure TLS opts for TlsCert strategy or when TLS paths are provided
        if (settings.AuthStrategy != NatsAuthStrategy.TlsCert &&
            string.IsNullOrEmpty(settings.TlsCertPath) &&
            string.IsNullOrEmpty(settings.TlsCaPath))
        {
            return NatsTlsOpts.Default;
        }

        // Build TLS options for client certificate authentication
        return new NatsTlsOpts
        {
            CertFile = settings.TlsCertPath,
            KeyFile = settings.TlsKeyPath,
            CaFile = settings.TlsCaPath
        };
    }

    /// <summary>
    /// Configure logging with a fluent API (programmatic configuration)
    /// Use this ONLY if you want to configure logging programmatically instead of using appsettings.json.
    ///
    /// Example: builder.AddLogging(log => log.WriteToConsole().WriteToFile("logs/app.log"))
    /// </summary>
    /// <param name="configure">Action to configure logging</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddLogging(Action<LoggingConfigurator> configure)
    {
        var configurator = new LoggingConfigurator();
        configure(configurator);

        Log.Logger = configurator.Build();
        Logging.ClearProviders();
        Logging.AddSerilog(Log.Logger);

        // Configure WedaFactory to use the logger factory from DI
        var serviceProvider = Services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        Core.WedaFactory.UseLoggerFactory(loggerFactory);

        return this;
    }

    /// <summary>
    /// Configure logging from appsettings.json (recommended)
    /// Reads Serilog configuration from the "Serilog" section in appsettings.json
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddLogging()
    {
        // Configure Serilog from appsettings.json
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(Configuration)
            .CreateLogger();

        Logging.ClearProviders();
        Logging.AddSerilog(Log.Logger);

        // Configure WedaFactory to use the logger factory from DI
        var serviceProvider = Services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        Core.WedaFactory.UseLoggerFactory(loggerFactory);

        return this;
    }

    /// <summary>
    /// Build the WedaApplication
    /// </summary>
    /// <returns>A configured WedaApplication instance</returns>
    public WedaApplication Build()
    {
        // Register project metadata (set via WithName / WithDescription) so the
        // mapping layer can read it when constructing the v1.2 wrapper Interface.
        Services.AddSingleton(_ => new ProjectInfo(_projectName, _projectDescription));

        // Register WedaApplicationContext (which creates SubNodeManager internally)
        Services.AddSingleton<IWedaApplicationContext>(sp =>
        {
            var cloudService = sp.GetRequiredService<IWedaCloudService>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var configuration = sp.GetService<IConfiguration>();

            // Get DeviceOptions from DI (configured by AddTelemetry, AddCommands, etc.)
            var deviceOptions = sp.GetService<IOptions<DeviceOptions>>()?.Value
                ?? DeviceOptions.Default;

            // Get ConnectionOptions from DI (configured by ConfigureConnectionPolicy)
            var connectionOptions = sp.GetService<IOptions<ConnectionOptions>>()?.Value
                ?? ConnectionOptions.Default;

            // Get storage services from DI (registered by UseDefaultCloud)
            var registrationStorage = sp.GetService<IDeviceRegistrationStorage>();
            var configurationCache = sp.GetService<IConfigurationCache>();

            // Get recording options from DI (registered by AddRecording)
            var recordingOptions = sp.GetService<IOptions<RecordingOptions>>()?.Value;

            return new Context.WedaApplicationContext(options =>
            {
                options.CloudService = cloudService;
                options.LoggerFactory = loggerFactory;
                options.Configuration = configuration;
                options.DeviceOptions = deviceOptions;
                options.ConnectionOptions = connectionOptions;
                // Pass DI-registered storage instances to share resources
                options.RegistrationStorage = registrationStorage;
                options.ConfigurationCache = configurationCache;
                // Pass recording options (WedaApplicationContext creates RecordingService internally)
                options.RecordingOptions = recordingOptions;
                // Forward AddDevice<TDevice>("sectionName") class registrations so the
                // loader can stash DeviceConfiguration.DeviceTypeName for typed devices.
                options.DeviceClassesBySection = _deviceClassesBySection;
            });
        });

        // Register ISubNodeManager from the context (it's created by WedaApplicationContext)
        Services.AddSingleton<ISubNodeManager>(sp =>
            sp.GetRequiredService<IWedaApplicationContext>().SubNodeManager);

        // Register IDeviceRegistry from the context
        Services.AddSingleton<IDeviceRegistry>(sp =>
            sp.GetRequiredService<IWedaApplicationContext>().DeviceRegistry);

        // Register IRecordingService from the context (may be null if recording not enabled)
        Services.AddSingleton<IRecordingService>(sp =>
            sp.GetRequiredService<IWedaApplicationContext>().RecordingService!);

        // Register IDynamicRecordStorage from the context (for MIME type data)
        Services.AddSingleton(sp =>
            sp.GetRequiredService<IWedaApplicationContext>().DynamicRecordStorage!);

        // Register device factory
        Services.AddSingleton<IDeviceFactory, DeviceFactory>();

        // Register hosted service for device management
        // All devices are registered via AddDevice<TDevice>() factory methods
        if (_deviceFactories.Count > 0)
        {
            Services.AddHostedService(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DeviceHostedService>>();
                var context = sp.GetRequiredService<IWedaApplicationContext>();
                var subNodeManager = sp.GetRequiredService<ISubNodeManager>();

                // Create devices via registered factory methods
                var devices = _deviceFactories.Select(factory => factory(context)).ToList();

                return new DeviceHostedService(logger, subNodeManager, devices);
            });

            // SubNode liveness heartbeat. Always registered, but inert unless a device
            // declares the reserved heartbeat sensor in devicecfg.json — that declaration
            // is the opt-in, so an application that wants no heartbeat gets none and an
            // SDK upgrade never starts one on its own.
            Services.AddHostedService<HeartbeatHostedService>();
        }

        var host = _hostBuilder.Build();
        return new WedaApplication(host);
    }
}
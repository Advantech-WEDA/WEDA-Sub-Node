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
using Weda.SubNode.Cloud;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Cloud.Serialization;
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
    private readonly List<DeviceConfiguration> _deviceConfigurations = new();
    private readonly List<Func<IWedaApplicationContext, IDevice>> _deviceFactories = [];
    private IDeviceTypeNameResolver _deviceTypeNameResolver = new DefaultDeviceTypeNameResolver();
    private readonly bool _useCache;
    private readonly IConfigurationCache _configurationCache;

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
    /// Add a device configuration to the application
    /// </summary>
    /// <param name="deviceConfiguration">Device configuration to add</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddDevice(DeviceConfiguration deviceConfiguration)
    {
        _deviceConfigurations.Add(deviceConfiguration);
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
    /// Add a device by type, automatically loading configuration from appsettings.json.
    /// The device configuration will be read from DeviceConfigs section using the type name as key.
    ///
    /// Example for AddDevice&lt;MyCustomDevice&gt;():
    /// {
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
    /// 1. .device-config-cache.json (if exists and --no-cache not specified)
    /// 2. appsettings.json DeviceConfigs section (fallback)
    ///
    /// Example for AddDevice&lt;MyCustomDevice&gt;("MyFirstDevice"):
    /// {
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

        // Try loading from cache first (if enabled)
        DeviceConfiguration? config = null;
        var configSource = "appsettings.json";

        if (_useCache)
        {
            try
            {
                var cachedConfig = _configurationCache.GetConfigurationAsync().GetAwaiter().GetResult();
                if (cachedConfig != null)
                {
                    config = cachedConfig;
                    configSource = ".device-config-cache.json";
                    Log.Information("Loaded device configuration from cache: {CachePath}", _configurationCache.CacheFilePath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load configuration from cache, falling back to appsettings.json");
            }
        }

        // Fallback to appsettings.json
        if (config == null)
        {
            var deviceConfigSection = Configuration.GetSection($"DeviceConfigs:{sectionName}");
            if (!deviceConfigSection.Exists())
            {
                throw new InvalidOperationException(
                    $"Device configuration section 'DeviceConfigs:{sectionName}' not found in appsettings.json. " +
                    $"Please ensure the configuration exists.");
            }

            config = deviceConfigSection.Get<DeviceConfiguration>();
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"Failed to bind configuration from 'DeviceConfigs:{sectionName}'. " +
                    $"Please check your appsettings.json format.");
            }
        }

        Log.Information("Using device configuration from {Source}", configSource);

        // Ensure DeviceTypeName is set for factory resolution
        if (string.IsNullOrEmpty(config.DeviceTypeName))
        {
            config.DeviceTypeName = typeof(TDevice).Name;
        }

        // Create factory that will instantiate the device with the loaded configuration
        return AddDevice<TDevice>(context =>
            (TDevice)Activator.CreateInstance(typeof(TDevice), context, config)!);
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
    /// Scan and add devices from appsettings.json configuration
    /// Supports two formats:
    ///
    /// 1. Dictionary format (recommended) - Key is used as DeviceTypeName if not specified:
    /// {
    ///   "DeviceConfigs": {
    ///     "TcpModbusDevice": { "DeviceName": "My Device", ... },
    ///     "MyFirstDevice": { "DeviceName": "Custom Device", ... }
    ///   }
    /// }
    /// Note: The key ("TcpModbusDevice", "MyFirstDevice") automatically becomes the DeviceTypeName
    ///       unless explicitly overridden with a "DeviceTypeName" property.
    ///
    /// 2. Legacy array format (requires explicit DeviceTypeName):
    /// {
    ///   "Devices": [
    ///     { "Enabled": true, "DeviceTypeName": "TcpModbusDevice", ... }
    ///   ]
    /// }
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder ScanDevicesFromConfiguration()
    {
        // Try DeviceConfigs (dictionary) format first
        var deviceConfigsSection = Configuration.GetSection("DeviceConfigs");
        if (deviceConfigsSection.Exists())
        {
            foreach (var deviceSection in deviceConfigsSection.GetChildren())
            {
                var device = deviceSection.Get<DeviceConfiguration>();
                if (device != null && device.Enabled)
                {
                    // Use the config key as DeviceTypeName if not specified
                    // e.g., "MyFirstDevice" from DeviceConfigs["MyFirstDevice"]
                    if (string.IsNullOrEmpty(device.DeviceTypeName))
                    {
                        device.DeviceTypeName = deviceSection.Key;
                    }
                    _deviceConfigurations.Add(device);
                }
            }
        }
        else
        {
            // Fallback to legacy Devices (array) format
            var devicesSection = Configuration.GetSection("Devices");
            var devices = devicesSection.Get<List<DeviceConfiguration>>();

            if (devices != null && devices.Count > 0)
            {
                foreach (var device in devices.Where(d => d.Enabled))
                {
                    _deviceConfigurations.Add(device);
                }
            }
        }

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
    /// Configure custom device type name resolver
    /// Allows custom logic for resolving device type names to Type instances
    /// </summary>
    /// <param name="resolver">Custom resolver implementation</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder UseDeviceTypeNameResolver(IDeviceTypeNameResolver resolver)
    {
        _deviceTypeNameResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
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

        Services.Configure<NatsConnectionSettings>(_hostBuilder.Configuration.GetSection("Nats"));

        // Register NatsClient as singleton
        Services.AddSingleton(sp =>
        {
            var natsOptions = sp.GetService<IOptions<NatsConnectionSettings>>()?.Value;
            var url = natsOptions?.Url ?? "nats://localhost:4222";
            var credsFile = natsOptions?.CredFile;
            var natsOpts = NatsOpts.Default with
            {
                Url = url,
                SerializerRegistry = WedaNatsSerializerRegistry.Default,
                AuthOpts = NatsAuthOpts.Default with
                {
                    CredsFile = credsFile
                }
            };

            return new NatsClient(natsOpts);
        });

        // Register NATS-based clients
        Services.AddSingleton<IDeviceAgentClient>(sp =>
        {
            var natsClient = sp.GetRequiredService<NatsClient>();
            var logger = sp.GetService<ILogger<DeviceAgentClient>>();
            return new DeviceAgentClient(natsClient, logger);
        });

        Services.AddSingleton<ITelemetryClient>(sp =>
        {
            var natsClient = sp.GetRequiredService<NatsClient>();
            var logger = sp.GetService<ILogger<TelemetryClient>>();
            return new TelemetryClient(natsClient, logger);
        });

        // Register WedaCloudService
        Services.AddSingleton<IWedaCloudService>(sp =>
        {
            var natsClient = sp.GetRequiredService<NatsClient>();
            var deviceAgentClient = sp.GetRequiredService<IDeviceAgentClient>();
            var telemetryClient = sp.GetRequiredService<ITelemetryClient>();
            var logger = sp.GetService<ILogger<WedaCloudService>>();
            return new WedaCloudService(natsClient, deviceAgentClient, telemetryClient, logger: logger);
        });

        return this;
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
        // Register device configurations as IReadOnlyList<DeviceConfiguration>
        var readOnlyConfigs = _deviceConfigurations.AsReadOnly();
        Services.AddSingleton<IReadOnlyList<DeviceConfiguration>>(readOnlyConfigs);

        // Register WedaApplicationContext
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

            return new Context.WedaApplicationContext(options =>
            {
                options.CloudService = cloudService;
                options.LoggerFactory = loggerFactory;
                options.Configuration = configuration;
                options.DeviceOptions = deviceOptions;
                options.ConnectionOptions = connectionOptions;
            });
        });

        // Register device factory
        Services.AddSingleton<IDeviceFactory, DeviceFactory>();

        // Register hosted service for device management
        // Combine both auto-scan devices and manually added devices into a single HostedService
        if (readOnlyConfigs.Count > 0 || _deviceFactories.Count > 0)
        {
            Services.AddHostedService(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DeviceHostedService>>();
                var context = sp.GetRequiredService<IWedaApplicationContext>();
                var allDevices = new List<IDevice>();

                // Add devices from auto-scan configurations
                if (readOnlyConfigs.Count > 0)
                {
                    foreach (var config in readOnlyConfigs)
                    {
                        // DeviceTypeName should have been set in ScanDevicesFromConfiguration()
                        // using the config key if not explicitly specified
                        if (string.IsNullOrEmpty(config.DeviceTypeName))
                        {
                            throw new InvalidOperationException(
                                $"Device '{config.DeviceName}' has no DeviceTypeName. " +
                                $"This should have been automatically set from the DeviceConfigs key. " +
                                $"Please check your configuration or use AddDevice<TDevice>() instead.");
                        }

                        // Resolve device type using the configured resolver
                        var deviceType = _deviceTypeNameResolver.Resolve(config.DeviceTypeName)
                            ?? throw new InvalidOperationException(
                                $"Unable to resolve device type '{config.DeviceTypeName}'. " +
                                $"Search priority:\n" +
                                $"  1. Fully qualified name (with assembly)\n" +
                                $"  2. Your project assembly (PRIORITY)\n" +
                                $"  3. SDK built-in devices (Weda.SubNode.Devices.Generic)\n" +
                                $"  4. Other loaded assemblies\n" +
                                $"Ensure the type exists and the assembly is referenced.");

                        // Create device instance using constructor
                        // This works for both base classes and derived classes (e.g., MyFirstDevice : TcpModbusDevice)
                        try
                        {
                            var device = (IDevice)Activator.CreateInstance(deviceType, context, config)!;
                            allDevices.Add(device);
                        }
                        catch (MissingMethodException)
                        {
                            throw new InvalidOperationException(
                                $"Device type '{deviceType.FullName}' must have a public constructor with signature: " +
                                $"public {deviceType.Name}(IWedaApplicationContext context, DeviceConfiguration configuration)");
                        }
                    }
                }

                // Add manually registered devices via AddDevice<TDevice>()
                if (_deviceFactories.Count > 0)
                {
                    var manualDevices = _deviceFactories.Select(factory => factory(context));
                    allDevices.AddRange(manualDevices);
                }

                return new DeviceHostedService(logger, allDevices);
            });
        }

        var host = _hostBuilder.Build();
        return new WedaApplication(host, readOnlyConfigs);
    }
}
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
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Cloud;
using Weda.SubNode.Cloud.Clients;

namespace Weda.SubNode.Host;

/// <summary>
/// Builder for configuring and creating a WedaApplication
/// Similar to WebApplicationBuilder in ASP.NET Core
/// </summary>
public class WedaApplicationBuilder
{
    private readonly HostApplicationBuilder _hostBuilder;
    private readonly List<DeviceConfiguration> _deviceConfigurations = new();
    private readonly List<Func<IWedaApplicationContext, IDevice>> _deviceFactories = [];

    internal WedaApplicationBuilder(HostApplicationBuilder hostBuilder)
    {
        _hostBuilder = hostBuilder;
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
    /// 1. Dictionary format (recommended):
    /// {
    ///   "DeviceConfigs": {
    ///     "ModbusDevice": { "DeviceName": "...", "DeviceType": "...", ... },
    ///     "OtherDevice": { ... }
    ///   }
    /// }
    ///
    /// 2. Legacy array format:
    /// {
    ///   "Devices": [
    ///     { "Enabled": true, "DeviceName": "...", ... }
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
    public WedaApplicationBuilder ConfigureNats(Action<NatsOptions> configureAction)
    {
        Services.Configure(configureAction);
        return this;
    }

    /// <summary>
    /// Configure telemetry polling interval (in milliseconds)
    /// </summary>
    /// <param name="intervalMs">Polling interval in milliseconds</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder ConfigurePollingInterval(int intervalMs)
    {
        Services.Configure<DeviceOptions>(options =>
        {
            options.DefaultPollingIntervalMs = intervalMs;
        });
        return this;
    }

    /// <summary>
    /// Enable telemetry sending to cloud (uplink)
    /// Devices will send telemetry data to the cloud platform
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddTelemetry()
    {
        Services.Configure<Abstractions.Devices.DeviceOptions>(options =>
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
        Services.Configure<Abstractions.Devices.DeviceOptions>(options =>
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
        Services.Configure<Abstractions.Devices.DeviceOptions>(options =>
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
        Services.Configure<Abstractions.Devices.DeviceOptions>(options =>
        {
            options.EnableConfigUpdates = true;
        });
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

        Services.AddSingleton(_ => Core.WedaFactory.Cloud.Mock);
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

        // Register NatsClient as singleton
        Services.AddSingleton(sp =>
        {
            var natsOptions = sp.GetService<IOptions<NatsOptions>>()?.Value;
            var url = natsOptions?.Url ?? "nats://localhost:4222";
            var natsOpts = NatsOpts.Default with { Url = url, SerializerRegistry = NatsClientDefaultSerializerRegistry.Default };

            // TODO: Add credentials support
            // if (!string.IsNullOrEmpty(natsOptions?.CredentialsFile))
            // {
            //     natsOpts = natsOpts with { CredentialsFile = natsOptions.CredentialsFile };
            // }

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
        Services.AddSingleton<Abstractions.Context.IWedaApplicationContext>(sp =>
        {
            var cloudService = sp.GetRequiredService<IWedaCloudService>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var configuration = sp.GetService<IConfiguration>();

            // Get DeviceOptions from DI (configured by AddTelemetry, AddCommands, etc.)
            var deviceOptions = sp.GetService<Microsoft.Extensions.Options.IOptions<Abstractions.Devices.DeviceOptions>>()?.Value
                ?? Abstractions.Devices.DeviceOptions.Default;

            return new Context.WedaApplicationContext(options =>
            {
                options.CloudService = cloudService;
                options.LoggerFactory = loggerFactory;
                options.Configuration = configuration;
                options.DeviceOptions = deviceOptions;
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
                // Auto-scan creates ModbusDevice (framework built-in) using the standard constructor convention
                if (readOnlyConfigs.Count > 0)
                {
                    foreach (var config in readOnlyConfigs)
                    {
                        // Use Activator to create ModbusDevice with standard constructor:
                        // public ModbusDevice(IWedaApplicationContext context, DeviceConfiguration config)
                        var deviceType = typeof(Core.Devices.ModbusDevice);
                        var device = (IDevice)Activator.CreateInstance(deviceType, context, config)!;
                        allDevices.Add(device);
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

/// <summary>
/// NATS configuration options
/// </summary>
public class NatsOptions
{
    public string? Url { get; set; }
    public string? CredentialsFile { get; set; }
}

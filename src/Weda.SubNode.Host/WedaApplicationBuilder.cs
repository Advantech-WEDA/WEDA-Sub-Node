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
    /// Add telemetry support to the application
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddTelemetry()
    {
        // TODO: Add OpenTelemetry integration
        return this;
    }

    /// <summary>
    /// Add health reporting support to the application
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddHealthReporting()
    {
        // TODO: Add Microsoft.Extensions.Diagnostics.HealthChecks if needed
        // Services.AddHealthChecks();
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
    /// Configure logging with a fluent API
    /// Example: builder.AddLogging(log => log.WriteToConsole().WriteToFile("logs/app.log"))
    /// </summary>
    /// <param name="configure">Action to configure logging</param>
    /// <returns>The builder for chaining</returns>
    public WedaApplicationBuilder AddLogging(Action<LoggingConfigurator> configure)
    {
        var configurator = new LoggingConfigurator();
        configure(configurator);

        Log.Logger = configurator.Build();
        Logging.AddSerilog(Log.Logger);

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

        // Register device factory
        Services.AddSingleton<IDeviceFactory, DeviceFactory>();

        // Register hosted service for device management
        Services.AddHostedService<DeviceHostedService>();

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

/// <summary>
/// Device configuration options
/// </summary>
public class DeviceOptions
{
    public int DefaultPollingIntervalMs { get; set; } = 1000;
}

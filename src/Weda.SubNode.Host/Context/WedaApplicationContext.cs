using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Cloud;
using Weda.SubNode.Cloud.Clients;
using Weda.SubNode.Cloud.Serialization;

namespace Weda.SubNode.Host.Context;

/// <summary>
/// Application context for Weda SubNode SDK.
/// Manages the lifecycle of framework-level services and provides factory methods for creating devices.
/// This is the top-level composition root that assembles all dependencies.
/// </summary>
/// <example>
/// <code>
/// // Simple usage with defaults
/// using var context = new WedaApplicationContext();
/// var device = new ModbusDevice(config, context);
///
/// // Custom configuration
/// using var context = new WedaApplicationContext(options =>
/// {
///     options.CloudService = myCloudService;
///     options.LoggerFactory = myLoggerFactory;
/// });
///
/// // Factory method
/// using var context = new WedaApplicationContext();
/// var device = context.CreateModbusDevice(config);
/// </code>
/// </example>
public class WedaApplicationContext : IWedaApplicationContext
{
    private readonly WedaContextOptions _options;
    private readonly IWedaCloudService _cloudService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly NatsClient? _natsClient;
    private readonly IConfiguration? _configuration;
    private readonly DeviceConfiguration? _deviceConfiguration;
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

        // Setup logger factory
        _loggerFactory = _options.LoggerFactory ?? NullLoggerFactory.Instance;

        // Store configuration reference
        _configuration = _options.Configuration;

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
        string deviceConfigKey = "",
        ILoggerFactory? loggerFactory = null)
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
                    Name = natsSection["Name"] ?? "default",
                    NatsSerializerRegistry = natsSection["SerializerType"]?.ToLower() switch
                    {
                        "json" => WedaNatsSerializerRegistry.Default,
                        _ => WedaNatsSerializerRegistry.Default
                    }
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
    /// Gets the connection options for device connection manager.
    /// </summary>
    public ConnectionOptions ConnectionOptions => _options.ConnectionOptions;

    /// <summary>
    /// Gets the configuration instance.
    /// </summary>
    public IConfiguration? Configuration => _configuration;

    /// <summary>
    /// Gets the device configuration loaded from IConfiguration.
    /// Returns null if no configuration was provided or device config not found.
    /// </summary>
    public DeviceConfiguration? DeviceConfiguration => _deviceConfiguration;

    #region Private Methods

    private DeviceConfiguration? LoadDeviceConfiguration()
    {
        if (_configuration == null)
            return null;

        try
        {
            var deviceConfig = _configuration
                .GetSection(_options.DeviceConfigurationKey)
                .Get<DeviceConfiguration>();

            if (deviceConfig == null)
            {
                _loggerFactory.CreateLogger<WedaApplicationContext>()
                    .LogWarning("Device configuration not found at: {ConfigKey}", _options.DeviceConfigurationKey);
                return null;
            }

            // Auto-load DTDL if enabled
            if (_options.AutoLoadDtdl && !string.IsNullOrEmpty(deviceConfig.DtdlPath))
            {
                try
                {
                    deviceConfig.LoadDtdlAsync(Directory.GetCurrentDirectory()).GetAwaiter().GetResult();
                    _loggerFactory.CreateLogger<WedaApplicationContext>()
                        .LogInformation("DTDL loaded from: {DtdlPath}", deviceConfig.DtdlPath);
                }
                catch (Exception ex)
                {
                    _loggerFactory.CreateLogger<WedaApplicationContext>()
                        .LogWarning(ex, "Failed to load DTDL from: {DtdlPath}", deviceConfig.DtdlPath);
                }
            }

            return deviceConfig;
        }
        catch (Exception ex)
        {
            _loggerFactory.CreateLogger<WedaApplicationContext>()
                .LogError(ex, "Failed to load device configuration from: {ConfigKey}", _options.DeviceConfigurationKey);
            return null;
        }
    }

    private (IWedaCloudService, NatsClient?) CreateDefaultCloudService()
    {
        var settings = _options.NatsConnectionSettings;
        var natsOpts = NatsOpts.Default with
        {
            Url = settings.Url,
            Name = settings.Name,
            SerializerRegistry = settings.NatsSerializerRegistry,
            AuthOpts = !string.IsNullOrEmpty(settings.CredFile)
                ? NatsAuthOpts.Default with { CredsFile = settings.CredFile }
                : NatsAuthOpts.Default
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Host.Configuration;

namespace Weda.SubNode.Host;

/// <summary>
/// Main application class for Weda SubNode applications
/// Similar to WebApplication in ASP.NET Core
/// Provides two creation modes:
/// 1. CreateDefaultBuilder() - Opinionated setup with defaults
/// 2. CreateBuilder() - Flexible setup for custom configuration
/// </summary>
public class WedaApplication : IAsyncDisposable
{
    private readonly IHost _host;

    internal WedaApplication(IHost host)
    {
        _host = host;
        Services = host.Services;
    }

    /// <summary>
    /// Gets the service provider for the application
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Create a WedaApplicationBuilder with default configuration
    /// Automatically configures:
    /// - Serilog logging from appsettings.json (with console output)
    /// - Configuration from appsettings.json, environment variables, and command-line arguments
    /// - Default cloud service
    /// - Telemetry and health reporting
    ///
    /// NOTE: Devices must be explicitly registered via AddDevice&lt;TDevice&gt;().
    /// Auto-scanning (ScanDevicesFromConfiguration) has been removed for explicit device registration.
    ///
    /// Command-line arguments can override any configuration value:
    /// - --Nats:Url=nats://localhost:4222          (override NATS URL)
    /// - --Nats:CredFile=/path/to/creds            (override credentials file)
    /// - --Serilog:MinimumLevel:Default=Debug      (override log level)
    /// - --no-cache                                (force loading from appsettings.json, ignore .device-config-cache.json)
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>A configured WedaApplicationBuilder</returns>
    public static WedaApplicationBuilder CreateDefaultBuilder(string[]? args = null)
    {
        // Check for --no-cache argument
        var useCache = !HasNoCacheArgument(args);

        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args ?? Array.Empty<string>());

        // Configure settings with priority (lowest to highest):
        // 1. appsettings.json (Serilog config - not cloud-synced)
        // 2. appsettings.{Environment}.json
        // 3. systemcfg.json → loaded into "SystemConfig" section (WedaNode)
        // 4. devicecfg.json → loaded into "DeviceConfig" section (SubNode + DeviceConfigs)
        // 5. customcfg.json → loaded into "CustomConfig" section (user-defined)
        // 6. Environment variables
        // 7. Command-line arguments (highest priority)
        //
        // Final configuration structure:
        // {
        //   "Serilog": {},           // from appsettings.json (not cloud-synced)
        //   "SystemConfig": {        // from systemcfg.json
        //     "WedaNode": {}
        //   },
        //   "DeviceConfig": {        // from devicecfg.json
        //     "SubNode": {},
        //     "DeviceConfigs": {}
        //   },
        //   "CustomConfig": {}       // from customcfg.json
        // }
        hostBuilder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            // Serilog configuration (not cloud-synced)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{hostBuilder.Environment.EnvironmentName}.json", optional: true)
            // Cloud-synced config files loaded into separate sections
            .AddJsonFileToSection("systemcfg.json", "SystemConfig", optional: true, reloadOnChange: true)
            .AddJsonFileToSection("devicecfg.json", "DeviceConfig", optional: true, reloadOnChange: true)
            .AddJsonFileToSection("customcfg.json", "CustomConfig", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args ?? Array.Empty<string>());

        // Configure Serilog with console output
        // If appsettings.json has Serilog config, use it; otherwise use defaults
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(hostBuilder.Configuration);

        // Always ensure console output is enabled (even if not in config)
        var hasConsoleConfig = hostBuilder.Configuration
            .GetSection("Serilog:WriteTo")
            .GetChildren()
            .Any(c => c.GetValue<string>("Name")?.Equals("Console", StringComparison.OrdinalIgnoreCase) == true);

        if (!hasConsoleConfig)
        {
            // No console sink in config, add default console output
            loggerConfig
                .MinimumLevel.Information()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        Log.Logger = loggerConfig.CreateLogger();

        hostBuilder.Logging.ClearProviders();
        hostBuilder.Logging.AddSerilog(Log.Logger);

        var builder = new WedaApplicationBuilder(hostBuilder, useCache);

        // Configure WedaFactory to use the logger factory from DI
        // This ensures all WedaFactory-created instances have proper logging
        var serviceProvider = hostBuilder.Services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        Core.WedaFactory.UseLoggerFactory(loggerFactory);

        // Add default cloud service (real NATS-based cloud service by default)
        // User can override by calling .UseMockCloud() for testing
        builder.UseDefaultCloud();

        // Enable all features by default (uplink + downlink)
        builder.AddTelemetry();           // Uplink: Send telemetry data
        builder.AddHealthReporting();     // Uplink: Send health status
        builder.AddCommands();            // Downlink: Receive commands
        builder.AddConfigUpdates();       // Downlink: Receive config updates
        builder.AddRecording();

        return builder;
    }

    /// <summary>
    /// Create a WedaApplicationBuilder with minimal configuration
    /// Provides a clean slate for custom configuration
    /// Automatically configures:
    /// - Configuration from appsettings.json, environment variables, and command-line arguments
    /// - Default cloud service
    ///
    /// User is responsible for:
    /// - Adding logging (call .AddLogging() to configure from appsettings.json)
    /// - Adding devices via AddDevice&lt;TDevice&gt;()
    /// - Adding telemetry and health reporting (if needed)
    ///
    /// Command-line arguments can override any configuration value:
    /// - --Nats:Url=nats://localhost:4222          (override NATS URL)
    /// - --Nats:CredFile=/path/to/creds            (override credentials file)
    /// - --Serilog:MinimumLevel:Default=Debug      (override log level)
    /// - --no-cache                                (force loading from appsettings.json, ignore .device-config-cache.json)
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>A minimal WedaApplicationBuilder</returns>
    public static WedaApplicationBuilder CreateBuilder(string[]? args = null)
    {
        // Check for --no-cache argument
        var useCache = !HasNoCacheArgument(args);

        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args ?? Array.Empty<string>());

        // Configuration - load config files, environment variables, and command-line arguments
        // Each cloud-synced config file is loaded into its own section to prevent key conflicts
        // Priority (lowest to highest): appsettings.json < section-prefixed configs < env vars < args
        hostBuilder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            // Serilog configuration (not cloud-synced)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{hostBuilder.Environment.EnvironmentName}.json", optional: true)
            // Cloud-synced config files loaded into separate sections
            .AddJsonFileToSection("systemcfg.json", "SystemConfig", optional: true, reloadOnChange: true)
            .AddJsonFileToSection("devicecfg.json", "DeviceConfig", optional: true, reloadOnChange: true)
            .AddJsonFileToSection("customcfg.json", "CustomConfig", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args ?? Array.Empty<string>());

        var builder = new WedaApplicationBuilder(hostBuilder, useCache);

        // Add default cloud service (can be overridden with .UseMockCloud())
        builder.UseDefaultCloud();

        return builder;
    }

    /// <summary>
    /// Run the application asynchronously
    /// This starts all registered devices and keeps the application running
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the application</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var logger = Services.GetRequiredService<ILogger<WedaApplication>>();
            logger.LogInformation("Starting Weda SubNode Application");

            await _host.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// Stop the application
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _host.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Dispose the application and all its resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _host?.Dispose();
        }

        await Log.CloseAndFlushAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Check if the --no-cache argument is present in command-line arguments
    /// </summary>
    private static bool HasNoCacheArgument(string[]? args)
    {
        if (args == null || args.Length == 0)
            return false;

        return args.Any(arg =>
            arg.Equals("--no-cache", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-no-cache", StringComparison.OrdinalIgnoreCase));
    }
}

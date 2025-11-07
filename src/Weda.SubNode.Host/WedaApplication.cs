using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Abstractions.Devices;

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
    private readonly IReadOnlyList<DeviceConfiguration> _deviceConfigurations;

    internal WedaApplication(IHost host, IReadOnlyList<DeviceConfiguration> deviceConfigurations)
    {
        _host = host;
        _deviceConfigurations = deviceConfigurations;
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
    /// - Configuration from appsettings.json and environment variables
    /// - Default cloud service
    /// - Automatic device scanning from appsettings.json
    /// - Telemetry and health reporting
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>A configured WedaApplicationBuilder</returns>
    public static WedaApplicationBuilder CreateDefaultBuilder(string[]? args = null)
    {
        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args ?? Array.Empty<string>());

        // Configure default settings
        hostBuilder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{hostBuilder.Environment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

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

        var builder = new WedaApplicationBuilder(hostBuilder);

        // Configure WedaFactory to use the logger factory from DI
        // This ensures all WedaFactory-created instances have proper logging
        var serviceProvider = hostBuilder.Services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        Core.WedaFactory.UseLoggerFactory(loggerFactory);

        // Add default cloud service (real NATS-based cloud service by default)
        // User can override by calling .UseMockCloud() for testing
        builder.UseDefaultCloud();

        // Auto-scan devices from configuration
        builder.ScanDevicesFromConfiguration();

        // Add default telemetry and health reporting
        builder.AddTelemetry();
        builder.AddHealthReporting();

        return builder;
    }

    /// <summary>
    /// Create a WedaApplicationBuilder with minimal configuration
    /// Provides a clean slate for custom configuration
    /// Automatically configures:
    /// - Serilog logging from appsettings.json
    /// - Configuration from appsettings.json and environment variables
    /// - Default cloud service
    ///
    /// User is responsible for:
    /// - Adding devices (manually or via ScanDevicesFromConfiguration)
    /// - Adding telemetry and health reporting (if needed)
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>A minimal WedaApplicationBuilder</returns>
    public static WedaApplicationBuilder CreateBuilder(string[]? args = null)
    {
        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args ?? Array.Empty<string>());

        // Configuration - load appsettings.json and environment variables
        hostBuilder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{hostBuilder.Environment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables();

        // Configure Serilog from appsettings.json
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(hostBuilder.Configuration)
            .CreateLogger();

        hostBuilder.Logging.ClearProviders();
        hostBuilder.Logging.AddSerilog(Log.Logger);

        var builder = new WedaApplicationBuilder(hostBuilder);

        // Configure WedaFactory to use the logger factory from DI
        var serviceProvider = hostBuilder.Services.BuildServiceProvider();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        Core.WedaFactory.UseLoggerFactory(loggerFactory);

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
            logger.LogInformation("Registered {DeviceCount} device(s)", _deviceConfigurations.Count);

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
}

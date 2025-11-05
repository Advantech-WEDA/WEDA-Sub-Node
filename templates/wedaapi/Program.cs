using Microsoft.Extensions.Configuration;
using Serilog;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core;
using WedaApi;

// ═══════════════════════════════════════════════════════════════════════════
// WedaApplication - Simple Pattern with MyFirstDevice Example
// ═══════════════════════════════════════════════════════════════════════════
// This template demonstrates:
// - Creating a custom device (MyFirstDevice)
// - Using MockCloudService for standalone operation
// - Automatic Modbus TCP communication
// - Event-driven telemetry processing
// ═══════════════════════════════════════════════════════════════════════════

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Configure logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Starting WedaApi with MyFirstDevice...");

    // Load device configuration
    var deviceConfig = configuration.GetSection("DeviceConfigs:MyFirstDevice").Get<DeviceConfiguration>()
        ?? throw new InvalidOperationException("Device configuration not found");

    // Create context with MockCloudService (no real cloud connection needed)
    var context = new WedaApplicationContext(options =>
    {
        options.CloudService = WedaFactory.Cloud.Mock; // Use mock cloud service
    });

    // Create your custom device
    var device = new MyFirstDevice(context, deviceConfig);

    // Initialize and start
    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Log.Information("✅ MyFirstDevice started successfully!");
    Log.Information("   Connecting to Modbus device at {Host}:{Port}",
        deviceConfig.Communication.GetValueOrDefault("Host", "unknown"),
        deviceConfig.Communication.GetValueOrDefault("Port", 0));
    Log.Information("   Press Ctrl+C to stop...");

    // Wait for cancellation
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    await Task.Delay(Timeout.Infinite, cts.Token);

    // Graceful shutdown
    await device.StopAsync();
    device.Dispose();
}
catch (OperationCanceledException)
{
    Log.Information("Application cancelled");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core;
using WedaApiC;

// ═══════════════════════════════════════════════════════════════════════════
// WedaApplication - Advanced Pattern with MyFirstDevice Example
// ═══════════════════════════════════════════════════════════════════════════
// This template demonstrates advanced features:
// - Full control over configuration and logging
// - Custom device implementation with event subscriptions
// - MockCloudService for standalone operation
// - Advanced telemetry processing and business rules
// - Event-driven architecture
// ═══════════════════════════════════════════════════════════════════════════

// 1. Load Configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// 2. Configure Logging (Serilog)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithProperty("Application", "WedaApiAdvanced")
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger));

try
{
    Log.Information("╔════════════════════════════════════════════════════════╗");
    Log.Information("║ WedaApi-C: Advanced Control Pattern Example           ║");
    Log.Information("╚════════════════════════════════════════════════════════╝");

    // 3. Load Device Configuration
    var deviceConfig = configuration.GetSection("DeviceConfigs:MyFirstDevice").Get<DeviceConfiguration>()
        ?? throw new InvalidOperationException("Device configuration 'DeviceConfigs:MyFirstDevice' not found in appsettings.json");

    // 4. Create ApplicationContext with MockCloudService
    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;
        options.CloudService = WedaFactory.Cloud.Mock; // Use mock cloud service
    });

    Log.Information("✅ Application context created with MockCloudService");

    // 5. Create Your Custom Device Instance
    var device = new MyFirstDevice(context, deviceConfig);

    // 6. Initialize and Start Device
    Log.Information("🔄 Initializing MyFirstDevice...");

    var initialized = await device.InitializeAsync();
    if (!initialized)
    {
        Log.Error("❌ Failed to initialize device");
        return;
    }

    await device.StartAsync();

    Log.Information("✅ MyFirstDevice started successfully!");
    Log.Information("   Connecting to Modbus device at {Host}:{Port}",
        deviceConfig.Communication.GetValueOrDefault("Host", "unknown"),
        deviceConfig.Communication.GetValueOrDefault("Port", 0));
    Log.Information("   Sensor count: {Count}", deviceConfig.Sensors.Count);
    Log.Information("");
    Log.Information("📊 The device will now:");
    Log.Information("   - Read sensor values from Modbus device");
    Log.Information("   - Process telemetry through custom business rules");
    Log.Information("   - Send telemetry to MockCloudService (logged only)");
    Log.Information("   - Monitor for threshold violations");
    Log.Information("");
    Log.Information("Press Ctrl+C to stop...");

    // 9. Wait for cancellation
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Log.Information("Shutdown requested...");
    };

    await Task.Delay(Timeout.Infinite, cts.Token);

    // 10. Graceful Shutdown
    Log.Information("Stopping device...");
    await device.StopAsync();
    device.Dispose();
    Log.Information("Device stopped");
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

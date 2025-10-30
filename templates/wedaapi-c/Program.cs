using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Host;

// ═══════════════════════════════════════════════════════════════════════════
// WedaApplication - CreateBuilder Pattern (Advanced / Full Control)
// ═══════════════════════════════════════════════════════════════════════════
// Use this pattern when you need fine-grained control over:
// - Configuration loading strategy
// - Logging setup and customization
// - Manual device instantiation
// - Cloud service selection (Mock / Real)
// - Event handlers and custom logic
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
    Log.Information("Starting Weda SubNode Application (Advanced Mode)");

    // 3. Load Device Configuration
    var deviceConfig = configuration.GetSection("DeviceConfigs:ModbusDevice").Get<DeviceConfiguration>()
        ?? throw new InvalidOperationException("Device configuration 'DeviceConfigs:ModbusDevice' not found in appsettings.json");

    // 4. Load NATS configuration
    var natsUrl = configuration["Nats:Url"] ?? "nats://localhost:4222";

    // 5. Create ApplicationContext with logging
    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;
        options.NatsUrl = natsUrl;
    });

    Log.Information("NATS URL configured: {NatsUrl}", natsUrl);

    // 6. Create Device Instance - Simple API with context
    var device = new ModbusDevice(context, deviceConfig);

    // 7. Subscribe to Device Events (Optional)
    device.DataReceived += (sender, e) =>
    {
        Log.Information("Data received from {DeviceId}: {Count} measures",
            e.DeviceId, e.Data.Count);
    };

    device.TelemetrySent += (sender, e) =>
    {
        Log.Information("Telemetry sent: {Success}, Count: {Count}",
            e.Success, e.MeasureCount);
    };

    device.ConnectionStateChanged += (sender, e) =>
    {
        Log.Information("Connection state changed: {Previous} -> {Current}",
            e.PreviousState, e.CurrentState);
    };

    // 8. Initialize and Start Device
    var initialized = await device.InitializeAsync();
    if (!initialized)
    {
        Log.Error("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Log.Information("Device started successfully. Press Ctrl+C to stop...");

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

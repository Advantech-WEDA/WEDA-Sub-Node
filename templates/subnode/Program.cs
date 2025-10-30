using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;
using WedaSubNode;

// ═══════════════════════════════════════════════════════════════════════════
// SubNode Template - MyFirstDevice Pattern
// ═══════════════════════════════════════════════════════════════════════════
// This template demonstrates how to create a custom device by inheriting
// from TcpModbusDevice. Communication is automatically created from configuration.
// Dependencies: Weda.SubNode.Host, Weda.SubNode.Devices, Weda.SubNode.Cloud
// ═══════════════════════════════════════════════════════════════════════════

// Configuration & Logging Setup
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog(Log.Logger));

try
{
    Log.Information("Starting MyFirstDevice application...");

    // Load device configuration
    var config = configuration.GetSection("DeviceConfigs:MyFirstDevice").Get<DeviceConfiguration>()
        ?? throw new InvalidOperationException("Device configuration not found");

    // Load NATS configuration
    var natsUrl = configuration["Nats:Url"] ?? "nats://localhost:4222";

    // Create ApplicationContext with logging
    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;
        options.NatsUrl = natsUrl;
    });

    Log.Information("NATS URL configured: {NatsUrl}", natsUrl);

    // Create your custom device instance - Simple API!
    var device = new MyFirstDevice(context, config);

    // Initialize and start the device
    var initialized = await device.InitializeAsync();
    if (!initialized)
    {
        Log.Error("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Log.Information("Device started successfully. Press Ctrl+C to stop...");

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

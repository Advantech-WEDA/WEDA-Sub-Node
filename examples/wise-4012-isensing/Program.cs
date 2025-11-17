using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Host.Context;
using Wise4012ISensingExample;

// ═══════════════════════════════════════════════════════════════════════════
// SubNode ISensing Template - MyFirstISensingDevice Pattern
// ═══════════════════════════════════════════════════════════════════════════
// Usage:
//   dotnet run          - Start the ISensing device with MQTT
// ═══════════════════════════════════════════════════════════════════════════

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
    // ═══════════════════════════════════════════════════════════════════════════
    // Code starts from here
    // ═══════════════════════════════════════════════════════════════════════════
    // Auto-select first device config from DeviceConfigs section (MyFirstISensingDevice)
    using var context = new WedaApplicationContext(configuration, loggerFactory, "MyFirstISensingDevice");
    var device = new MyFirstISensingDevice(context);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize device");
        return;
    }

    // Start the device (connects to MQTT and subscribes to topics)
    await device.StartAsync();
    Log.Information("Device started. Press Ctrl+C to stop...");

    // ═══════════════════════════════════════════════════════════════════════════
    // Wait for Ctrl+C to stop the application
    // ═══════════════════════════════════════════════════════════════════════════
    var cts = new CancellationTokenSource();
    
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);

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

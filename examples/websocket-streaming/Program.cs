using Microsoft.Extensions.Configuration;
using Serilog;
using Weda.SubNode.Host.Context;
using WebSocketStreamingExample;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    using var context = new WedaApplicationContext(configuration, deviceConfigKey: "WebSocketStreamingDeviceConfig");
    var config = context.DeviceConfiguration ?? throw new InvalidOperationException("Device configuration not found");

    // Get WebSocket URI from configuration
    var webSocketUri = configuration["WebSocket:Uri"]
        ?? throw new InvalidOperationException("WebSocket URI not configured");

    // Create device using inheritance pattern (new architecture)
    // WebSocketStreamingDevice -> StreamingDeviceBase -> DeviceBase
    var device = new WebSocketStreamingDevice(context, config, webSocketUri);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize WebSocket streaming device");
        return;
    }

    await device.StartAsync();
    Log.Information("WebSocket streaming device started. Press Ctrl+C to stop...");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

    try
    {
        await Task.Delay(Timeout.Infinite, cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Expected on Ctrl+C
    }

    await device.StopAsync();
    device.Dispose();
}
catch (OperationCanceledException)
{
    Log.Information("WebSocket streaming device stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "WebSocket streaming device terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

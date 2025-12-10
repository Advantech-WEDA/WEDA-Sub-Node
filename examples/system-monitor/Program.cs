using Microsoft.Extensions.Configuration;
using Serilog;
using SystemMonitorExample.Devices;
using Weda.SubNode.Host.Context;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    using var context = new WedaApplicationContext(configuration, deviceConfigKey: "SystemMonitorDeviceConfig");
    var config = context.DeviceConfiguration ?? throw new InvalidOperationException("Device configuration not found");

    var device = new LocalSystemMonitorDevice(context, config);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize system monitor");
        return;
    }

    await device.StartAsync();
    Log.Information("System monitor started. Press Ctrl+C to stop...");

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
    Log.Information("System monitor stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "System monitor terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

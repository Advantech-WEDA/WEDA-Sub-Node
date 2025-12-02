using Microsoft.Extensions.Configuration;
using Serilog;
using Weda.SubNode.Host.Context;
using SystemMonitorExample;

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    using var context = new WedaApplicationContext(configuration, deviceConfigKey: "SystemMonitorDeviceConfig");
    var config = context.DeviceConfiguration ?? throw new InvalidOperationException("Device configuration not found");
    var communication = new NullCommunication();
    var device = new SystemMonitorDevice(context, config, communication);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize system monitor");
        return;
    }

    await device.StartAsync();
    Log.Information("System monitor started. Press Ctrl+C to stop...");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);

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

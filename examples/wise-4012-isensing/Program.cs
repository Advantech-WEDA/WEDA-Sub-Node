using Serilog;
using Weda.SubNode.Host.Context;
using Wise4012ISensingExample;

try
{
    using var context = new WedaApplicationContext();
    var device = new MyFirstISensingDevice(context);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Log.Information("Device started. Press Ctrl+C to stop...");

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

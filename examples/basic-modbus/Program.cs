using Microsoft.Extensions.Configuration;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Communication;

try
{
    // Create ApplicationContext - SDK will automatically load appsettings.json
    using var context = new WedaApplicationContext();

    var config = context.Configuration?.GetSection("DeviceConfigs:ModbusDevice").Get<DeviceConfiguration>()
        ?? throw new InvalidOperationException("DeviceConfigs:ModbusDevice configuration not found");

    Console.WriteLine("ApplicationContext created successfully");

    // Create TCP communication
    var host = config.Communication.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
    var port = config.Communication.TryGetValue("Port", out var p) ? Convert.ToInt32(p) : 502;
    var communication = new TcpCommunication(host, port);

    // Simple and clean API - config, context, and communication
    var device = new ModbusDevice(context, config, communication);

    var initialized = await device.InitializeAsync();
    if (!initialized)
    {
        Console.WriteLine("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Console.WriteLine("Device started. Press Ctrl+C to stop...");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Application stopped");
}
catch (Exception ex)
{
    Console.WriteLine($"Error in SubNode: {ex}");
}

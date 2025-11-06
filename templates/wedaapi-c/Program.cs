using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core;
using Weda.SubNode.Simulators.Modbus;
using WedaApiC;

// Configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithProperty("Application", "WedaApiC")
    .CreateLogger();

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);

try
{
    // Start Modbus Simulator
    var simulatorConfig = configuration.GetSection("TcpModbusSimulatorConfiguration").Get<TcpModbusSimulatorConfiguration>()
        ?? throw new InvalidOperationException("Simulator configuration not found");

    var simulator = new TcpModbusSimulator(simulatorConfig, logger: loggerFactory.CreateLogger<TcpModbusSimulator>());
    await simulator.StartAsync();

    Log.Information("Simulator started on {IP}:{Port}",
        simulatorConfig.TcpConnection.IpAddress,
        simulatorConfig.TcpConnection.Port);

    // Start Device
    var deviceConfig = configuration.GetSection("DeviceConfigs:MyFirstDevice").Get<DeviceConfiguration>()
        ?? throw new InvalidOperationException("Device configuration not found");

    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;
        options.CloudService = WedaFactory.Cloud.Mock;
    });

    var device = new MyFirstDevice(context, deviceConfig);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Log.Information("MyFirstDevice started. Press Ctrl+C to stop...");

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
    await simulator.StopAsync();
}
catch (OperationCanceledException)
{
    Log.Information("Application stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

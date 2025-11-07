using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Cloud.Serialization;
using Weda.SubNode.Core.Communication;

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
    var config = configuration.GetSection("DeviceConfigs:ModbusDevice").Get<DeviceConfiguration>()
        ?? throw new InvalidOperationException("DeviceConfigs:ModbusDevice configuration not found");

    // Load NATS configuration
    var natsUrl = configuration["Nats:Url"] ?? "nats://localhost:4222";

    // Create ApplicationContext with logging
    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;
        options.NatsConnectionSettings = NatsConnectionSettings.Default with
        {
            Url = natsUrl,
            NatsSerializerRegistry = WedaNatsSerializerRegistry.Default
        };
    });

    Log.Information("NATS URL configured: {NatsUrl}", natsUrl);

    // Create TCP communication
    var host = config.Communication.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
    var port = config.Communication.TryGetValue("Port", out var p) ? Convert.ToInt32(p) : 502;
    var communication = new TcpCommunication(host, port);

    // Simple and clean API - config, context, and communication
    var device = new ModbusDevice(context, config, communication);

    var initialized = await device.InitializeAsync();
    if (!initialized)
    {
        Log.Error("Failed to initialize device");
        return;
    }

    // var measures = await device.ReadTelemetryAsync();
    // if (measures.Count == 0)
    // {
    //     Log.Error("Failed to read telemetry");
    //     return;
    // }

    // while (true)
    // {
    //     try
    //     {
    //         var measures = await device.ReadTelemetryAsync();

    //         foreach (var measure in measures)
    //         {
    //             Console.WriteLine(measure.ValueObject);
    //         }

    //         Console.WriteLine("Press any key to continue, or 'Q' to quit...\n");
    //         var key = Console.ReadKey(intercept: true);

    //         if (key.KeyChar == 'q' || key.KeyChar == 'Q')
    //         {
    //             Console.WriteLine("\nExiting program...");
    //             break;
    //         }

    //         if (key.KeyChar == 'r' || key.KeyChar == 'R')
    //         {
    //             await device.ReportHealthAsync();
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error reading telemetry: {ex.Message}");
    //         Console.WriteLine("Press any key to retry, or 'Q' to quit...\n");

    //         var key = Console.ReadKey(intercept: true);
    //         if (key.KeyChar == 'q' || key.KeyChar == 'Q')
    //         {
    //             Console.WriteLine("\nExiting program...");
    //             break;
    //         }
    //     }
    // }

    await device.StartAsync();
    Log.Information("Device started. Press Ctrl+C to stop...");

    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    // Normal shutdown
}
catch (Exception ex)
{
    Log.Error(ex, "Error in SubNode");
}
finally
{
    await Log.CloseAndFlushAsync();
}

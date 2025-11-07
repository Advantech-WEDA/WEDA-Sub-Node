using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Weda.SubNode.Core.Protocols.ISensing;
using Weda.SubNode.Devices.Advantech;
using Weda.SubNode.Host.Context;

namespace Wise4012ISensingExample;

/// <summary>
/// Example showing programmatic device configuration using MqttISensingDeviceConfiguration
/// instead of appsettings.json.
///
/// To run this example instead of Program.cs:
/// 1. Comment out the code in Program.cs
/// 2. Uncomment the Main method signature below
/// 3. Run: dotnet run
/// </summary>
public static class ProgrammaticConfigExample
{
    // Uncomment the line below to use this as the entry point
    // public static async Task Main(string[] args)
    public static async Task RunExample(string[] args)
    {
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
            // Strongly-typed programmatic configuration (no appsettings.json needed!)
            // ═══════════════════════════════════════════════════════════════════════════
            var deviceConfig = new MqttISensingDeviceConfiguration
            {
                DeviceName = "WISE-4012SE-001",
                Manufacturer = "Advantech",
                Model = "WISE-4012SE",
                MacAddress = "00d0c9aabbcc", // Replace with your device's MAC
                GroupId = "my-factory-floor-1"
            }
            .WithBroker("localhost", 1883, useTls: false)
            .WithCredentials("admin", "password123")
            .AddSensor(
                name: "AI0",
                dtmi: "dtmi:advantech:edgesync:devicesensors;1",
                fieldName: "ai0",
                sensorGroup: Weda.SubNode.Abstractions.Telemetry.SensorGroup.AI,
                unit: "V"
            )
            .AddSensor(
                name: "AI1",
                dtmi: "dtmi:advantech:edgesync:devicesensors;1",
                fieldName: "ai1",
                sensorGroup: Weda.SubNode.Abstractions.Telemetry.SensorGroup.AI,
                unit: "mA"
            )
            .AddSensor(
                name: "AI2",
                dtmi: "dtmi:advantech:edgesync:devicesensors;1",
                fieldName: "ai2",
                sensorGroup: Weda.SubNode.Abstractions.Telemetry.SensorGroup.AI,
                unit: "°C"
            )
            .AddSensor(
                name: "AI3",
                dtmi: "dtmi:advantech:edgesync:devicesensors;1",
                fieldName: "ai3",
                sensorGroup: Weda.SubNode.Abstractions.Telemetry.SensorGroup.AI,
                unit: "%"
            );

            // Convert to generic DeviceConfiguration
            var genericConfig = deviceConfig.ToDeviceConfiguration();

            // Create context and device instance
            using var context = new WedaApplicationContext(configuration, loggerFactory);
            var device = new Wise4012SeDevice(context, genericConfig);

            if (!await device.InitializeAsync())
            {
                Log.Error("Failed to initialize device");
                return;
            }

            await device.StartAsync();
            Log.Information("Device started with programmatic configuration. Press Ctrl+C to stop...");

            // Wait for Ctrl+C
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
    }
}

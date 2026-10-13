using Microsoft.Extensions.Configuration;

using Serilog;

using SystemAgentExample;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    System Agent");
Console.WriteLine("===========================================");
Console.WriteLine();

try
{
    // Step 1: Validate configuration (validation now happens in device lifecycle hooks)
    Console.WriteLine("Configuration validation will be performed during device initialization...");
    Console.WriteLine("[OK] Ready to start");

    // Step 2: Build the application with required features
    var builder = WedaApplication.CreateBuilder(args)
        .AddLogging()
        .AddTelemetry()        // Uplink: Send telemetry to cloud
        .AddHealthReporting()  // Uplink: Send health reports to cloud
        .AddConfigUpdates()    // Downlink: Receive and validate config updates from cloud via NATS
        .AddCommands()
        .AddRecording();        // Downlink: Receive and execute commands

    // Step 3: Register LocalSystemAgentDevice
    // Note: Device configuration validation happens in ValidateConfigurationUpdate override
    const string deviceConfigKey = "SystemAgentDeviceConfig";

    // A devicecfg.json that declares no heartbeat gets the default one, added before the
    // device is bound so the SDK treats it as declared; see HeartbeatConfigCompat.
    var heartbeatDefaults = HeartbeatConfigCompat.DefaultSensorSettings(builder.Configuration, deviceConfigKey);
    if (heartbeatDefaults != null)
    {
        builder.Configuration.AddInMemoryCollection(heartbeatDefaults);
        Console.WriteLine(
            $"[INFO] devicecfg.json declares no '{Heartbeat.SensorName}' sensor; " +
            $"using the default heartbeat (every {Heartbeat.DefaultIntervalMilliseconds} ms)");
    }

    builder.AddDevice<LocalSystemAgentDevice>(deviceConfigKey);

    // Step 4: Build and run the application
    var app = builder.Build();

    Console.WriteLine("Starting system agent...");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine();

    await app.RunAsync();

    return 0; // Success
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL ERROR: {ex.Message}");
    Console.Error.WriteLine($"Exception Type: {ex.GetType().FullName}");
    Console.Error.WriteLine($"Stack Trace:");
    Console.Error.WriteLine(ex.StackTrace);
    
    if (ex.InnerException != null)
    {
        Console.Error.WriteLine($"\nInner Exception: {ex.InnerException.Message}");
        Console.Error.WriteLine($"Inner Exception Type: {ex.InnerException.GetType().FullName}");
        Console.Error.WriteLine($"Inner Stack Trace:");
        Console.Error.WriteLine(ex.InnerException.StackTrace);
    }
    
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 255; // Unexpected error
}
finally
{
    await Log.CloseAndFlushAsync();
}

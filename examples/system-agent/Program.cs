using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Serilog;

using SystemAgentExample;

using Weda.SubNode.Host;
using Weda.SubNode.Host.Configuration;

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
        .AddCommands();        // Downlink: Receive and execute commands

    // Step 3: Register LocalSystemAgentDevice
    // Note: Device configuration validation happens in ValidateConfigurationUpdate override
    builder.AddDevice<LocalSystemAgentDevice>("SystemAgentDeviceConfig");

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

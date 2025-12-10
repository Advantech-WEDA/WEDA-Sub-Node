using SystemMonitorExample;
using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    System Monitor");
Console.WriteLine("===========================================");
Console.WriteLine();

var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()
    .AddTelemetry()        // uplink
    .AddHealthReporting()  // uplink
    // .AddCommands()      // System monitor doesn't need commands
    // .AddConfigUpdates() // System monitor doesn't need config updates
    .UseMockCloud();       // Use mock server for demo (no Weda.Core needed)

// Register LocalSystemMonitorDevice
builder.AddDevice<LocalSystemMonitorDevice>("SystemMonitorDeviceConfig");

// Build and run the application
var app = builder.Build();

Console.WriteLine("Starting system monitor...");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await app.RunAsync();

using StockMonitor;
using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    Stock Monitor - Advantech (2395)");
Console.WriteLine("    Taiwan Stock Exchange Real-Time Data");
Console.WriteLine("===========================================");
Console.WriteLine();

var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()
    .AddTelemetry()        // uplink
    .AddHealthReporting()  // uplink
    // .AddCommands()      // Stock monitor doesn't need commands
    // .AddConfigUpdates() // Stock monitor doesn't need config updates
    .UseMockCloud();       // Use mock server for demo (no Weda.Core needed)

// Register TwseStockMonitorDevice (uses TWSE HTTP API)
builder.AddDevice<TwseStockMonitorDevice>("StockMonitorConfig");

// Build and run the application
var app = builder.Build();

Console.WriteLine("Starting stock monitor...");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await app.RunAsync();

using StockMonitor;
using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    Stock Monitor - Advantech (2395)");
Console.WriteLine("    Taiwan Stock Exchange Real-Time Data");
Console.WriteLine("===========================================");
Console.WriteLine();

var builder = WedaApplication.CreateDefaultBuilder(args);

// Cloud target is configuration-driven so the same image runs either way:
//   customcfg.json "UseMockCloud": true  -> log telemetry locally, no WedaNode needed
//   customcfg.json "UseMockCloud": false -> publish to the WedaNode in systemcfg.json
if (bool.TryParse(builder.Configuration["CustomConfig:UseMockCloud"], out var useMockCloud) && useMockCloud)
{
    builder.UseMockCloud();
}

// Register TwseStockMonitorDevice (uses TWSE HTTP API)
builder.AddDevice<TwseStockMonitorDevice>("StockMonitorConfig");

// Build and run the application
var app = builder.Build();

Console.WriteLine("Starting stock monitor...");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await app.RunAsync();

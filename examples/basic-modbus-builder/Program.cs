using Weda.SubNode.Host;
using BasicModbusBuilderExample;

// Create WedaApplication using Builder pattern (similar to ASP.NET Core)
var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()           // Configure logging from appsettings.json
    .AddTelemetry()         // Enable telemetry upload to cloud
    .AddHealthReporting()   // Enable health status reporting to cloud
    .AddCommands()          // Enable command receiving from cloud
    .AddConfigUpdates();    // Enable configuration updates from cloud

// Add custom device with configuration from appsettings.json
// Configuration section name: "DeviceConfigs:MyModbusDevice"
builder.AddDevice<MyModbusDevice>("MyModbusDevice");

// Build and run the application
var app = builder.Build();
await app.RunAsync();

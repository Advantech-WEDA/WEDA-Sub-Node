using Weda.SubNode.Host;
using Wise4012Builder;

var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()
    .AddTelemetry()        // uplink
    .AddHealthReporting()  // uplink
    .AddCommands()         // downlink
    .AddConfigUpdates();   // downlink

// It will reference to DeviceConfigs:MyFirstDeviceConfig in appsettings.json
builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");

// Build and run the application
var app = builder.Build();
await app.RunAsync();

using Weda.SubNode.Host;
using Wise4012Builder;

var builder = WedaApplication.CreateDefaultBuilder(args);

// It will reference to DeviceConfigs:MyFirstDeviceConfig in appsettings.json
builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");

// Build and run the application
var app = builder.Build();
await app.RunAsync();

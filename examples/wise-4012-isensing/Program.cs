using Serilog;

using Weda.SubNode.Host;
using Wise4012ISensingExample;

try
{
    // Switch to the standard WedaApplicationBuilder pipeline so the host
    // loader records (sectionName -> typeof(MyFirstISensingDevice)) and
    // resolves DeviceTypeName from the [DeviceType] attribute, enabling typed
    // sensor-dtmi dispatch. The previous hand-wired
    // SubNode + WedaApplicationContext path bypassed that tracking.
    var builder = WedaApplication.CreateDefaultBuilder(args);
    builder.AddDevice<MyFirstISensingDevice>("MyFirstDevice");

    var app = builder.Build();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

using daq_feature_proxy.Devices;
using Serilog;
using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    DAQ Feature Proxy");
Console.WriteLine("===========================================");
Console.WriteLine();

try
{
    var builder = WedaApplication.CreateDefaultBuilder(args);

    builder.AddDevice<FeatureProxyDevice>("FeatureProxyConfig");

    var app = builder.Build();

    Console.WriteLine("Starting DAQ feature proxy...");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine();

    await app.RunAsync();

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FATAL ERROR: {ex.Message}");
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 255;
}
finally
{
    await Log.CloseAndFlushAsync();
}

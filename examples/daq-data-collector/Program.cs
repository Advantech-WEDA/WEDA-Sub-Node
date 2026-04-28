using daq_data_collector.Devices;

using Serilog;

using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    DAQ Data Collector");
Console.WriteLine("===========================================");
Console.WriteLine();

try
{
    var builder = WedaApplication.CreateBuilder(args)
        .UseMockCloud()     // Uncomment to enable mock cloud connectivity for testing (no real cloud connection)
        .AddLogging()
        .AddTelemetry()
        .AddHealthReporting()
        .AddConfigUpdates()
        .AddCommands()
        .AddRecording();

    builder.AddDevice<UniaxialVibrationDevice>("UniaxialVibrationDeviceConfig");

    var app = builder.Build();

    Console.WriteLine("Starting DAQ data collector...");
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

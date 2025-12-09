using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PowerAggregationExample.Devices;
using PowerAggregationExample.Simulators;
using Serilog;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;

try
{
    // Build the application with logging from appsettings.json
    var builder = WedaApplication.CreateBuilder(args)
        .AddLogging()
        .AddTelemetry()
        .AddHealthReporting()
        .UseMockCloud();

    builder.AddDevice<CurrentSensorDevice>("CurrentSensor");
    builder.AddDevice<VoltageSensorDevice>("VoltageSensor");
    builder.AddDevice<PowerAggregatorDevice>("PowerAggregator");

    var app = builder.Build();

    // Start Modbus simulators before starting devices
    Log.Information("Starting Modbus simulators...");

    var loggerFactory = app.Services.GetService<ILoggerFactory>();
    var currentSimulator = SimulatorFactory.CreateCurrentSimulator(loggerFactory?.CreateLogger<TcpModbusSimulator>());
    var voltageSimulator = SimulatorFactory.CreateVoltageSimulator(loggerFactory?.CreateLogger<TcpModbusSimulator>());

    await currentSimulator.StartAsync();
    await voltageSimulator.StartAsync();

    Log.Information("Modbus simulators started:");
    Log.Information("  - Current simulator on port 5020");
    Log.Information("  - Voltage simulator on port 5021");

    // Run the application (handles device lifecycle automatically)
    Log.Information("Starting Power Aggregation application...");
    Log.Information("Press Ctrl+C to stop.");

    await app.RunAsync();

    // Cleanup simulators
    await currentSimulator.StopAsync();
    await voltageSimulator.StopAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Power Aggregation example terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

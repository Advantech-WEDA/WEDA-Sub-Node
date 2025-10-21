using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Weda.SubNode.Simulators.Modbus;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

var simulatorConfig = configuration.GetSection(TcpModbusSimulatorConfiguration.SectionName).Get<TcpModbusSimulatorConfiguration>()
    ?? throw new InvalidOperationException("Simulator configuration not found in appsettings.json");

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
var logger = loggerFactory.CreateLogger("ModbusSimulator");

try
{
    logger.LogInformation("╔════════════════════════════════════════════════════════╗");
    logger.LogInformation("║ Modbus TCP Simulator - Standalone Example             ║");
    logger.LogInformation("╚════════════════════════════════════════════════════════╝");
    logger.LogInformation("This simulator acts as a physical Modbus device:");
    logger.LogInformation("  - Simulating {SensorCount} sensors with realistic value changes", simulatorConfig.Sensors.Count);
    logger.LogInformation("");

    var simulator = new TcpModbusSimulator(
        simulatorConfig,
        logger: loggerFactory.CreateLogger<TcpModbusSimulator>());

    await simulator.StartAsync();

    logger.LogInformation("Simulator started successfully");
    logger.LogInformation("  Listening on: {IP}:{Port}",
        simulatorConfig.TcpConnection.IpAddress,
        simulatorConfig.TcpConnection.Port);
    logger.LogInformation("  Slave ID: {SlaveId}", simulatorConfig.ModbusProtocol.SlaveId);
    logger.LogInformation("");
    logger.LogInformation("Simulated Sensors:");
    foreach (var sensor in simulatorConfig.Sensors)
    {
        var unit = sensor.Unit ?? SensorDefaults.GetDefaultUnit(sensor.Type);
        logger.LogInformation("  - {Name} ({Type}): Address {Address}, {DataType}, Unit: {Unit}",
            sensor.Name,
            sensor.Type,
            sensor.StartAddress,
            sensor.DataType,
            unit);
    }
    logger.LogInformation("");
    logger.LogInformation("Simulator is running. You can connect any Modbus TCP client to read sensor values.");
    logger.LogInformation("Sensor values will gradually change based on simulation parameters.");
    logger.LogInformation("Update interval: {Interval} seconds",
        simulatorConfig.Simulation.GlobalUpdateIntervalSeconds ?? 10);
    logger.LogInformation("");
    logger.LogInformation("Press Ctrl+C to stop...");

    // Keep running until interrupted
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        logger.LogInformation("\nShutdown signal received...");
    };

    await Task.Delay(Timeout.Infinite, cts.Token);

    logger.LogInformation("Stopping simulator...");
    await simulator.StopAsync();
    logger.LogInformation("Simulator stopped successfully");
}
catch (OperationCanceledException)
{
    logger.LogInformation("Simulator stopped by user");
}
catch (Exception ex)
{
    logger.LogError(ex, "Error running simulator");
}
finally
{
    await Log.CloseAndFlushAsync();
}

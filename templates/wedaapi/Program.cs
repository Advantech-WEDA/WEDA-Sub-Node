using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;

// Create application with default configuration
// - Reads devices from appsettings.json (DeviceConfigs section)
// - Configures logging from Serilog section
var builder = WedaApplication.CreateDefaultBuilder(args);

// Use mock cloud service (no NATS connection required)
builder.UseMockCloud();

// Register Modbus simulator as hosted service (starts automatically with the app)
builder.Services.AddHostedService(sp =>
{
    var config = builder.Configuration.GetSection("TcpModbusSimulatorConfiguration")
        .Get<TcpModbusSimulatorConfiguration>()
        ?? throw new InvalidOperationException("TcpModbusSimulatorConfiguration not found in appsettings.json");

    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TcpModbusSimulator>>();

    return new TcpModbusSimulatorHostedService(config, logger);
});

// Build and run the application
var app = builder.Build();
await app.RunAsync();

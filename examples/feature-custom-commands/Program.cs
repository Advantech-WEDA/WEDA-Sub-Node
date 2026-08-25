using CommandHandlerExample;
using CommandHandlerExample.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;

// Cloud connection is configured in systemcfg.json (WedaNode section).
// For local development without a WedaNode, append .UseMockCloud().
var builder = WedaApplication.CreateDefaultBuilder(args);

// Register Modbus simulator as hosted service (starts automatically with the app)
builder.Services.AddHostedService(sp =>
{
    var config = builder.Configuration
        .GetSection(TcpModbusSimulatorConfiguration.SectionName)
        .Get<TcpModbusSimulatorConfiguration>()
        ?? throw new InvalidOperationException(
            $"Missing '{TcpModbusSimulatorConfiguration.SectionName}' configuration section in appsettings.json");

    var logger = sp.GetRequiredService<ILogger<TcpModbusSimulator>>();
    return new TcpModbusSimulatorHostedService(config, logger);
});

// Dispatch a demo "sensor.read" command through the command pipeline after startup.
// The SensorReadCommandHandler itself is auto-registered: CommandRegistry scans
// this assembly for ICommandHandler implementations at startup.
builder.Services.AddHostedService<CommandDemoService>();

// It will reference DeviceConfigs:MyFirstDevice in devicecfg.json
builder.AddDevice<MyFirstDevice>("MyFirstDevice");

// Build and run the application
var app = builder.Build();
await app.RunAsync();

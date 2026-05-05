using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;

using WedaBuilder;

var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();

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
builder.AddDevice<MyFirstDevice>("MyFirstDevice");

// Build and run the application
var app = builder.Build();
await app.RunAsync();

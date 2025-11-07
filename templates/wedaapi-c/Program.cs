using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;
using WedaApiC;

var builder = WedaApplication.CreateBuilder(args);

// Use Mock Cloud for testing (override default)
builder.UseMockCloud();

// Add telemetry and health reporting manually
builder.AddTelemetry();
builder.AddHealthReporting();

// Register device manually (no auto-scan)
var deviceConfig = ConfigureDeviceConfiguration();
builder.AddDevice<MyFirstDevice>(deviceConfig);

// Register Modbus simulator as hosted service (starts automatically with the app)
builder.Services.AddHostedService(sp =>
{
    var config = ConfigureTcpModbusSimulator();
    var logger = sp.GetRequiredService<ILogger<TcpModbusSimulator>>();
    return new TcpModbusSimulatorHostedService(config, logger);
});

// Build and run the application
var app = builder.Build();
await app.RunAsync();

static TcpModbusSimulatorConfiguration ConfigureTcpModbusSimulator()
{
    return new TcpModbusSimulatorConfiguration
    {
        TcpConnection = new TcpConnectionSettings
        {
            IpAddress = "127.0.0.1",
            Port = 5020
        },
        ModbusProtocol = new ModbusProtocolSettings
        {
            SlaveId = 1,
            UseModbusAddressing = false,
            HoldingRegisterBase = 0
        },
        Simulation = new SimulationSettings
        {
            GlobalUpdateIntervalSeconds = 5,
            EnableValueChanges = true
        },
        Sensors =
        [
            new()
            {
                Name = "TemperatureSensor",
                Type = SensorType.Temperature,
                StartAddress = 0,
                RegisterCount = 2,
                DataType = SimulatedDataType.Float32,
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 18.0,
                    MaxValue = 32.0,
                    InitialValue = 25.0,
                    ChangeRate = 0.2,
                    NoiseLevel = 0.1
                }
            }
        ]
    };
}

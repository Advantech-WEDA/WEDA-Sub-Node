using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;
using testdevice;
using Weda.SubNode.WebApi;

var builder = WedaApplication.CreateDefaultBuilder(args);

builder.AddWebApi();
builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");

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
            GlobalUpdateIntervalSeconds = 2,
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

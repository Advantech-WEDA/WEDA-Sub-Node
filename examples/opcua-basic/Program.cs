using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;
using Weda.SubNode.Simulators.OpcUa;
using Weda.SubNode.WebApi;
using opcua_device;

var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();

builder.AddWebApi();
builder.AddDevice<MyOpcUaDevice>("MyOpcUaDeviceConfig");

// Register OPC-UA simulator as hosted service (starts automatically with the app)
builder.Services.AddHostedService(sp =>
{
    var config = ConfigureOpcUaSimulator();
    var logger = sp.GetRequiredService<ILogger<OpcUaSimulator>>();
    return new OpcUaSimulatorHostedService(config, logger);
});

// Build and run the application
var app = builder.Build();
await app.RunAsync();

static OpcUaSimulatorConfiguration ConfigureOpcUaSimulator()
{
    return new OpcUaSimulatorConfiguration
    {
        Server = new OpcUaServerSettings
        {
            Port = 4840,
            ServerName = "WedaOpcUaSimulator"
        },
        Simulation = new SimulationSettings
        {
            GlobalUpdateIntervalSeconds = 2,
            EnableValueChanges = true
        },
        Nodes =
        [
            new()
            {
                Name = "TemperatureSensor",
                NodeId = "Temperature",
                Type = SensorType.Temperature,
                DataType = SimulatedDataType.Float32,
                Unit = "celsius",
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 18.0,
                    MaxValue = 32.0,
                    InitialValue = 25.0,
                    ChangeRate = 0.2,
                    NoiseLevel = 0.1
                }
            },
            new()
            {
                Name = "HumiditySensor",
                NodeId = "Humidity",
                Type = SensorType.Humidity,
                DataType = SimulatedDataType.Float32,
                Unit = "percent",
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 30.0,
                    MaxValue = 80.0,
                    InitialValue = 55.0,
                    ChangeRate = 0.5,
                    NoiseLevel = 0.2
                }
            },
            new()
            {
                Name = "PressureSensor",
                NodeId = "Pressure",
                Type = SensorType.Pressure,
                DataType = SimulatedDataType.Float32,
                Unit = "hPa",
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 980.0,
                    MaxValue = 1030.0,
                    InitialValue = 1013.0,
                    ChangeRate = 0.1,
                    NoiseLevel = 0.05
                }
            }
        ]
    };
}

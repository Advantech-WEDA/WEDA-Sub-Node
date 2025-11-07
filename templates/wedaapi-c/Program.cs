using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;
using WedaApiC;

var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()
    .AddTelemetry()
    .AddHealthReporting()
    .AddCommands()
    .AddConfigUpdates()
    .UseMockCloud();

// Register custom device manually (full control pattern)
var deviceConfig = ConfigureTcpModbusDevice();
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

static TcpModbusDeviceConfiguration ConfigureTcpModbusDevice()
{
    // Configure Device using TcpModbusDeviceConfiguration
    var modbusDeviceConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "WedaApiC",
        Manufacturer = "Advantech",
        Model = "CustomDevice-v1",
        Host = "127.0.0.1",
        Port = 5020,
        SlaveId = 1
    };

    // Create temperature sensor
    var tempSensor = new ModbusSensorConfiguration
    {
        Name = "temperature.sensor",
        Dtmi = "dtmi:advantech:EdgeSync:Temperature;1",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };

    // Add sensor to device
    modbusDeviceConfig.AddSensor(tempSensor);

    // Return TcpModbusDeviceConfiguration directly (no need to call ToDeviceConfiguration)
    return modbusDeviceConfig;
}

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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Core.Transforms;
using Weda.SubNode.Simulators.Modbus;
using WedaSubNode;

// Configuration (only for Serilog)
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);

try
{
    // Configure Modbus Simulator programmatically
    var simulator = await ConfigureTcpModbusSimulator();

    // Configure Device programmatically
    var deviceConfig = ConfigureDeviceConfiguration();

    // create a WedaApplicationContext with system configuration
    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;

        // remove thie line to enable real cloud service
        options.CloudService = WedaFactory.Cloud.Mock; 
    });

    var device = new MyFirstDevice(context, deviceConfig);

    if (!await device.InitializeAsync())
    {
        Log.Error("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Log.Information("MyFirstDevice started. Press Ctrl+C to stop...");

    // Wait for cancellation
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    await Task.Delay(Timeout.Infinite, cts.Token);

    // Graceful shutdown
    await device.StopAsync();
    device.Dispose();
    await simulator.StopAsync();
}
catch (OperationCanceledException)
{
    Log.Information("Application stopped");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

DeviceConfiguration ConfigureDeviceConfiguration()
{
    // Configure Device using TcpModbusDeviceConfiguration
    var modbusDeviceConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",
        Manufacturer = "Advantech",
        Model = "CustomDevice-v1",
        Host = "127.0.0.1",
        Port = 5020,
        SlaveId = 1
    };

    // Create temperature sensor with transform pipeline
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

    // Configure transform pipeline (execution order: 0 -> 1)
    tempSensor
        .AddTransform(new CalibrationTransform(scale: 0.1, offset: -40));  // [0] Calibration: raw * 0.1 - 40

    // Add sensor to device
    modbusDeviceConfig.AddSensor(tempSensor);

    // Convert to DeviceConfiguration
    return modbusDeviceConfig.ToDeviceConfiguration();
}

async Task<TcpModbusSimulator> ConfigureTcpModbusSimulator()
{
    var simulatorConfig = new TcpModbusSimulatorConfiguration
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
        Sensors = new List<SimulatedSensor>
        {
            new SimulatedSensor
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
        }
    };

    var simulator = new TcpModbusSimulator(simulatorConfig, logger: loggerFactory.CreateLogger<TcpModbusSimulator>());
    await simulator.StartAsync();
    return simulator;
}
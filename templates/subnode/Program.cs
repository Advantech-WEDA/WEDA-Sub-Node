using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Simulators.Modbus;
using WedaSubNode;

try
{
    // create a WedaApplicationContext with system configuration
    // SDK will automatically load appsettings.json for Configuration and LoggerFactory
    using var context = new WedaApplicationContext(options =>
    {
        // remove this line to enable real cloud service
        options.CloudService = WedaFactory.Cloud.Mock;
    });

    // Configure Modbus Simulator programmatically
    var simulator = await ConfigureTcpModbusSimulator(context);

    // Configure Device programmatically
    var deviceConfig = ConfigureDeviceConfiguration();

    var device = new MyFirstDevice(context, deviceConfig);

    if (!await device.InitializeAsync())
    {
        Console.WriteLine("Failed to initialize device");
        return;
    }

    await device.StartAsync();
    Console.WriteLine("MyFirstDevice started. Press Ctrl+C to stop...");

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
    Console.WriteLine("Application stopped");
}
catch (Exception ex)
{
    Console.WriteLine($"Application terminated unexpectedly: {ex}");
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
        SlaveId = 1,
        DtdlPath = "assets/dtdl/dtmi/advantech/edgesync/sample-1.json"
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
    tempSensor.Config.Interval = 5000;

    // Add sensor to device
    modbusDeviceConfig.AddSensor(tempSensor);

    // Convert to DeviceConfiguration
    var deviceConfig = modbusDeviceConfig.ToDeviceConfiguration();

    // Load DTDL metadata (required for cloud registration)
    deviceConfig.InitializeDtdl();

    return deviceConfig;
}

async Task<TcpModbusSimulator> ConfigureTcpModbusSimulator(WedaApplicationContext context)
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

    var logger = context.LoggerFactory.CreateLogger<TcpModbusSimulator>();
    var simulator = new TcpModbusSimulator(simulatorConfig, logger: logger);
    await simulator.StartAsync();
    return simulator;
}
# SubNode Template

A custom device pattern template that demonstrates how to inherit from `TcpModbusDevice` for advanced customization.

## Features

- ✅ **Custom Device Pattern**: Full control over device behavior by inheriting from TcpModbusDevice
- ✅ **Automatic Communication Setup**: Communication layer automatically created from configuration
- ✅ **Uses MockCloudService**: No cloud connection required for testing
- ✅ **Clean Architecture**: Separation of device logic and infrastructure
- ✅ **Extensible**: Easy to add custom methods, properties, and behaviors

## When to Use This Template

Use the SubNode template when you need:
- Custom device initialization logic
- Advanced communication handling
- Multiple device types in one application
- Device-specific methods and properties
- Fine-grained control over the device lifecycle

For simpler use cases, consider `wedaapi` or `wedaapi-c` templates instead.

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Docker (optional, for Modbus simulator)

### Step 1: Start Modbus Simulator (Optional)

If you don't have a real Modbus device, you can use the ModbusSimulator:

```bash
# From repository root
cd examples/ModbusSimulatorExample
docker-compose up -d

# Or run directly without Docker
dotnet run --project examples/ModbusSimulatorExample/ModbusSimulatorExample.csproj
```

The simulator will start on `127.0.0.1:502` (as configured in appsettings.json).

### Step 2: Run Your Device

```bash
dotnet restore
dotnet build
dotnet run
```

You should see output like:

```
[12:34:56 INF] Starting MyFirstDevice application...
[12:34:56 INF] ✅ Application context created with MockCloudService
[12:34:57 INF] Device started successfully. Press Ctrl+C to stop...
[12:34:58 INF] temperature.sensor: 25.3
```

### Step 3: Customize Your Device

Edit `MyFirstDevice.cs` to add custom device logic:

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    // Add custom properties
    public int CustomCounter { get; private set; }

    public MyFirstDevice(...)
        : base(context, configuration)
    {
        DataReceived += OnDataReceived;
    }

    // Add custom methods
    public async Task DoCustomAction()
    {
        // Your custom logic
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Process sensor data
        CustomCounter++;
    }
}
```

## Architecture

### Inheritance Hierarchy

```
DeviceBase (abstract)
  └─ TcpModbusDevice
       └─ MyFirstDevice (your custom device)
```

### Key Benefits

1. **Automatic Protocol Handling**: Modbus TCP communication is handled by base class
2. **Event-Driven**: Subscribe to events (DataReceived, ConnectionStateChanged, etc.)
3. **Lifecycle Management**: Initialize, Start, Stop methods provided
4. **Configuration-Driven**: Device behavior controlled via appsettings.json

## Configuration

### Device Configuration (appsettings.json)

The device configuration is in `DeviceConfigs:MyFirstDevice` section:

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "DeviceName": "MySubNode",
      "DeviceType": "modbusEthernet",
      "Communication": {
        "Host": "127.0.0.1",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [
        {
          "ResourceId": "temp-001",
          "Name": "temperature.sensor",
          "SensorGroup": "TEMP",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          }
        }
      ]
    }
  }
}
```

### Connecting to Real Modbus Device

Update the `Communication` section:

```json
"Communication": {
  "Host": "192.168.1.100",  // Your device IP
  "Port": 502,               // Standard Modbus port
  "SlaveId": 1               // Modbus slave ID
}
```

## Advanced Customization

### Adding Custom Initialization

```csharp
public override async Task<bool> InitializeAsync()
{
    // Call base initialization first
    if (!await base.InitializeAsync())
        return false;

    // Add your custom initialization
    _logger.LogInformation("Performing custom initialization...");

    // TODO: Load additional resources, validate configuration, etc.

    return true;
}
```

### Overriding Data Processing

```csharp
protected override async Task ProcessDataAsync(List<MeasureData> data)
{
    // Add pre-processing
    _logger.LogDebug("Processing {Count} measurements", data.Count);

    // Call base processing
    await base.ProcessDataAsync(data);

    // Add post-processing
    // TODO: Additional processing, validation, transformation
}
```

### Adding Device-Specific Commands

```csharp
public async Task<bool> SetDeviceParameterAsync(string parameter, object value)
{
    _logger.LogInformation("Setting {Parameter} to {Value}", parameter, value);

    // TODO: Implement custom command logic
    // Example: Write to Modbus holding register

    return true;
}
```

## Project Structure

```
.
├── Program.cs              # Entry point with manual device instantiation
├── MyFirstDevice.cs        # Custom device inheriting from TcpModbusDevice
├── appsettings.json        # Configuration
└── README.md              # This file
```

## Use Cases

This template is ideal for:

1. **Custom Device Drivers**: Implementing device-specific protocols
2. **Advanced Control Logic**: Complex initialization and control sequences
3. **Multiple Device Types**: Managing different device models
4. **Custom Communication**: Extending or modifying Modbus behavior
5. **Hardware Integration**: Direct hardware access and control

## Comparison with Other Templates

| Feature | wedaapi | wedaapi-c | subnode |
|---------|---------|-----------|---------|
| Complexity | Simple | Medium | Advanced |
| Event Handling | Basic | Full | Customizable |
| Business Rules | Manual | Built-in | Custom |
| Extensibility | Limited | Medium | Full |
| Use Case | Quick start | Production | Custom devices |

## Next Steps

1. **Add Custom Methods**: Implement device-specific operations
2. **Override Lifecycle**: Customize initialization, startup, shutdown
3. **Extend Communication**: Add custom Modbus functions or protocols
4. **Add State Management**: Track custom device states
5. **Connect to Cloud**: Replace MockCloudService with real cloud service

## Learn More

- [Weda.SubNode Documentation](../../docs/)
- [Custom Device Development Guide](../../docs/guides/custom-devices.md)
- [TcpModbusDevice Source](../../src/Weda.SubNode.Devices/Generic/TcpModbusDevice.cs)
- [DeviceBase API Reference](../../src/Weda.SubNode.Core/Devices/DeviceBase.cs)

## Troubleshooting

### Cannot connect to Modbus device

- Check if ModbusSimulator is running: `docker ps | grep modbus-simulator`
- Verify Host, Port, and SlaveId in appsettings.json
- Check firewall settings
- Enable Debug logging for detailed connection logs

### No sensor data received

- Verify sensor Parameters match your Modbus device
- Check RegisterAddress, RegisterCount, and DataType
- Use ModbusScanner to discover device registers
- Enable Debug logging in Serilog configuration

### Device initialization fails

- Check InitializeAsync override doesn't block
- Verify all required resources are available
- Check for exceptions in logs
- Test base.InitializeAsync() separately

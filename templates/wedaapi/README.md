# WedaApi Template

A simple Modbus TCP device template demonstrating basic device implementation with MyFirstDevice.

## Features

- ✅ Simple and clean code structure
- ✅ Inherits from `TcpModbusDevice` for automatic Modbus TCP support
- ✅ Uses `MockCloudService` - no cloud connection required
- ✅ Real-time sensor data logging with formatted output
- ✅ Ready-to-use Modbus TCP configuration

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

The simulator will start on `127.0.0.1:5020` (as configured in appsettings.json).

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
[12:34:56 INF] ✅ MyFirstDevice started successfully!
[12:34:56 INF]    Connecting to Modbus device at 127.0.0.1:5020
[12:34:58 INF] 📊 Temperature Sensor: 25.30 °C
```

### Step 3: Customize Your Device

Edit `MyFirstDevice.cs` to add your custom business logic:

```csharp
private void OnDataReceived(object? sender, DataReceivedEvent e)
{
    // Add your custom logic here
    // - Apply business rules
    // - Trigger alerts
    // - Store data to database
    // - Forward to other systems
}
```

## Configuration

### Device Configuration (appsettings.json)

The device configuration is in `DeviceConfigs:MyFirstDevice` section:

- **Host**: Modbus TCP server address (default: `127.0.0.1`)
- **Port**: Modbus TCP port (default: `5020`)
- **Sensors**: Array of sensor configurations with register addresses

### Connecting to Real Modbus Device

Update the `Communication` section in `appsettings.json`:

```json
"Communication": {
  "Host": "192.168.1.100",  // Your device IP
  "Port": 502,               // Standard Modbus port
  "ProtocolType": "modbus"
}
```

## Project Structure

```
.
├── Program.cs              # Entry point with device initialization
├── MyFirstDevice.cs        # Your custom device implementation
├── appsettings.json        # Configuration
└── README.md              # This file
```

## Next Steps

1. **Add More Sensors**: Edit `appsettings.json` to add more sensor configurations
2. **Add Business Logic**: Implement threshold monitoring, alerts, etc. in `MyFirstDevice.cs`
3. **Connect to Cloud**: Replace `MockCloudService` with real cloud service for production
4. **Add Persistence**: Store data to database or time-series storage

## Learn More

- [Weda.SubNode Documentation](../../docs/)
- [Modbus Protocol Guide](../../docs/protocols/modbus.md)
- [TcpModbusDevice API Reference](../../src/Weda.SubNode.Devices/)

## Troubleshooting

### Cannot connect to Modbus device

- Check if ModbusSimulator is running: `docker ps | grep modbus-simulator`
- Verify Host and Port in appsettings.json
- Check firewall settings

### No sensor data received

- Verify sensor register addresses match your Modbus device
- Check DataType matches the register format
- Enable Debug logging in Serilog configuration

# WedaApi-C Template (Advanced Control Pattern)

An advanced Modbus TCP device template with comprehensive event handling, business rules, and monitoring capabilities.

## Features

- ✅ **Full Event Observability**: Subscribes to all device lifecycle events
  - DataReceived
  - TelemetrySent
  - ConnectionStateChanged
  - DeviceStatusChanged
- ✅ **Business Rules Engine**: Apply custom logic and threshold monitoring
- ✅ **Advanced Logging**: Detailed visual logs with structured information
- ✅ **Production-Ready**: Comprehensive error handling and state tracking
- ✅ **Uses MockCloudService**: No cloud connection required for testing

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

You should see detailed output like:

```
╔════════════════════════════════════════════════════════╗
║ WedaApi-C: Advanced Control Pattern Example           ║
╚════════════════════════════════════════════════════════╝
[12:34:56 INF] ✅ Application context created with MockCloudService
[12:34:56 INF] 🔌 Connection state changed: Connecting
[12:34:57 INF] 🔌 Connection state changed: Connected
[12:34:57 INF] 📊 Status changed: Running
[12:34:58 INF] 📊 Temperature Sensor: 25.30 °C
[12:34:58 INF] 📤 Telemetry batch sent: 1 sensors
```

### Step 3: Customize Business Rules

Edit `MyFirstDevice.cs` to add your custom business rules:

```csharp
private void ApplyBusinessRules(string sensorName, object value, SensorGroup sensorGroup)
{
    // Example: Temperature threshold alert
    if (sensorGroup == SensorGroup.TEMP && value is double temperature)
    {
        if (temperature > 30.0)
        {
            _logger.LogWarning("⚠️  High temperature detected: {Temp}°C from {Sensor}",
                temperature, sensorName);
            // TODO: Send alert, trigger action, etc.
        }
    }

    // Add your custom rules here
}
```

## Architecture

### Event-Driven Design

This template demonstrates a comprehensive event-driven architecture:

```csharp
public MyFirstDevice(...)
{
    // Subscribe to all device events for full observability
    DataReceived += OnDataReceived;
    TelemetrySent += OnTelemetrySent;
    ConnectionStateChanged += OnConnectionStateChanged;
    DeviceStatusChanged += OnDeviceStatusChanged;
}
```

### Event Handlers

1. **OnDataReceived**: Process raw sensor data, apply business rules
2. **OnTelemetrySent**: Track telemetry transmission metrics
3. **OnConnectionStateChanged**: Monitor device connectivity
4. **OnDeviceStatusChanged**: Track device operational status

## Configuration

### Device Configuration (appsettings.json)

The device configuration is in `DeviceConfigs:MyFirstDevice` section:

- **Host**: Modbus TCP server address (default: `127.0.0.1`)
- **Port**: Modbus TCP port (default: `502`)
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

## Advanced Features

### Business Rules

The template includes example business rules for:
- Temperature threshold monitoring (high/low alerts)
- Configurable thresholds
- Extensible rule engine pattern

### Metrics Tracking

- Data reception counter
- Telemetry transmission tracking
- Connection state monitoring
- Device status tracking

### Visual Logging

- Structured logs with emojis for easy visual parsing
- Sensor data formatted with appropriate units
- Connection state changes highlighted
- Status transitions tracked

## Project Structure

```
.
├── Program.cs              # Entry point with banner and detailed logging
├── MyFirstDevice.cs        # Advanced device with full event handling
├── appsettings.json        # Configuration
└── README.md              # This file
```

## Use Cases

This template is ideal for:

1. **Production Systems**: Full observability and error handling
2. **Critical Applications**: Comprehensive state tracking
3. **Complex Business Logic**: Threshold monitoring, alerts, actions
4. **System Integration**: Multiple event hooks for external systems
5. **Monitoring & Analytics**: Detailed metrics and telemetry tracking

## Next Steps

1. **Implement Business Rules**: Add threshold monitoring, validation, transformations
2. **Add Alert System**: Send notifications on threshold violations
3. **Connect to Database**: Store metrics and historical data
4. **Integrate External Systems**: Use event handlers to trigger external actions
5. **Add Cloud Connection**: Replace MockCloudService with real cloud service

## Learn More

- [Weda.SubNode Documentation](../../docs/)
- [Event-Driven Architecture Guide](../../docs/architecture/events.md)
- [Business Rules Pattern](../../docs/patterns/business-rules.md)
- [TcpModbusDevice API Reference](../../src/Weda.SubNode.Devices/)

## Troubleshooting

### Cannot connect to Modbus device

- Check if ModbusSimulator is running: `docker ps | grep modbus-simulator`
- Verify Host and Port in appsettings.json
- Check ConnectionStateChanged events for connection errors
- Check firewall settings

### No sensor data received

- Monitor DataReceived event logs
- Verify sensor register addresses match your Modbus device
- Check DataType matches the register format
- Enable Debug logging in Serilog configuration

### Business rules not triggering

- Verify SensorGroup is correctly set in appsettings.json
- Check threshold values in ApplyBusinessRules
- Enable Debug logging to see all data values

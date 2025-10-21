# Modbus TCP Simulator - Standalone Example

This example demonstrates a **standalone Modbus TCP device simulator** with no dependencies on the Weda SubNode SDK.

## Purpose

The simulator acts as a **physical Modbus device** that:
- Speaks standard Modbus TCP protocol
- Has NO knowledge of SubNode concepts (deviceId, resourceId, etc.)
- Simulates realistic sensor value changes over time
- Can be used to test any Modbus TCP client

## Architecture

```
┌─────────────────────┐
│  TcpModbusSimulator │  <- Pure Modbus TCP Device
│                     │     (No SubNode dependency)
│  - Simulated Sensors│
│  - Modbus Protocol  │
│  - TCP Server       │
└──────────┬──────────┘
           │ TCP 127.0.0.1:5020
           │ Standard Modbus Protocol
           │
    Any Modbus Client can connect
    (including Weda SubNode)
```

## Running the Simulator

```bash
cd examples/ModbusSimulatorExample
dotnet run
```

The simulator will:
1. Start a TCP server on `127.0.0.1:5020`
2. Listen for Modbus TCP connections
3. Simulate 5 sensors with gradually changing values
4. Run continuously until Ctrl+C

## Simulated Sensors

| Sensor | Type | Address | Data Type | Range |
|--------|------|---------|-----------|-------|
| TemperatureSensor | Temperature | 40001 | Float32 | 18-32°C |
| HumiditySensor | Humidity | 40003 | Float32 | 30-80% |
| PressureSensor | Pressure | 40005 | Float32 | 980-1030 hPa |
| VoltageSensor | Voltage | 40007 | Float32 | 220-240V |
| CurrentSensor | Current | 40009 | Float32 | 0-10A |

## Testing with Modbus Client

You can connect any Modbus TCP client to test:

```bash
# Using modbus-cli (if installed)
modbus read 127.0.0.1:5020 1 holding-register 40001 2

# Using Python pymodbus
from pymodbus.client import ModbusTcpClient
client = ModbusTcpClient('127.0.0.1', port=5020)
result = client.read_holding_registers(0, 2, slave=1)
```

## Configuration

Edit [appsettings.json](appsettings.json) to customize:

### TCP Connection
```json
{
  "TcpConnection": {
    "IpAddress": "127.0.0.1",
    "Port": 5020
  }
}
```

### Modbus Protocol
```json
{
  "ModbusProtocol": {
    "SlaveId": 1,
    "UseModbusAddressing": true,
    "HoldingRegisterBase": 40001
  }
}
```

### Global Simulation Settings
Control update intervals for all sensors at once:

```json
{
  "Simulation": {
    "GlobalUpdateIntervalSeconds": 5,
    "EnableValueChanges": true
  }
}
```

**Options:**
- `GlobalUpdateIntervalSeconds` (ushort, 1-65535):
  - If set (e.g., `5`): All sensors update every N seconds (overrides individual settings)
  - If `null`: Each sensor uses its own `UpdateIntervalSeconds` from defaults (10 seconds)
  - **Safety**: Limited to ushort range to prevent excessive update rates
- `EnableValueChanges` (bool):
  - `true` (default): Sensors simulate realistic value changes
  - `false`: Sensors maintain their initial values (useful for testing static values)

**Examples:**

```json
// All sensors update every 3 seconds
{
  "Simulation": {
    "GlobalUpdateIntervalSeconds": 3,
    "EnableValueChanges": true
  }
}
```

```json
// Static values (no simulation), individual sensor intervals
{
  "Simulation": {
    "GlobalUpdateIntervalSeconds": null,
    "EnableValueChanges": false
  }
}
```

### Individual Sensor Configuration
Each sensor can have its own simulation parameters:

```json
{
  "Sensors": [
    {
      "Name": "TemperatureSensor",
      "Type": "Temperature",
      "StartAddress": 40001,
      "RegisterCount": 2,
      "DataType": "Float32",
      "SimulationParams": {
        "MinValue": 18.0,
        "MaxValue": 32.0,
        "InitialValue": 25.0,
        "ChangeRate": 0.2,
        "NoiseLevel": 0.1
      }
    }
  ]
}
```

**Sensor Parameters:**
- `MinValue` / `MaxValue`: Value range for simulation
- `InitialValue`: Starting value (if null, random between min/max)
- `ChangeRate`: How much value can change per update
- `NoiseLevel`: Random noise added to each update
- ~~`UpdateIntervalSeconds`~~: Removed - now controlled by `GlobalUpdateIntervalSeconds`

**Note:** If you don't specify `SimulationParams`, the simulator uses sensible defaults based on sensor `Type`.

## Separation from SubNode

This simulator is **completely independent** from the Weda SubNode SDK:
- No dependency on `Weda.SubNode.Core`
- No device registration or cloud connection
- No deviceId or resourceId concepts
- Pure protocol implementation

For an example of connecting a SubNode to this simulator, see the separate DeviceExample project.

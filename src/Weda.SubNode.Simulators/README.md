# Weda.SubNode.Simulators

Test infrastructure for the Weda SubNode SDK providing realistic device simulators.

## TcpModbusSimulator

Full Modbus TCP server implementation with realistic sensor value simulation.

### Features

- Modbus TCP Protocol (Function Codes: 03, 06, 10)
- Standard Modbus addressing (40001-49999 for holding registers)
- Configurable via appsettings.json
- Realistic sensor simulation with gradual value changes
- Multiple data types: UInt16, Int16, Float32, UInt32, Int32, Float64, UInt64, Int64
- Predefined sensor types: Temperature, Humidity, Pressure, Voltage, Current
- Configurable simulation parameters per sensor

### Configuration Example

```json
{
  "Simulator": {
    "TcpConnection": {
      "IpAddress": "127.0.0.1",
      "Port": 5020
    },
    "ModbusProtocol": {
      "SlaveId": 1,
      "UseModbusAddressing": true,
      "HoldingRegisterBase": 40001
    },
    "Sensors": [
      {
        "Name": "Temperature",
        "Type": "Temperature",
        "StartAddress": 40001,
        "RegisterCount": 2,
        "DataType": "Float32",
        "SimulationParams": {
          "MinValue": 18.0,
          "MaxValue": 32.0,
          "InitialValue": 25.0,
          "ChangeRate": 0.2,
          "UpdateIntervalMs": 2000,
          "NoiseLevel": 0.1
        }
      }
    ]
  }
}
```

### Sensor Types

Each sensor type has realistic default parameters:

- **Temperature**: 18-32C, slow changes (+/-0.2C/update), low noise (+/-0.1C)
- **Humidity**: 30-80%, medium changes (+/-0.5%/update), medium noise (+/-0.2%)
- **Pressure**: 980-1030 hPa, very slow changes (+/-0.1hPa/update), very low noise (+/-0.05hPa)
- **Voltage**: 220-240V, small changes (+/-0.5V/update), medium noise (+/-0.3V)
- **Current**: 0-10A, fast changes (+/-1.0A/update), medium noise (+/-0.2A)
- **Custom**: Use SimulationParams to define your own behavior

### Usage

```csharp
using Microsoft.Extensions.Configuration;
using Weda.SubNode.Simulators.Modbus;

// Load configuration
var config = configuration.GetSection("Simulator")
    .Get<TcpModbusSimulatorConfiguration>();

// Create and start simulator
var simulator = new TcpModbusSimulator(config, logger);
await simulator.StartAsync();

// Simulator will automatically update sensor values based on configuration

// Stop simulator
await simulator.StopAsync();
```

### Modbus Addressing

When `UseModbusAddressing` is true:
- Address 40001 maps to holding register 0
- Address 40002 maps to holding register 1
- etc.

When false:
- Addresses are used directly as register numbers

### Simulation Algorithm

The simulator uses a bounded random walk algorithm:

1. Start with initial value (or random within min/max)
2. Each update interval:
   - Generate random direction: -1 to +1
   - Calculate change: direction * changeRate
   - Add noise: +/- noiseLevel
   - Apply change: newValue = currentValue + change + noise
   - Clamp to bounds: Math.Clamp(newValue, minValue, maxValue)

This produces realistic, gradual value changes that stay within configured bounds.

### Data Type Conversion

Float32 values are encoded in big-endian byte order to Modbus registers:
- Float32 (4 bytes) -> 2 registers
- Float64 (8 bytes) -> 4 registers
- UInt32/Int32 (4 bytes) -> 2 registers
- UInt64/Int64 (8 bytes) -> 4 registers

### Example Output

```
Temperature: 25.00 -> 24.96 -> 25.06 -> 24.92 -> 25.17 -> 25.35 -> 25.24 -> 25.14
Humidity:    55.00 -> 55.29 -> 55.06 -> 54.76 -> 54.83 -> 54.35 -> 53.94 -> 53.60
Pressure:    1013.25 -> 1013.13 -> 1013.14 -> 1013.21 (very slow)
Current:     5.00 -> 5.75 -> 5.03 -> 6.48 -> 5.90 -> 2.31 -> 0.08 (fast changes)
```

## See Also

- [ModbusSimulatorExample](../../examples/ModbusSimulatorExample/) - Complete usage example
- [Weda.SubNode.Core](../Weda.SubNode.Core/) - Core framework implementation

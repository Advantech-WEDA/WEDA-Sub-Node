# Quick Start: Simple API Mode (wedaapi)

**Template**: `dotnet new wedaapi`
**Time to complete**: 5 minutes
**Difficulty**: * Beginner

## What You'll Build

A minimal Modbus device application that:
- Reads sensor data automatically
- Sends telemetry to cloud via NATS
- Zero custom code required
- Everything configured in JSON

**Perfect for**: Beginners, quick prototypes, standard data collection scenarios

---

## Step 1: Create Project

Create a new project using the `wedaapi` template:

```bash
# Create project
dotnet new wedaapi -n MySimpleDevice
cd MySimpleDevice

# Project structure
MySimpleDevice/
├── Program.cs           # 3 lines of code!
├── appsettings.json     # All configuration here
└── MySimpleDevice.csproj
```

---

## Step 2: Review the Code

Open `Program.cs` - notice how simple it is:

```csharp
using Weda.SubNode.Host;

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
```

**That's it!** Just 3 lines of code. Everything else is configured in `appsettings.json`.

---

## Step 3: Configure Your Device

Edit `appsettings.json` to set up your Modbus device and sensors:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Weda.SubNode": "Debug"
    }
  },
  "DeviceConfigs": {
    "TempSensor": {                         // User-defined config name
      "Enabled": true,                      // true to enable auto-discovery, false to ignore auto-discovery.
      "DeviceName": "Temperature Sensor",         // User-defined device name
      "DeviceType": "AdamEthernet",         // An enum mapping to built-in implementation of protocols.
      "ConnectionSettings": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1
      },
      "PollingInterval": 1000,
      "Sensors": [
        {
          "ResourceId": "temp",
          "Name": "Temperature",
          "RegisterAddress": 0,
          "RegisterType": "HoldingRegister",
          "DataType": "Float32",
          "Config": {
            "Enabled": true,
            "Interval": 1000
          }
        },
        {
          "ResourceId": "humidity",
          "Name": "Humidity",
          "RegisterAddress": 2,
          "RegisterType": "HoldingRegister",
          "DataType": "Float32",
          "Config": {
            "Enabled": true,
            "Interval": 1000
          }
        }
      ]
    }
  },
  "Nats": {
    "Url": "nats://localhost:4222",
    "CredFile": "",
    "Name": "default",
    "SerializerType": "json"
  }
}
```

### Configuration Explained

#### Device Settings
- **DeviceConfigs**: Dictionary of device configurations (key = device name)
- **Id**: Unique identifier for this device
- **DeviceType**: Protocol type (`ModbusTCP`, `ModbusRTU`, etc.)
- **ConnectionSettings**: How to connect to the device
  - **Host**: IP address of Modbus device
  - **Port**: Modbus TCP port (usually 502)
  - **SlaveId**: Modbus slave/unit ID (1-247)
- **PollingInterval**: How often to read data (milliseconds)

#### Sensor Configuration
- **ResourceId**: Unique sensor identifier (used in cloud)
- **Name**: Human-readable sensor name
- **RegisterAddress**: Modbus register number
- **RegisterType**: `HoldingRegister`, `InputRegister`, `Coil`, or `DiscreteInput`
- **DataType**: `Int16`, `UInt16`, `Int32`, `UInt32`, `Float32`, `Float64`, etc.

#### Cloud Integration
- **Nats.Url**: NATS server connection string
- **Nats.Enabled**: Set to `false` for offline testing

---

## Step 4: Run Your Application

### Build and Run

```bash
dotnet build
dotnet run
```

### Expected Output

```
[12:34:56 INF] Starting Weda SubNode application...
[12:34:56 INF] NATS URL configured: nats://localhost:4222
[12:34:56 INF] Initializing device: TempSensor (temp-sensor-001)
[12:34:56 INF] Device started successfully. Press Ctrl+C to stop...
[12:34:57 DBG] Connected to Modbus device at 192.168.1.100:502
[12:34:57 DBG] Reading sensor: temp (Register: 0)
[12:34:57 DBG] Reading sensor: humidity (Register: 2)
[12:34:57 INF] [DATA] Temperature: 25.5°C
[12:34:57 INF] [DATA] Humidity: 65.0%
[12:34:58 INF] [DATA] Temperature: 25.6°C
[12:34:58 INF] [DATA] Humidity: 65.2%
```

---

## Step 5: Test Without Hardware (Optional)

If you don't have a physical Modbus device, use sandbox mode:

### Edit appsettings.json

```json
{
  "DeviceConfigs": {
    "TempSensor": {
      "ConnectionSettings": {
        "Host": "127.0.0.1",  // Localhost
        "Port": 502
      },
      "Sandbox": {
        "Enabled": true,      // Enable simulator
        "RandomSeed": 42      // Reproducible random values
      }
    }
  }
}
```

### Start Modbus Simulator

```bash
# Option 1: Use built-in example
cd examples/ModbusSimulatorExample
dotnet run

# Option 2: Use external simulator
# Install ModbusPal or pyModSlave
```

See **[02. Sandbox Testing](02_sandbox_testing.md)** for detailed simulator setup.

---

## Step 6: Add Data Processing (Optional)

You can add transformations and filters directly in `appsettings.json`:

### Example: Calibration + Unit Conversion

```json
{
  "Sensors": [
    {
      "ResourceId": "temp",
      "Name": "Temperature",
      "RegisterAddress": 0,
      "DataType": "Float32",
      "Config": {
        "Enabled": true,
        "Interval": 1000,
        "TransformPipeline": [
          {
            "Type": "Calibration",
            "Enabled": true,
            "Order": 0,
            "Parameters": {
              "Scale": 1.05,
              "Offset": -2.0
            }
          },
          {
            "Type": "UnitConversion",
            "Enabled": true,
            "Order": 1,
            "Parameters": {
              "FromUnit": "C",
              "ToUnit": "F"
            }
          }
        ]
      }
    }
  ]
}
```

### Example: Kalman Filter for Noise Reduction

```json
{
  "Sensors": [
    {
      "ResourceId": "vibration",
      "Name": "Vibration",
      "RegisterAddress": 10,
      "DataType": "Float32",
      "Config": {
        "Enabled": true,
        "DspPipeline": [
          {
            "Type": "Kalman",
            "Enabled": true,
            "Order": 0,
            "Parameters": {
              "ProcessNoise": 0.01,
              "MeasurementNoise": 0.1
            }
          }
        ]
      }
    }
  ]
}
```

---

## Step 7: Multi-Device Setup (Optional)

The `wedaapi` template supports multiple devices in a single application:

```json
{
  "DeviceConfigs": {
    "TempSensor": {
      "Id": "temp-001",
      "ConnectionSettings": { "Host": "192.168.1.100", "Port": 502 },
      "Sensors": [/* ... */]
    },

    "PressureSensor": {
      "Id": "pressure-001",
      "ConnectionSettings": { "Host": "192.168.1.101", "Port": 502 },
      "Sensors": [/* ... */]
    },

    "FlowMeter": {
      "Id": "flow-001",
      "ConnectionSettings": { "Host": "192.168.1.102", "Port": 502 },
      "Sensors": [/* ... */]
    }
  }
}
```

All devices start automatically and run concurrently.

---

## Troubleshooting

### Connection Failed

**Error**: `Failed to connect to Modbus device`

**Solutions**:
1. Verify IP address and port in `appsettings.json`
2. Check network connectivity: `ping 192.168.1.100`
3. Verify the Modbus device is powered on and accessible
4. Try using sandbox mode to test without hardware

---

### NATS Connection Issues

**Error**: `Could not connect to NATS server`

**Solutions**:
1. Start NATS server: `nats-server -js`
2. Verify NATS URL in `appsettings.json`
3. For offline testing, set `"Nats.Enabled": false`

---

### Build Errors

**Error**: `Package restore failed`

**Solutions**:
1. Ensure .NET 9.0 SDK is installed: `dotnet --version`
2. Check internet connectivity
3. Force restore: `dotnet restore --force`

---

## What You've Learned

*> Create a project with `dotnet new wedaapi`
*> Configure devices entirely in JSON
*> Read Modbus sensors automatically
*> Send telemetry to cloud via NATS
*> Add transformations and filters without code
*> Support multiple devices in one application

---

## Next Steps

Now that you have a working application, continue learning:

1. **[02. Sandbox Testing](02_sandbox_testing.md)** - Test without hardware
2. **[03. Data Transformations](03_add_transformation.md)** - Advanced data processing
3. **[04. DSP Filters](04_add_dsp_filters.md)** - Signal processing techniques
4. **[05. Hooks & Events](05_using_hooks.md)** - Extend with custom logic

---

## When to Use wedaapi vs wedaapi-c vs subnode?

### Stick with `wedaapi` (Simple API) if:
- *> You're happy with configuration-driven approach
- *> No need for custom services or dependency injection
- *> Standard Modbus communication is sufficient

### Upgrade to `wedaapi-c` (Advanced API) when:
- ! You need to register custom services
- ! Custom logging or middleware required
- ! Want full control over application builder

### Upgrade to `subnode` (Custom Device) when:
- ! Need to override device lifecycle methods
- ! Custom protocol implementation
- ! Want to handle events programmatically

See **[01. Quick Start Overview](01_quick_start.md)** for template comparison.

---

## Summary

**Simple API Mode (`wedaapi`)** gives you:
- [FAST] **Fastest setup** - Just 3 lines of code
- [NOTE] **Configuration-driven** - Everything in JSON
- [TARGET] **Perfect for beginners** - No complex concepts
- >>> **Production-ready** - Built-in error handling, logging, retry logic

**Trade-offs**:
- X Limited customization (use `wedaapi-c` or `subnode` for more control)
- X Cannot register custom services (use `wedaapi-c` instead)
- X Cannot override device lifecycle (use `subnode` instead)

**Perfect for**: Data collection, prototyping, standard Modbus scenarios.

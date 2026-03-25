---
sidebar_position: 2
sidebar_label: 'Start with Example'
hide_title: true
title: 'Start with Example - Running Pre-built SubNode Examples'
keywords: ['SubNode', 'Example', 'WISE-4012', 'Modbus', 'Quick Start']
description: 'Learn SubNode by running and exploring pre-built examples'
---

# Start with Example

> Learn SubNode by running and exploring pre-built examples.

## Available Examples

The `examples/` directory contains production-ready examples:

| Example | Description | Protocol | Best For |
|---------|-------------|----------|----------|
| **wise-4012** | Industrial I/O module | Modbus TCP | Learning basics |
| **power-aggregation** | Multi-device aggregation | Multi-source | Data aggregation |
| **stock-monitor** | HTTP API integration | HTTP | Custom protocols |
| **image-sensor** | Image streaming | MQTT | Binary data |
| **air-quality-monitor** | Environmental sensing | Mixed | Sensor fusion |

## Running the WISE-4012 Example

The `wise-4012` example is recommended for beginners. It demonstrates:
- Basic device configuration
- Modbus TCP communication
- Sensor telemetry collection
- Event handling

### Step 1: Clone the Repository

```bash
git clone https://your-repo/edge_subnode.git
cd edge_subnode
```

### Step 2: Navigate to the Example

```bash
cd examples/wise-4012
```

### Step 3: Review the Project Structure

```
wise-4012/
├── Program.cs           # Application entry point
├── MyFirstDevice.cs     # Custom device implementation
├── devicecfg.json       # Device and sensor configuration
├── appsettings.json     # Logging configuration
└── wise-4012.csproj     # Project file
```

### Step 4: Configure the Device

Edit `devicecfg.json` to match your device:

```json
{
  "SubNode": {
    "Name": "Wise4012",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "172.16.8.122",   // <-- Change to your device IP
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "channel_0",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 1,
            "DataType": "UInt16"
          },
          "Report": {
            "Enabled": true,
            "Interval": 3000
          }
        }
        // ... more sensors
      ]
    }
  }
}
```

### Step 5: Run the Example

```bash
dotnet run
```

**Expected output:**

```
[12:34:56 INF] SubNode started. Press Ctrl+C to stop...
[12:34:57 INF] channel_0: 1234
[12:34:57 INF] channel_1: 5678
[12:34:57 INF] channel_2: 9012
[12:34:57 INF] channel_3: 3456
[12:35:00 INF] channel_0: 1235
...
```

Press `Ctrl+C` to stop.

## Understanding the Code

### Program.cs

```csharp
using Weda.SubNode.Host;
using Wise4012Example;

// Create application builder with defaults
var builder = WedaApplication.CreateDefaultBuilder(args);

// Register device with configuration key
builder.AddDevice<MyFirstDevice>("MyFirstDevice");

// Build and run
var app = builder.Build();
await app.RunAsync();
```

Key points:
- `CreateDefaultBuilder` loads configuration from `devicecfg.json` and `appsettings.json`
- `AddDevice<T>("key")` registers a device type with a configuration key
- The configuration key (`"MyFirstDevice"`) maps to `DeviceConfigs.MyFirstDevice` in JSON

### MyFirstDevice.cs

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        // Enable event tracking
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value}",
                    sensor.Name, measure.Value);
            }
        }
    }
}
```

Key points:
- Inherits from `TcpModbusDevice` for Modbus TCP support
- Constructor receives context and configuration key
- `DataReceived` event fires when telemetry is collected
- Access sensor configuration via `Configuration.Sensors`

## Running Without Hardware

If you don't have a physical device, use the built-in simulator:

1. Navigate to the template with simulator:
   ```bash
   cd templates/wedabuilder
   ```

2. Run the template (includes Modbus simulator):
   ```bash
   dotnet run
   ```

The simulator creates a virtual Modbus device at `127.0.0.1:5020`.

## Next Steps

- [Start with Template](./start-with-template.md) - Create your own project
- [Sensor Configuration](../04-sensor-configuration/configuration-via-json.md) - Configure sensors in detail
- [Project Structure](../03-project-structure.md) - Understand the codebase

## Troubleshooting

### Connection Refused

```
Error: Connection refused to 172.16.8.122:502
```

**Solutions:**
- Verify device IP address is correct
- Check network connectivity (`ping 172.16.8.122`)
- Ensure Modbus TCP port (502) is open
- Verify device is powered on

### No Data Received

**Solutions:**
- Check `SlaveId` matches your device configuration
- Verify register addresses are correct for your device
- Enable debug logging in `appsettings.json`:
  ```json
  {
    "Serilog": {
      "MinimumLevel": {
        "Default": "Debug"
      }
    }
  }
  ```

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

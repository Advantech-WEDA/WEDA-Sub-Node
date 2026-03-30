---
sidebar_position: 2
sidebar_label: 'Start with Example'
hide_title: true
title: 'Start with Example | SubNode SDK'
keywords: ['SubNode', 'Example', 'WISE-4012', 'Modbus', 'Quick Start']
description: 'Learn SubNode SDK by running and exploring pre-built examples.'
---

# Start with Example

> Learn SubNode by running and exploring pre-built examples.

## Overview

SubNode SDK ships with several ready-to-run example projects covering different protocols and use cases. This article walks you through the WISE-4012 example -- from running it, to configuring it, to understanding the code -- so you can quickly grasp the core SubNode workflow.

## What You'll Learn

After reading this article, you will be able to:

- Run a pre-built SubNode example project
- Understand the relationship between `devicecfg.json` and the `Program.cs` entry point
- Understand the basic structure of a custom device class
- Develop without physical hardware using the built-in simulator

## Prerequisites

- Completed [Prerequisites](./01-prerequisites.md)

---

## Available Examples

The `examples/` directory contains ready-to-run examples:

| Example | Description | Protocol | Best For |
|---------|-------------|----------|----------|
| **wise-4012** | Industrial I/O module | Modbus TCP | Learning basics |
| **power-aggregation** | Multi-device aggregation | Multi-source | Data aggregation |
| **stock-monitor** | HTTP API integration | HTTP | Custom protocols |
| **image-sensor** | Image streaming | MQTT | Binary data |
| **air-quality-monitor** | Environmental sensing | Mixed | Sensor fusion |

---

## Running the WISE-4012 Example

The `wise-4012` example is recommended for beginners. It demonstrates:

- Basic device configuration
- Modbus TCP communication
- Sensor telemetry collection
- Event handling

### Step 1: Clone the Repository

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
cd WEDA-Sub-Node
```

### Step 2: Navigate to the Example

```bash
cd examples/wise-4012
```

### Step 3: Review the Project Structure

```text
wise-4012/
├── Program.cs           # Application entry point
├── MyFirstDevice.cs     # Custom device implementation
├── devicecfg.json       # Device and sensor configuration
├── appsettings.json     # Logging configuration
└── wise-4012.csproj     # Project file
```

### Step 4: Configure the Device

Edit the `Host` field in `devicecfg.json` to match your device IP:

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
        "Host": "172.16.8.122",
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
      ]
    }
  }
}
```

> **Note**: Change `"Host": "172.16.8.122"` to your device's actual IP address.

### Step 5: Run the Example

```bash
dotnet run
```

**Expected output:**

```text
[12:34:56 INF] SubNode started. Press Ctrl+C to stop...
[12:34:57 INF] channel_0: 1234
[12:34:57 INF] channel_1: 5678
[12:34:57 INF] channel_2: 9012
[12:34:57 INF] channel_3: 3456
[12:35:00 INF] channel_0: 1235
...
```

Press `Ctrl+C` to stop.

---

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
- The configuration key (`"MyFirstDevice"`) maps to `DeviceConfigs.MyFirstDevice` in the JSON

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
- The `DataReceived` event fires when telemetry is collected
- Access sensor configuration via `Configuration.Sensors`

---

## Running Without Hardware

If you do not have a physical device, use the built-in simulator:

1. Navigate to the template with the simulator:

   ```bash
   cd templates/wedabuilder
   ```

2. Run the template (includes Modbus Simulator):

   ```bash
   dotnet run
   ```

The simulator creates a virtual Modbus device at `127.0.0.1:5020`.

---

## Troubleshooting

### Connection Refused

```text
Error: Connection refused to 172.16.8.122:502
```

**Solutions:**

- Verify the device IP address is correct
- Check network connectivity (`ping 172.16.8.122`)
- Ensure Modbus TCP port (502) is open
- Verify the device is powered on

### No Data Received

**Solutions:**

- Check that `SlaveId` matches your device configuration
- Verify the register addresses are correct for your device
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

---

## Summary

- The `examples/` directory provides several ready-to-run example projects
- `devicecfg.json` defines device connections and sensor settings; `Program.cs` wires them together via `AddDevice<T>("key")`
- Custom device classes inherit from `TcpModbusDevice` and handle telemetry through the `DataReceived` event
- Without physical hardware, use the built-in simulator in `templates/wedabuilder`

## See Also

- [Start with Template](./03-start-with-template.md) - Create your own project from a template
- [Sensor Configuration](../04-configuration/02-configuration-via-json.md) - Configure sensors in detail
- [Project Structure](../03-hierarchy/01-project-structure.md) - Understand the codebase

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-06 | Rain Hu | Doc created. |
| 1.1.0 | 2026-03-30 | Rain Hu | Rewritten to match zh version with Overview, What You'll Learn, Summary. |

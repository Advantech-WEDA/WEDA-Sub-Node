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

| Example | Description | Data Type | Protocol | Best For |
|---------|-------------|-----------|----------|----------|
| **wise-4012** | Industrial I/O module | double + boolean | Modbus TCP | Learning basics |
| **wise-4012-isensing** | Advantech proprietary protocol | double + boolean | ISensing MQTT | Learning basics |
| **stock-monitor** | HTTP API integration | double | HTTP | Custom protocols |
| **image-sensor** | Image streaming | image/png | MQTT | Binary data |
| **air-quality-monitor** | Environmental sensing | application/json | HTTP | Sensor fusion |
| **power-aggregation** | Multi-device aggregation | double | Multi-source | Data aggregation |

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
├── .weda/                    # Runtime data (registration cache, local storage)
├── payloads/                 # Sample command payloads for testing
├── Program.cs                # Application entry point (*)
├── MyFirstDevice.cs          # Custom device implementation (*)
├── devicecfg.json            # Device and sensor configuration (*)
├── systemcfg.json            # WedaNode connection settings
├── customcfg.json            # Custom application settings (reserved)
├── appsettings.json          # Logging (Serilog) configuration
├── Dockerfile                # Container image build
├── docker-compose.yml        # One-command container deployment
└── Wise4012Example.csproj    # Project file and NuGet references
```

> **Quick-start focus**: The three files marked `(*)` are the core. `Program.cs` handles startup, `MyFirstDevice.cs` defines device behavior, and `devicecfg.json` defines connections and sensors. The remaining files only need adjustment in advanced scenarios.

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

```json
{
  "SubNode": { ... },
  "DeviceConfigs": {
    "MyFirstDevice": { ... }
  }
}
```

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

If you do not have a physical device, you can use the SDK's built-in Modbus TCP Simulator.

### Option 1: Using Docker (Recommended)

The example directory provides `docker-compose-sim.yml` which starts both the Simulator and wise-4012 together:

```bash
# In examples/wise-4012/ directory
docker compose -f docker-compose-sim.yml up -d
```

> Remember to change `Host` to `"127.0.0.1"` and `Port` to `5020` in `devicecfg.json`.

View logs:

```bash
docker compose -f docker-compose-sim.yml logs -f wise-4012
```

Stop:

```bash
docker compose -f docker-compose-sim.yml down
```

### Option 2: Run Simulator with dotnet run

The SDK provides a standalone Simulator Host project at `tools/simulator-host/`. Start the Simulator in one terminal, then run the example in another:

**Terminal 1 - Start Simulator:**

```bash
cd tools/simulator-host
dotnet run
```

**Terminal 2 - Run the example:**

```bash
cd examples/wise-4012
dotnet run
```

> Remember to change `Host` to `"127.0.0.1"` and `Port` to `5020` in `devicecfg.json`.

The Simulator listens on `0.0.0.0:5020` by default and simulates temperature and humidity sensors. Simulation parameters can be adjusted in `tools/simulator-host/appsettings.json`.

### Option 3: Register Simulator in Program.cs

Register the Simulator as a HostedService directly in your application, so it starts and stops together with your app:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;

var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();

// Register Modbus simulator as hosted service
builder.Services.AddHostedService(sp =>
{
    var config = builder.Configuration
        .GetSection(TcpModbusSimulatorConfiguration.SectionName)
        .Get<TcpModbusSimulatorConfiguration>()
        ?? throw new InvalidOperationException(
            $"Missing '{TcpModbusSimulatorConfiguration.SectionName}' section");

    var logger = sp.GetRequiredService<ILogger<TcpModbusSimulator>>();
    return new TcpModbusSimulatorHostedService(config, logger);
});

builder.AddDevice<MyFirstDevice>("MyFirstDevice");

var app = builder.Build();
await app.RunAsync();
```

Add the `TcpModbusSimulatorConfiguration` section to `appsettings.json`:

```json
{
  "TcpModbusSimulatorConfiguration": {
    "TcpConnection": {
      "IpAddress": "0.0.0.0",
      "Port": 5020
    },
    "ModbusProtocol": {
      "SlaveId": 1
    },
    "Simulation": {
      "GlobalUpdateIntervalSeconds": 1,
      "EnableValueChanges": true
    },
    "Sensors": [
      {
        "Name": "TemperatureSensor",
        "Type": "Temperature",
        "StartAddress": 0,
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
}
```

> This approach is ideal for rapid iteration during development, with no Docker dependency.

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

## See Also

- [Start with Template](./03-start-with-template.md) - Create your own project from a template
- [Sensor Configuration](../04-configuration/02-configuration-via-json.md) - Configure sensors in detail
- [Project Structure](../03-hierarchy/01-project-structure.md) - Understand the codebase

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |
---
sidebar_position: 3
sidebar_label: 'Start with Template'
hide_title: true
title: 'Start with Template - Creating a New SubNode Project'
keywords: ['SubNode', 'Template', 'dotnet new', 'Project Creation']
description: 'Create a new SubNode project using dotnet templates'
---

# Start with Template

> Create a new SubNode project from scratch using templates.

## Available Templates

SubNode provides two project templates:

| Template | Name | Description |
|----------|------|-------------|
| **subnode** | WedaSubNode | Traditional SubNode pattern with explicit lifecycle control |
| **wedabuilder** | WedaBuilder | Builder pattern with fluent API (recommended) |

## Using the WedaBuilder Template (Recommended)

The `wedabuilder` template uses the modern builder pattern with automatic lifecycle management.

### Step 1: Create New Project

```bash
# From repository root
cd templates/wedabuilder

# Or copy to new location
cp -r templates/wedabuilder ~/projects/my-subnode
cd ~/projects/my-subnode
```

### Step 2: Restore Dependencies

```bash
dotnet restore
```

### Step 3: Review Project Structure

```
my-subnode/
├── Program.cs           # Application entry with builder pattern
├── MyFirstDevice.cs     # Custom device class
├── devicecfg.json       # Device configuration
├── appsettings.json     # Logging configuration
└── WedaBuilder.csproj   # Project file
```

### Step 4: Understand the Code

**Program.cs:**

```csharp
using Weda.SubNode.Host;
using WedaBuilder;

var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()           // Console and file logging
    .AddTelemetry()         // Uplink: telemetry reporting
    .AddHealthReporting()   // Uplink: health status
    .AddCommands()          // Downlink: remote commands
    .AddConfigUpdates()     // Downlink: configuration sync
    .AddRecording()         // Local: historical data storage
    .UseMockCloud();        // Use mock server (change for production)

builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");

var app = builder.Build();
await app.RunAsync();
```

**MyFirstDevice.cs:**

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Process received telemetry
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("Received: {ResourceId} = {Value}",
                measure.ResourceId, measure.Value);
        }
    }
}
```

### Step 5: Configure Your Device

Edit `devicecfg.json`:

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "YourCompany",
    "Model": "MyDevice-v1",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDeviceConfig": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "127.0.0.1",
        "Port": 5020
      },
      "Properties": {
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "SensorGroup": "TEMP",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Report": {
            "Enabled": true,
            "Interval": 5000
          }
        }
      ]
    }
  }
}
```

### Step 6: Run with Simulator

The template includes a Modbus simulator that starts automatically:

```bash
dotnet run
```

**Expected output:**

```
[12:00:00 INF] Modbus simulator started on 127.0.0.1:5020
[12:00:01 INF] SubNode started. Press Ctrl+C to stop...
[12:00:06 INF] Received: abc12 = 25.3
[12:00:11 INF] Received: abc12 = 25.5
...
```

## Using the SubNode Template

The `subnode` template provides explicit lifecycle control.

### Step 1: Create New Project

```bash
cd templates/subnode
```

### Step 2: Review the Code

**Program.cs:**

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;

// Create context with mock cloud
using var context = new WedaApplicationContext(
    options => options.CloudService = WedaFactory.Cloud.Mock);

// Configure and start simulator
var simulator = await ConfigureTcpModbusSimulator(context);

// Create SubNode with explicit lifecycle
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyFirstDevice(context, ConfigureDeviceConfiguration()));

// Manual lifecycle control
await subNode.InitializeAsync();
await subNode.StartAsync();

Console.WriteLine("SubNode started. Press Ctrl+C to stop...");
await Task.Delay(Timeout.Infinite);
```

### Differences from WedaBuilder

| Aspect | WedaBuilder | SubNode |
|--------|-------------|---------|
| Configuration | JSON-based | Code-based |
| Lifecycle | Automatic | Manual |
| Flexibility | Opinionated | Full control |
| Best for | Most use cases | Custom scenarios |

## Adding Configuration via Code

For the SubNode template, configure sensors programmatically:

```csharp
DeviceConfiguration ConfigureDeviceConfiguration()
{
    var modbusConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",
        Manufacturer = "YourCompany",
        Model = "CustomDevice-v1",
        Host = "127.0.0.1",
        Port = 5020,
        SlaveId = 1
    };

    // Add temperature sensor
    var tempSensor = new ModbusSensorReporturation
    {
        Name = "temperature.sensor",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };
    tempSensor.Config.Interval = 5000;

    modbusConfig.AddSensor(tempSensor);

    var deviceConfig = modbusConfig.ToDeviceConfiguration();
    deviceConfig.InitializeDtdl();

    return deviceConfig;
}
```

## Customizing Your Device

### Add More Sensors

```json
{
  "Sensors": [
    {
      "Name": "temperature",
      "SensorGroup": "TEMP",
      "Parameters": {
        "RegisterType": "HoldingRegister",
        "RegisterAddress": 0,
        "RegisterCount": 2,
        "DataType": "Float32"
      }
    },
    {
      "Name": "humidity",
      "SensorGroup": "AI",
      "Parameters": {
        "RegisterType": "HoldingRegister",
        "RegisterAddress": 2,
        "RegisterCount": 2,
        "DataType": "Float32"
      }
    }
  ]
}
```

### Add Data Transforms

```json
{
  "Sensors": [
    {
      "Name": "temperature",
      "Report": {
        "Enabled": true,
        "Interval": 5000,
        "Transforms": [
          {
            "Type": "calibration",
            "Enabled": true,
            "Parameters": {
              "Scale": 0.1,
              "Offset": -10.0
            }
          }
        ]
      }
    }
  ]
}
```

## Next Steps

- [Connect to WedaCore](./connect-to-wedacore.md) - Configure cloud connectivity
- [Sensor Configuration](../04-sensor-configuration/configuration-reference.md) - Full configuration reference
- [Data Pipeline](../05-data-pipeline/pipeline-overview.md) - Data transformation guide

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

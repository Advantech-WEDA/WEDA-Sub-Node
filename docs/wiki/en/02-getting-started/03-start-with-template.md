---
sidebar_position: 3
sidebar_label: 'Start with Template'
hide_title: true
title: 'Start with Template | SubNode SDK'
keywords: ['SubNode', 'Template', 'WedaBuilder', 'Project Creation']
description: 'Create a new SubNode project from scratch using project templates.'
---

# Start with Template

> Create a new SubNode project from scratch using project templates.

## Overview

SubNode SDK provides two project templates that let you quickly scaffold a runnable edge device application. This article covers the differences between them, the step-by-step setup, and how to customize sensors and data transforms on top of the template.

## What You'll Learn

After reading this article, you will be able to:

- Create a new project using the WedaBuilder template and run it
- Understand the differences between the WedaBuilder and SubNode templates
- Add sensors and data transforms via JSON or code

## Prerequisites

- Completed [Prerequisites](./01-prerequisites.md)

---

## Available Templates

| Template | Name | Description |
|----------|------|-------------|
| **wedabuilder** | WedaBuilder | Builder pattern with fluent API (recommended) |
| **subnode** | WedaSubNode | Traditional SubNode pattern with explicit lifecycle control |

---

## Using the WedaBuilder Template (Recommended)

The `wedabuilder` template uses the modern builder pattern with automatic lifecycle management.

### Step 1: Create a New Project

```bash
# Copy template to new location
cp -r templates/wedabuilder ~/projects/my-subnode
cd ~/projects/my-subnode
```

### Step 2: Restore Dependencies

```bash
dotnet restore
```

### Step 3: Review the Project Structure

```text
my-subnode/
├── Program.cs           # Application entry point (builder pattern)
├── MyFirstDevice.cs     # Custom device class
├── devicecfg.json       # Device configuration
├── appsettings.json     # Logging and simulator configuration
└── WedaBuilder.csproj   # Project file
```

### Step 4: Understand the Code

**Program.cs:**

```csharp
using Weda.SubNode.Host;
using WedaBuilder;

var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();       // Use mock server (change for production)

builder.AddDevice<MyFirstDevice>("MyFirstDevice");

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
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("Received: {ResourceId} = {Value}",
                measure.ResourceId, measure.Value);
        }
    }
}
```

### Step 5: Configure the Device

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
    "MyFirstDevice": {
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

### Step 6: Run with the Simulator

The template includes a Modbus Simulator that starts automatically (configured in `appsettings.json`):

```bash
dotnet run
```

**Expected output:**

```text
[12:00:00 INF] Modbus simulator started on 127.0.0.1:5020
[12:00:01 INF] SubNode started. Press Ctrl+C to stop...
[12:00:06 INF] Received: abc12 = 25.3
[12:00:11 INF] Received: abc12 = 25.5
...
```

---

## Using the SubNode Template

The `subnode` template provides explicit lifecycle control with code-based device configuration.

### Code Overview

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Cloud;

// Create context with mock cloud
using var context = new WedaApplicationContext(
    options => options.CloudService = Cloud.Mock());

// Create SubNode with explicit lifecycle
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyFirstDevice(context, ConfigureDeviceConfiguration()));

await subNode.InitializeAsync();
await subNode.StartAsync();

Console.WriteLine("SubNode started. Press Ctrl+C to stop...");
await Task.Delay(Timeout.Infinite);
```

### Configuring Sensors via Code

```csharp
TcpModbusDeviceConfiguration ConfigureDeviceConfiguration()
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

    var tempSensor = new ModbusSensorReporturation
    {
        Name = "temperature.sensor",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };
    tempSensor.Config.Interval = 1000;

    modbusConfig.AddSensor(tempSensor);

    return modbusConfig;
}
```

### Differences Between the Two Templates

| Aspect | WedaBuilder | SubNode |
|--------|-------------|---------|
| Configuration | JSON-based | Code-based |
| Lifecycle | Automatic | Manual |
| Flexibility | Opinionated | Full control |
| Best for | Most use cases | Custom scenarios |

---

## Customizing the Template

### Adding More Sensors

Add items to the `Sensors` array in `devicecfg.json`:

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

### Adding a Data Transform

Add `TransformPipeline` inside `Report`:

```json
{
  "Name": "temperature",
  "Report": {
    "Enabled": true,
    "Interval": 5000,
    "TransformPipeline": [
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
```

---

## Summary

- **WedaBuilder template** (recommended): Builder pattern + JSON configuration with automatic lifecycle management
- **SubNode template**: Code-based configuration + manual lifecycle control for fully custom scenarios
- Both include a built-in Modbus Simulator -- no physical hardware required for development
- Extend functionality through the `Sensors` array and `TransformPipeline` in `devicecfg.json`

## See Also

- [Connect to WedaCore](./04-connect-to-wedacore.md) - Set up cloud connectivity
- [Sensor Configuration](../04-configuration/02-configuration-via-json.md) - Full JSON configuration reference
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Data transformation guide

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-06 | Rain Hu | Doc created. |
| 1.1.0 | 2026-03-30 | Rain Hu | Rewritten to match zh version with Overview, What You'll Learn, Summary. |

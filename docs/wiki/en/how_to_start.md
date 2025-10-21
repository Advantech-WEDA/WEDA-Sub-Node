---
title: How to Start
category: guide
order: 1
parent: null
related:
  - path: introduction.md
    title: Introduction
  - path: modbus/modbus_scanner.md
    title: Modbus Scanner
  - path: architecture/overview.md
    title: Architecture Overview
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Quick start guide for Weda SubNode SDK - Installation and first steps
tags: [Quick Start, Installation, Tutorial, Getting Started]
---

# How to Start with Weda SubNode SDK

This guide will help you get started with the Weda SubNode SDK in just a few minutes.

## Prerequisites

- **.NET 9.0 SDK** or later
- Basic knowledge of C# and .NET
- A Modbus device (physical or simulator) for testing

## Installation

### Method 1: Using Project Template (Recommended)

Install the Weda SubNode project template:

```bash
dotnet new install Weda.SubNode.Templates
```

Create a new project:

```bash
dotnet new wedaapi -n MyIoTApp
cd MyIoTApp
```

### Method 2: Manual NuGet Installation

Add the required NuGet packages to your existing project:

```bash
dotnet add package Weda.SubNode.Host
dotnet add package Weda.SubNode.Devices
dotnet add package Weda.SubNode.Cloud
```

## Quick Start (5 Minutes)

### Step 1: Configure Your Device

Edit `appsettings.json`:

```json
{
  "Devices": [
    {
      "DeviceName": "My First Modbus Device",
      "DeviceType": "ModbusTCP",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502,
        "ProtocolType": "modbus"
      },
      "Properties": {
        "SlaveId": 1
      },
      "Periods": {
        "ReadTelemetry": 2000,
        "SendTelemetry": 5000,
        "ReportHealth": 60000
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          }
        },
        {
          "Name": "humidity",
          "Dtmi": "dtmi:advantech:EdgeSync:Humidity;1",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 2,
            "RegisterCount": 2,
            "DataType": "Float32"
          }
        }
      ]
    }
  ]
}
```

**Configuration Explained:**
- **DeviceName**: Friendly name for your device
- **Communication.Host**: IP address of your Modbus device
- **Communication.Port**: Modbus TCP port (default: 502)
- **Properties.SlaveId**: Modbus slave ID
- **Periods.ReadTelemetry**: How often to read from device (ms)
- **Periods.SendTelemetry**: How often to send to cloud (ms)
- **Sensors**: Define which registers to read

### Step 2: Write the Application Code

Edit `Program.cs`:

```csharp
using Weda.SubNode.Host;

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
```

That's it! Just 3 lines of code.

### Step 3: Run the Application

```bash
dotnet run
```

You should see output like:

```
[12:00:00 INF] Starting Weda SubNode Application...
[12:00:01 INF] Connecting to device: My First Modbus Device
[12:00:01 INF] Device connected successfully
[12:00:01 INF] Reading telemetry...
[12:00:01 INF] Temperature: 25.3°C
[12:00:01 INF] Humidity: 60.5%
[12:00:01 INF] Telemetry sent to cloud
```

## Don't Know Your Device Configuration?

Use the **Modbus Scanner** to auto-discover your device's registers:

```csharp
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Modbus;

var config = new DeviceConfiguration
{
    DeviceName = "Scanner",
    DeviceType = "ModbusTCP",
    Communication = new Dictionary<string, object>
    {
        ["Host"] = "192.168.1.100",
        ["Port"] = 502,
        ["SlaveId"] = 1
    },
    Sensors = []  // Empty - we'll discover them
};

var communication = WedaFactory.Communication.Tcp.Create("192.168.1.100", 502);
var device = new ModbusDevice(config, communication, WedaFactory.Cloud.Null);
await device.InitializeAsync();

// Scan registers
var results = await device.ScanRegistersAsync();

// Generate sensor configuration
var suggestions = device.GenerateSensorSuggestions(results);
```

See [Modbus Scanner Guide](modbus/ModbusScanner.md) for details.


---
title: Introduction to Weda SubNode SDK
category: overview
order: 1
parent: null
related:
  - path: how_to_start.md
    title: How to Start
  - path: architecture/overview.md
    title: Architecture Overview
  - path: modbus/modbus_scanner.md
    title: Modbus Scanner
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Introduction to Weda SubNode SDK - A comprehensive .NET SDK for IoT edge devices
tags: [Introduction, Overview, Getting Started]
---

# Introduction to Weda SubNode SDK

## What is Weda SubNode SDK?

**Weda SubNode SDK** is a .NET SDK designed for IoT edge devices, providing a complete solution for device connectivity, data acquisition, processing, and cloud integration.

## Key Features

### Multi-Protocol Support
- Built-in support for industrial communication protocols
- **Modbus TCP/RTU**: Full implementation with auto-discovery scanner
- **MQTT**: Support for publish/subscribe patterns
- Extensible protocol framework for custom protocols

### Flexible Data Processing Pipeline
- **Data Calibration**: Linear and non-linear calibration support
- **Unit Conversion**: Temperature, pressure, and custom unit conversions
- **DSP Filtering**: Kalman filter, moving average, and custom filters
- **Transform Pipeline**: Composable data transformation workflows

### Event-Driven Architecture
- Complete event system for all device lifecycle stages
- Easy extension of business logic through event subscription
- Real-time data processing and monitoring

### Cloud Integration
- Seamless integration with Weda EdgeSync Cloud platform
- NATS-based messaging for reliable communication
- Device registration and configuration management
- Telemetry upload and health reporting

### Easy to Use
- ASP.NET Core-style Host framework
- Configuration-based device setup
- Minimal code required for common scenarios
- Rich API for advanced use cases

## Use Cases

### Industrial IoT Data Collection
Collect data from industrial devices like PLCs, sensors, and actuators using standard protocols like Modbus.

### Edge Computing
Process and analyze data at the edge before sending to cloud, reducing latency and bandwidth usage.

### Device Health Monitoring
Monitor device health, connection status, and performance metrics in real-time.

### Multi-Device Management
Manage multiple devices in parallel with independent lifecycles and configurations.

### Data Quality Control
Apply calibration, filtering, and validation to ensure data quality before cloud upload.

## Architecture Overview

The SDK follows a layered architecture:

```
┌─────────────────────────────────────────┐
│       Host Framework                    │
│  - WedaApplication                      │
│  - Configuration Management             │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│       Device Layer                      │
│  - TcpModbusDevice                      │
│  - Custom Devices                       │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│       Core Layer                        │
│  - DeviceBase                           │
│  - ModbusDevice                         │
│  - Transforms & Filters                 │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│       Abstractions                      │
│  - IDevice                              │
│  - ICommunication                       │
│  - ITelemetryTransform                  │
└─────────────────────────────────────────┘
```

## Quick Example

Here's a minimal example to get started:

```csharp
using Weda.SubNode.Host;

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
```

With configuration in `appsettings.json`:

```json
{
  "Devices": [
    {
      "DeviceName": "My Modbus Device",
      "DeviceType": "ModbusTCP",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Parameters": {
            "RegisterAddress": 0,
            "DataType": "Float32"
          }
        }
      ]
    }
  ]
}
```

That's it! The SDK will automatically:
- Connect to the Modbus device
- Read sensor data periodically
- Apply calibration and filtering
- Upload telemetry to cloud
- Report device health


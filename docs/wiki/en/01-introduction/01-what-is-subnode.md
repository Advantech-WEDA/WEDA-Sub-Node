---
sidebar_position: 1
sidebar_label: 'What is SubNode?'
hide_title: true
title: 'What is SubNode? | SubNode SDK Documentation'
keywords:
  - SubNode SDK
  - Industrial IoT
  - IoT Edge Device
  - WedaCore
  - .NET IoT SDK
description: 'Learn how SubNode SDK simplifies cloud integration for industrial devices. Supports multiple industrial protocols with data processing pipeline and remote control capabilities.'
---

# What is SubNode?

> A lightweight .NET SDK for building edge device applications that connect industrial equipment to the WedaCore cloud platform.

## Overview

SubNode SDK is an edge device development framework designed for Industrial IoT (IIoT) scenarios. It addresses common challenges when connecting field devices (PLCs, sensors, industrial PCs) to cloud platforms: complex protocol integration, repetitive data processing development, high reliability requirements, and cloud communication costs. SubNode abstracts these low-level details, allowing developers to focus on business logic.

## What You'll Learn

After reading this article, you will be able to:

- Understand SubNode SDK's positioning and core value
- Understand SubNode's aggregation model architecture (SubNode → Device → Sensor)
- Identify application scenarios suitable for SubNode
- Run your first SubNode application (sandbox mode)

---

## Core Features

| Feature | Description |
|---------|-------------|
| **Aggregation Model** | SubNode serves as the Aggregation Root, managing 1..N Devices, each Device containing 1..N Sensors |
| **Device Communication** | Built-in support for Modbus TCP/RTU, MQTT, HTTP, with extensible custom protocols |
| **Data Collection** | Read sensor telemetry data at configurable intervals |
| **Data Processing** | Pipeline architecture for transformation (calibration, unit conversion) and filtering (moving average, Kalman) |
| **Cloud Integration** | Sync with WedaCore, supporting telemetry upload and remote commands |
| **Reliability** | Built-in reconnection, circuit breaker, and error retry mechanisms |

---

## System Architecture

```
┌──────────────────┐        ┌──────────────────┐        ┌──────────────────┐
│  Physical Device │        │     SubNode      │        │    WedaCore      │
│                  │        │   Application    │        │    (Cloud)       │
│  ┌────────────┐  │        │  ┌────────────┐  │        │  ┌─────────────┐ │
│  │    PLC     │  │ Modbus │  │   Device   │  │  NATS  │  │ Digital Twin│ │
│  └────────────┘  │◀──────▶│  └────────────┘  │◀──────▶│  └─────────────┘ │
│  ┌────────────┐  │        │  ┌────────────┐  │        │  ┌─────────────┐ │
│  │   Sensor   │  │  MQTT  │  │  Pipeline  │  │        │  │  IoT DB     │ │
│  └────────────┘  │◀──────▶│  └────────────┘  │        │  └─────────────┘ │
│  ┌────────────┐  │        │  ┌────────────┐  │        │  ┌─────────────┐ │
│  │  Custom    │  │ Custom │  │   Cloud    │  │        │  │   Remote    │ │
│  │  Device    │  │◀──────▶│  │  Service   │  │        │  │  Commands   │ │
│  └────────────┘  │        │  └────────────┘  │        │  └─────────────┘ │
└──────────────────┘        └──────────────────┘        └──────────────────┘
```

**Bidirectional Data Flow**:

1. **Uplink (Telemetry)**: Device reads → Pipeline processes → Cloud Service uploads to WedaCore
2. **Downlink (Command)**: WedaCore sends command → Cloud Service receives → Device executes control

---

## Target Audience

### Solution Architects

Complete most work through JSON configuration without writing code:

- **Configuration-driven**: Use `devicecfg.json` to define sensors, transformation rules, and reporting intervals
- **Pre-built device types**: Ready-to-use classes like `TcpModbusDevice`, `MqttDevice`
- **Example library**: Start from working examples in the `examples/` directory

### .NET Developers

Clean, extensible architecture:

- **Modern .NET 10**: Leverage the latest platform features
- **Dependency Injection**: Standard DI patterns for easy testing
- **Interface-oriented**: Clear contracts like `IDevice`, `ICommunication`, `IProtocolParser`
- **Pipeline architecture**: Composable Transforms and DSP Filters

---

## Supported Scenarios

| Scenario | Example | Protocol |
|----------|---------|----------|
| Industrial I/O Module | WISE-4012 analog/digital input | Modbus TCP |
| Power Monitoring | Multi-meter data aggregation | Modbus RTU |
| Environmental Sensing | Air quality monitoring station | MQTT |
| Vision System | Image sensor streaming | MQTT / HTTP |
| Custom Integration | REST API data source | HTTP |
| Special Equipment | Proprietary protocol devices | Custom Protocol |

---

## Quick Example

Minimal SubNode application (sandbox mode, using built-in Modbus Simulator):

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed
- Verify installation: `dotnet --version` should display `10.x.x`

### Create and Run Project

```bash
# Clone the project
git clone https://github.com/ADVANTECH-Corp/edge_subnode.git
cd edge_subnode

# Install project template from local
dotnet new install ./templates/wedabuilder

# Create application subdirectory
mkdir apps && cd apps

# Create project using template
dotnet new wedabuilder -n MyFirstSubnode

# Navigate to project
cd MyFirstSubnode

# Run (defaults to Modbus Simulator + MockCloudService)
dotnet run
```

### Expected Results

After running, SubNode will:
1. Start the built-in Modbus Simulator (sandbox mode)
2. Connect to `127.0.0.1:502`
3. Read the `temperature` sensor every 1 second
4. Simulate telemetry upload via MockCloudService

For detailed steps, see [Start with Template](../02-getting-started/03-start-with-template.md).

---

## Summary

- SubNode is an edge device SDK connecting industrial equipment to cloud platforms
- Uses aggregation model: SubNode (1) → Device (N) → Sensor (N) hierarchy
- Built-in support for common industrial protocols with extensible custom protocols
- Provides Pipeline architecture for data transformation and filtering
- Suitable for Solution Architects (JSON configuration) and .NET Developers (code extension)

## See Also

- [Architecture Overview](./02-architecture.md) - Understand system design and components
- [Terminology](./03-terminology.md) - Key terms and definitions
- [Start with Example](../02-getting-started/02-start-with-example.md) - Run your first SubNode application

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-23 | Rain Hu | Doc created. |

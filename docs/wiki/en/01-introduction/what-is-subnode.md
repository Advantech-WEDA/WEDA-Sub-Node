---
sidebar_position: 1
sidebar_label: 'What is SubNode?'
hide_title: true
title: 'What is SubNode? - Edge Device SDK Overview'
keywords: ['SubNode', 'Edge SDK', 'IoT', 'Device Integration', 'WedaCore']
description: 'Introduction to SubNode SDK - A .NET-based edge device SDK for industrial IoT applications'
---

# What is SubNode?

> A lightweight .NET SDK for building edge device applications that connect industrial equipment to the WedaCore cloud platform.

## Overview

SubNode is an edge device SDK designed for industrial IoT scenarios. It provides a standardized framework for:

- **Device Communication** - Connect to various industrial devices via Modbus TCP/RTU, MQTT, HTTP, and custom protocols
- **Data Collection** - Read sensor telemetry with configurable intervals and batching
- **Data Processing** - Transform and filter data through a pipeline architecture
- **Cloud Integration** - Seamlessly sync with WedaCore for digital twin management
- **Remote Control** - Execute commands and update configurations from the cloud

## Why SubNode?

### For Solution Architects (SA)

SubNode simplifies edge device integration without requiring deep programming knowledge:

- **Configuration-Driven** - Define sensors, transforms, and reporting via JSON files
- **Pre-built Device Types** - Use ready-made device classes for common protocols (Modbus, MQTT)
- **Device Activator** - GUI-based installation wizard handles deployment
- **Examples Library** - Start from working examples and customize for your needs

### For .NET Developers

SubNode provides a clean, extensible architecture:

- **Modern .NET 9.0** - Built on the latest .NET platform
- **Dependency Injection** - Standard DI patterns throughout
- **Interface-Based Design** - Clear contracts for customization
- **Pipeline Architecture** - Composable data transformations
- **Event-Driven** - Rich event system for monitoring and integration

## Core Capabilities

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              SubNode SDK                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐              │
│   │   Device     │    │    Data      │    │    Cloud     │              │
│   │Communication │───>│  Pipeline    │───>│ Integration  │              │
│   └──────────────┘    └──────────────┘    └──────────────┘              │
│         │                    │                    │                     │
│         v                    v                    v                     │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐              │
│   │ Modbus TCP   │    │ Transforms   │    │  WedaCore    │              │
│   │ Modbus RTU   │    │ DSP Filters  │    │  Telemetry   │              │
│   │ MQTT         │    │ Thresholds   │    │  Commands    │              │
│   │ HTTP         │    │ Calibration  │    │  Config Sync │              │
│   │ Custom       │    │              │    │              │              │
│   └──────────────┘    └──────────────┘    └──────────────┘              │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## Supported Scenarios

| Scenario | Example | Protocol |
|----------|---------|----------|
| Industrial I/O | WISE-4012 analog/digital inputs | Modbus TCP |
| Power Monitoring | Multi-meter data aggregation | Modbus RTU |
| Environmental Sensing | Air quality monitoring | MQTT |
| Vision Systems | Image sensor streaming | MQTT/HTTP |
| Custom Integration | Stock market data feed | HTTP API |

## Quick Example

A minimal SubNode application requires just a few lines of code:

```csharp
using Weda.SubNode.Host;

var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();
```

Combined with a `devicecfg.json` configuration file, this creates a fully functional edge device that:

1. Connects to a Modbus TCP device
2. Reads configured sensors at specified intervals
3. Transforms data through the pipeline
4. Reports telemetry to WedaCore

See the [Getting Started](../02-getting-started/02-start-with-example.md) guide to begin building your first SubNode application.

## What's Next?

- [Architecture Overview](./architecture.md) - Understand the system design
- [Terminology](./terminology.md) - Learn key concepts and terms
- [Getting Started](../02-getting-started/02-start-with-example.md) - Build your first application

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

---
sidebar_position: 0
sidebar_label: 'Home'
hide_title: true
title: 'SubNode SDK Documentation'
keywords: ['SubNode', 'SDK', 'Documentation', 'IoT', 'Edge Device']
description: 'Official documentation for SubNode SDK - A .NET-based edge device SDK for industrial IoT applications'
---

# SubNode SDK Documentation

> A lightweight .NET SDK for building edge device applications that connect industrial equipment to the WedaCore cloud platform.

## Quick Navigation

### Getting Started

New to SubNode? Start here:

| Guide | Description |
|-------|-------------|
| [What is SubNode?](./01-introduction/what-is-subnode.md) | Overview and capabilities |
| [Prerequisites](./02-getting-started/01-prerequisites.md) | Development environment setup |
| [Start with Example](./02-getting-started/02-start-with-example.md) | Run your first SubNode application |
| [Start with Template](./02-getting-started/03-start-with-template.md) | Create a new project |

### Core Concepts

Understand the fundamentals:

| Topic | Description |
|-------|-------------|
| [Architecture](./01-introduction/architecture.md) | System design and components |
| [Terminology](./01-introduction/terminology.md) | Key terms and definitions |
| [Project Structure](./03-hierarchy/01-project-structure.md) | SDK organization |

### Configuration

Configure your devices and sensors:

| Guide | Description |
|-------|-------------|
| [Configuration via JSON](./04-sensor-configuration/configuration-via-json.md) | JSON-based setup (recommended) |
| [Configuration via Code](./04-sensor-configuration/configuration-via-code.md) | Programmatic configuration |
| [Configuration Reference](./04-sensor-configuration/configuration-reference.md) | Complete options reference |
| [Liveness Heartbeat](./04-sensor-configuration/heartbeat.md) | Report SubNode connectivity to the platform |

### Data Processing

Process and transform sensor data:

| Topic | Description |
|-------|-------------|
| [Pipeline Overview](./05-data-pipeline/01-overview.md) | Data processing architecture |

### Cloud Integration

Connect to WedaCore:

| Guide | Description |
|-------|-------------|
| [Connect to WedaCore](./02-getting-started/04-connect-to-wedacore.md) | Cloud connectivity setup |

## Documentation Structure

```
docs/wiki/en/
├── 01-introduction/          # What is SubNode, Architecture, Terminology
├── 02-getting-started/       # Prerequisites, Examples, Templates, Cloud
├── 03-architecture/          # Project structure, SDK modules
├── 04-sensor-configuration/  # JSON, Code, Reference
├── 05-data-pipeline/         # Transforms, DSP filters
├── 06-remote-control/        # Commands, Configurations (coming soon)
├── 07-custom-device/         # ICommunication, IProtocolParser (coming soon)
├── 08-connection-settings/   # WedaNode credentials (coming soon)
└── 09-troubleshooting/       # FAQ, Common issues (coming soon)
```

## Target Audience

This documentation is designed for:

| Audience | Focus Areas |
|----------|-------------|
| **Solution Architects** | JSON configuration, Examples, Cloud integration |
| **.NET Developers** | Code configuration, Custom devices, API reference |

## Quick Example

A minimal SubNode application:

```csharp
using Weda.SubNode.Host;

var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();
```

With `devicecfg.json`:

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice"
  },
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "192.168.1.100",
        "Port": 502
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
            "Interval": 3000
          }
        }
      ]
    }
  }
}
```

## Examples

Explore production-ready examples in the `examples/` directory:

| Example | Description | Protocol |
|---------|-------------|----------|
| [wise-4012](https://github.com/example/edge_subnode/tree/main/examples/wise-4012) | Industrial I/O module | Modbus TCP |
| [power-aggregation](https://github.com/example/edge_subnode/tree/main/examples/power-aggregation) | Multi-device aggregation | Multi-source |
| [stock-monitor](https://github.com/example/edge_subnode/tree/main/examples/stock-monitor) | HTTP API integration | HTTP |
| [image-sensor](https://github.com/example/edge_subnode/tree/main/examples/image-sensor) | Image streaming | MQTT |

## Version

This documentation is for SubNode SDK v1.0.0.

## Language

- **English** (current)
- [Traditional Chinese (繁體中文)](../zh/README.md)

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

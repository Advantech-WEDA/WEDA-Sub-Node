---
sidebar_position: 2
sidebar_label: 'Architecture'
hide_title: true
title: 'SubNode Architecture Overview'
keywords: ['SubNode', 'Architecture', 'Device', 'Pipeline', 'Communication']
description: 'Technical architecture overview of SubNode SDK components and data flow'
---

# Architecture Overview

> Understanding the core components and data flow of SubNode SDK.

## System Architecture

SubNode follows a layered architecture that separates concerns between communication, protocol parsing, data processing, and cloud integration.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  WedaCore                                   │
│                              (Cloud Platform)                               │
└─────────────────────────────────────────────────────────────────────────────┘
                                      ▲
                                      │ NATS
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  WedaNode                                   │
│                         (Local Proxy: 127.0.0.1:4224)                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      ▲
                                      │ NATS
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SubNode Application                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                         WedaApplication Host                          │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │  │
│  │  │   Device    │  │   Device    │  │   Device    │  │   Device    │   │  │
│  │  │  Instance   │  │  Instance   │  │  Instance   │  │  Instance   │   │  │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘   │  │
│  │         │                │                │                │          │  │
│  │         v                v                v                v          │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │                      Cloud Service                              │  │  │
│  │  │              (Telemetry, Commands, Configuration)               │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                      ▲
                                      │ Various Protocols
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Physical Devices                                 │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐                 │
│  │ Modbus    │  │   MQTT    │  │   HTTP    │  │  Custom   │                 │
│  │  Device   │  │  Broker   │  │   API     │  │ Protocol  │                 │
│  └───────────┘  └───────────┘  └───────────┘  └───────────┘                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Device Architecture

Each device in SubNode follows a consistent internal structure:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  Device                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                        DeviceConfiguration                          │    │
│  │   (from devicecfg.json: sensors, communication, periods, etc.)      │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                        │
│                                    v                                        │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐          │
│  │  Communication  │───>│ Protocol Parser │───>│    Sensors      │          │
│  │   (Transport)   │    │   (Encoding)    │    │  (Data Points)  │          │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘          │
│          │                       │                      │                   │
│          │                       │                      v                   │
│          │                       │         ┌─────────────────────────┐      │
│          │                       └────────>│    Data Pipeline        │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   Transforms    │    │      │
│          │                                 │  └────────┬────────┘    │      │
│          │                                 │           v             │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   DSP Filters   │    │      │
│          │                                 │  └─────────────────┘    │      │
│          │                                 └─────────────────────────┘      │
│          │                                              │                   │
│          │                                              v                   │
│          │         ┌─────────────────────────────────────────────────┐      │
│          └────────>│              DeviceOrchestrator                 │      │
│                    │   (Lifecycle, Scheduling, Event Coordination)   │      │
│                    └─────────────────────────────────────────────────┘      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Key Components

### Communication Layer

The communication layer handles transport-level concerns:

| Interface | Pattern | Use Case |
|-----------|---------|----------|
| `IRequestResponseCommunication` | Request/Response | Modbus TCP, HTTP API |
| `IPubSubCommunication` | Publish/Subscribe | MQTT, NATS |
| `IStreamingCommunication` | Bidirectional Stream | WebSocket, gRPC |

### Protocol Parser

The protocol parser converts between raw protocol data and telemetry measures:

```
Raw Data (bytes/registers) <──> Protocol Parser <──> TelemetryMeasure
```

Built-in parsers include:
- **ModbusProtocolParser** - Modbus register interpretation
- **ISensingProtocolParser** - Advantech ISensing format
- **ImageProtocolParser** - Binary image data

### Data Pipeline

Data flows through a configurable pipeline:

```
Raw Value ──> Transform ──> DSP Filter ──>  Final Value
              (Scale,       (Smooth,       
               Offset)       Average)      
```

Each stage is optional and configurable per sensor.

### DeviceOrchestrator

The orchestrator coordinates device operations:

- **Lifecycle Management** - Initialize, Start, Stop sequences
- **Periodic Tasks** - Telemetry reading, health reporting
- **Event Dispatch** - DataReceived, ConnectionStateChanged, etc.
- **Configuration Updates** - Two-phase commit for config changes

## Data Flow

### Telemetry Flow (Device to Cloud)

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ Physical │    │ Protocol │    │   Data   │    │  Cloud   │    │ WedaCore │
│  Device  │───>│  Parser  │───>│ Pipeline │───>│ Service  │───>│          │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │                  │               │               │
     │  Raw bytes    │ TelemetryMeasure │  Transformed  │   NATS        │
     │  registers    │  (ResourceId,    │  measures     │  message      │
     │               │   Value, Time)   │               │               │
```

### Command Flow (Cloud to Device)

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ WedaCore │    │  Cloud   │    │ Command  │    │ Protocol │    │ Physical │
│          │───>│ Service  │───>│ Handler  │───>│  Parser  │───>│  Device  │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │                │               │              │
     │  NATS         │  DeviceCommand │  Execute      │  Encoded     │
     │  message      │  (Name, Params)│  logic        │  bytes       │
```

## Configuration Architecture

SubNode uses a hierarchical configuration system:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Configuration Sources                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Priority (lowest to highest):                                              │
│                                                                             │
│  1. appsettings.json          ─── Logging (Serilog) configuration           │
│                                                                             │
│  2. systemcfg.json            ─── System-level settings (NATS, cloud)       │
│         │                                                                   │
│         └──> "SystemConfig" section                                         │
│                                                                             │
│  3. devicecfg.json            ─── Device definitions and sensors            │
│         │                                                                   │
│         ├──> "SubNode" section (metadata)                                   │
│         └──> "DeviceConfigs" section (per-device settings)                  │
│                                                                             │
│  4. Environment Variables     ─── Override any setting                      │
│                                                                             │
│  5. Command-line Arguments    ─── Highest priority overrides                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Event System

SubNode provides a rich event system for monitoring and integration:

| Event | Description |
|-------|-------------|
| `DataReceived` | Raw data received from device |
| `DataProcessed` | Data after pipeline processing |
| `ConnectionStateChanged` | Device connection up/down |
| `DeviceStatusChanged` | Status transitions |
| `TelemetrySent` | Telemetry sent to cloud |
| `ValueChanged` | Sensor value changed |
| `ConfigurationUpdateReceived` | Config update from cloud |

Events are disabled by default for performance. Enable only the events you need.

## See Also

- [Terminology](./terminology.md) - Key terms and definitions
- [Project Structure](../03-hierarchy/01-project-structure.md) - File and folder organization
- [Sensor Configuration](../04-sensor-configuration/configuration-reference.md) - Detailed configuration options

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

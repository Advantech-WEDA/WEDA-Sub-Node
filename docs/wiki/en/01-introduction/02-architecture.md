---
sidebar_position: 2
sidebar_label: 'Architecture Overview'
hide_title: true
title: 'Architecture Overview | SubNode SDK'
keywords: ['SubNode', 'Architecture', 'Device', 'Pipeline', 'Communication', 'Aggregation Model']
description: 'Technical architecture overview of SubNode SDK core components and data flow, including aggregation model, device architecture, and configuration system.'
---

# Architecture Overview

> Understand SubNode SDK's core components and data flow.

## Overview

This article introduces the technical architecture of SubNode SDK, including system layering, aggregation model, device internal structure, and data flow. Understanding these concepts helps you correctly design and extend SubNode applications.

## What You'll Learn

After reading this article, you will be able to:

- Understand SubNode's layered system architecture
- Master the SubNode → Device → Sensor aggregation model
- Understand device internal components (Communication, Protocol Parser, Pipeline)
- Understand telemetry uplink and command downlink data flows

---

## System Architecture

SubNode adopts a layered architecture that separates concerns of communication, protocol parsing, data processing, and cloud integration.

```
┌─────────────────────┐    ┌─────────────────────────────────────────────────────────┐    ┌─────────────┐
│  Physical Devices   │    │                    WedaApplication                      │    │  WedaCore   │
│                     │    │                                                         │    │   (Cloud)   │
│  ┌───────────────┐  │    │  ┌─────────────┐    ┌─────────────────────────────────┐ │    │             │
│  │  Modbus PLC   │◀─┼────┼─▶│  Device A   │◀──▶│         Host Services           │ │    │ ┌─────────┐ │
│  └───────────────┘  │    │  │  (Modbus)   │    │  ┌───────────────────────────┐  │ │    │ │ Digital │ │
│                     │    │  └─────────────┘    │  │      CloudService         │◀─┼─┼───▶│ │  Twin   │ │
│  ┌───────────────┐  │    │  ┌─────────────┐    │  │  (Telemetry & Commands)   │  NATS   │ └─────────┘ │
│  │  MQTT Sensor  │◀─┼────┼─▶│  Device B   │◀──▶│  └───────────────────────────┘  │ │    │             │
│  └───────────────┘  │    │  │  (MQTT)     │    │  ┌───────────────────────────┐  │ │    │ ┌─────────┐ │
│                     │    │  └─────────────┘    │  │   DeviceOrchestrator      │  │ │    │ │ IoT DB  │ │
│  ┌───────────────┐  │    │  ┌─────────────┐    │  │  (Lifecycle & Scheduling) │  │ │    │ └─────────┘ │
│  │   HTTP API    │◀─┼────┼─▶│  Device C   │◀──▶│  └───────────────────────────┘  │ │    │             │
│  └───────────────┘  │    │  │  (HTTP)     │    │  ┌───────────────────────────┐  │ │    │ ┌─────────┐ │
│                     │    │  └─────────────┘    │  │    RecordingService       │  │ │    │ │ remote  │ │
│  ┌───────────────┐  │    │  ┌─────────────┐    │  │  (Local Storage & Replay) │  │ │    │ │ commands│ │
│  │ Custom Device │◀─┼────┼─▶│  Device D   │◀──▶│  └───────────────────────────┘  │ │    │ └─────────┘ │
│  └───────────────┘  │    │  │  (Custom)   │    │  ┌───────────────────────────┐  │ │    │             │
│                     │    │  └─────────────┘    │  │   ConfigurationManager    │  │ │    │ ┌─────────┐ │
│                     │    │                     │  │  (Runtime Config Update)  │  │ │    │ │ other   │ │
│                     │    │                     │  └───────────────────────────┘  │ │    │ │ services│ │
│                     │    │                     └─────────────────────────────────┘ │    │ └─────────┘ │
└─────────────────────┘    └─────────────────────────────────────────────────────────┘    └─────────────┘
        Edge                                     SubNode                                       Cloud
```

**Key Concepts**:

- Each **Physical Device** corresponds to one **Device Instance** (1:1 mapping)
- **WedaApplication** acts as a container, managing multiple Devices (1:N relationship)
- **Host Services** provide shared functionality:
  - **CloudService** - Telemetry upload and command reception
  - **DeviceOrchestrator** - Lifecycle and scheduling management
  - **RecordingService** - Local storage and replay
  - **ConfigurationManager** - Runtime configuration updates
- **CloudService** communicates with WedaCore via **NATS**

## Aggregation Model

SubNode adopts the Domain-Driven Design (DDD) aggregation pattern. **SubNode is the Aggregation Root**, managing multiple Device entities under it.

### SubNode and Device Relationship

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        SubNode (Aggregation Root)                           │
│                                                                             │
│  SubNodeId: "subnode-001"                                                   │
│  Name: "FactoryMonitor"                                                     │
│  SubNodeType: "AdamEthernet"                                                │
│                                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐              │
│  │    Device A     │  │    Device B     │  │    Device C     │              │
│  │  (Modbus PLC)   │  │  (MQTT Sensor)  │  │  (HTTP API)     │              │
│  │                 │  │                 │  │                 │              │
│  │  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  │              │
│  │  │ Sensor 1  │  │  │  │ Sensor 1  │  │  │  │ Sensor 1  │  │              │
│  │  │ Sensor 2  │  │  │  │ Sensor 2  │  │  │  │ Sensor 2  │  │              │
│  │  │ Sensor 3  │  │  │  └───────────┘  │  │  └───────────┘  │              │
│  │  └───────────┘  │  │                 │  │                 │              │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Hierarchy

| Level | Description | Cardinality |
|-------|-------------|-------------|
| **SubNode** | Root entity of the edge application, registration unit with WedaCore | 1 application = 1 SubNode |
| **Device** | Abstraction of physical device or data source | 1 SubNode contains 1..N Devices |
| **Sensor** | Data point within a device | 1 Device contains 1..N Sensors |

### Design Principles

1. **Unified Identity**: SubNode has a unique `SubNodeId`, all Devices and Sensors register with cloud through this ID
2. **Lifecycle Management**: SubNode coordinates initialization, start, and stop of all Devices
3. **Shared Services**: All Devices share the same Cloud Service connection
4. **Independent Communication**: Each Device can use different protocols to connect to different physical devices

### Code Mapping

```csharp
// SubNode level configuration
var builder = WedaApplication.CreateDefaultBuilder(args);

// Register multiple Devices (1:N relationship)
builder.AddDevice<TcpModbusDevice>("PlcDevice");      // Device A
builder.AddDevice<MqttDevice>("EnvironmentSensor");   // Device B
builder.AddDevice<HttpDevice>("WeatherApi");          // Device C

var app = builder.Build();
await app.RunAsync();
```

```json
{
  "SubNode": {
    "Name": "FactoryMonitor",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "YourCompany",  // Optional
    "Model": "SubNode-Template",    // Optional
    "SwVersion": "1.0.0"            // Optional
  },
  "DeviceConfigs": {
    "PlcDevice": { ... },           // Device A configuration
    "EnvironmentSensor": { ... },   // Device B configuration
    "WeatherApi": { ... }           // Device C configuration
  }
}
```

---

## Device Architecture

Each Device in SubNode follows a consistent internal structure:

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
│                                    ▼                                        │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐          │
│  │  Communication  │───▶│ Protocol Parser │───▶│    Sensors      │          │
│  │   (Transport)   │    │   (Encoding)    │    │  (Data Points)  │          │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘          │
│          │                       │                     │                    │
│          │                       │                     ▼                    │
│          │                       │         ┌─────────────────────────┐      │
│          │                       └────────▶│    Data Pipeline        │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   Transforms    │    │      │
│          │                                 │  └────────┬────────┘    │      │
│          │                                 │           ▼             │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   DSP Filters   │    │      │
│          │                                 │  └─────────────────┘    │      │
│          │                                 └─────────────────────────┘      │
│          │                                             │                    │
│          │                                             ▼                    │
│          │         ┌─────────────────────────────────────────────────┐      │
│          └────────▶│              DeviceOrchestrator                 │      │
│                    │   (Lifecycle, Scheduling, Event Coordination)   │      │
│                    └─────────────────────────────────────────────────┘      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Core Components

### Communication Layer

The communication layer handles transport-level concerns:

| Interface | Pattern | Use Cases |
|-----------|---------|-----------|
| `IRequestResponseCommunication` | Request/Response | Modbus TCP, HTTP API |
| `IPubSubCommunication` | Publish/Subscribe | MQTT, NATS |
| `IStreamingCommunication` | Bidirectional Stream | WebSocket, gRPC |

### Protocol Parser

Protocol Parser converts between raw protocol data and telemetry measures:

```
Raw Data (bytes/registers) ◀──▶ Protocol Parser ◀──▶ TelemetryMeasure
```

Built-in Parsers include:
- **ModbusProtocolParser** - Modbus register interpretation
- **ISensingProtocolParser** - Advantech ISensing format
- **ImageProtocolParser** - Binary image data

### Data Pipeline

Data flows through a configurable Pipeline:

```
Raw Value ──▶ Transform ──▶ DSP Filter ──▶ Final Value
              (Scale,       (Smooth,
               Offset)       Average)
```

Each stage is optional and can be configured per sensor.

### DeviceOrchestrator

The Orchestrator coordinates device operations:

- **Lifecycle Management** - Initialize, Start, Stop sequences
- **Periodic Tasks** - Telemetry reading, health reporting
- **Event Dispatch** - DataReceived, ConnectionStateChanged, etc.
- **Configuration Updates** - Two-phase commit for configuration changes

## Data Flow

### Telemetry Flow (Device to Cloud)

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ Physical │    │ Protocol │    │   Data   │    │  Cloud   │    │ WedaCore │
│  Device  │───▶│  Parser  │───▶│ Pipeline │───▶│ Service  │───▶│          │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │                  │               │             │
     │  Raw bytes    │ TelemetryMeasure │  Transformed  │   NATS      │
     │  registers    │  (ResourceId,    │  measures     │  message    │
     │               │   Value, Time)   │               │             │
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

SubNode uses the **Digital Twin Shadow** concept to manage configuration. Three configuration files represent different scopes, each independent with **no priority order**:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    Configuration Files (Digital Twin Shadow)                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐  │
│  │   systemcfg.json    │  │   devicecfg.json    │  │   customcfg.json    │  │
│  │   (System Scope)    │  │   (Device Scope)    │  │    (App Scope)      │  │
│  ├─────────────────────┤  ├─────────────────────┤  ├─────────────────────┤  │
│  │                     │  │                     │  │                     │  │
│  │  WedaNode:          │  │  SubNode:           │  │  {Custom Settings}  │  │
│  │    Url              │  │    Name             │  │                     │  │
│  │    AuthStrategy     │  │    SubNodeType      │  │  User-defined       │  │
│  │    Username         │  │    Manufacturer     │  │  key-value pairs    │  │
│  │    Password         │  │    Model            │  │  for application    │  │
│  │    ...              │  │    SwVersion        │  │  business logic     │  │
│  │                     │  │                     │  │                     │  │
│  │                     │  │  DeviceConfigs:     │  │                     │  │
│  │                     │  │    {DeviceName}:    │  │                     │  │
│  │                     │  │      Sensors        │  │                     │  │
│  │                     │  │      Communication  │  │                     │  │
│  │                     │  │      Periods        │  │                     │  │
│  │                     │  │      ...            │  │                     │  │
│  │                     │  │                     │  │                     │  │
│  └─────────────────────┘  └─────────────────────┘  └─────────────────────┘  │
│           │                        │                        │               │
│           ▼                        ▼                        ▼               │
│     SystemConfig             DeviceConfig              CustomConfig         │
│       Section                  Section                   Section            │
│                                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│  Override Sources (can override any config file settings):                  │
│                                                                             │
│  - Environment Variables    SystemConfig:WedaNode:Url="nats://..."          │
│  - Command-line Arguments   --SystemConfig:WedaNode:Url="nats://..."        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Configuration File Purposes

| File | Scope | Purpose |
|------|-------|---------|
| `systemcfg.json` | System | NATS connection settings (URL, authentication, credentials) |
| `devicecfg.json` | Device | SubNode identity, device definitions, sensor configuration |
| `customcfg.json` | Application | Application-specific custom settings (business logic, thresholds) |
| `appsettings.json` | Logging | Serilog logging configuration (not synced with Digital Twin) |

## Event System

SubNode provides a rich event system for monitoring and integration:

| Event | Description |
|-------|-------------|
| `DataReceived` | Raw data received from device |
| `DataProcessed` | Data after Pipeline processing |
| `ConnectionStateChanged` | Device connection state changed |
| `DeviceStatusChanged` | Status transitions |
| `TelemetrySent` | Telemetry sent to cloud |
| `ValueChanged` | Sensor value changed |
| `ConfigurationUpdateReceived` | Configuration update received from cloud |

Events are disabled by default for performance. Enable only the events you need. For details, see [Event System](../09-customization/02-event-system.md).

## Summary

- SubNode adopts a layered architecture separating communication, protocol parsing, data processing, and cloud integration
- Aggregation model: SubNode (1) → Device (N) → Sensor (N), with SubNode as the Aggregation Root
- Each Device contains Communication, Protocol Parser, Data Pipeline, and DeviceOrchestrator
- Data flow is divided into uplink (telemetry) and downlink (command) directions
- Configuration system uses Digital Twin Shadow concept, with three config files (system/device/custom) managing different scopes

## See Also

- [Terminology](./03-terminology.md) - Key terms and definitions
- [Project Structure](../03-hierarchy/01-project-structure.md) - File and folder organization
- [Sensor Configuration](../04-configuration/03-configuration-examples.md) - Detailed configuration options
- [Event System](../09-customization/02-event-system.md) - Custom event handling

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-23 | Rain Hu | Doc created. |

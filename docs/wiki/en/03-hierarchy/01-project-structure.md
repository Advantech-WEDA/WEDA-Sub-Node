---
sidebar_position: 1
sidebar_label: 'Project Structure'
hide_title: true
title: 'Project Structure | SubNode SDK'
keywords: ['SubNode', 'Project Structure', 'SDK', 'Architecture', 'Module']
description: 'Understand the SubNode SDK project structure, module responsibilities, and configuration loading mechanism.'
---

# Project Structure

> Understand the SubNode SDK project structure, module responsibilities, and configuration loading mechanism.

## Overview

SubNode SDK uses a modular architecture, placing interface definitions, core implementations, pre-built devices, application hosting, and cloud integration in separate projects. This article covers the top-level structure, the responsibilities of each SDK module, and the typical application project layout.

## What You'll Learn

After reading this article, you will be able to:

- Locate SDK source code, examples, templates, and tools
- Understand the responsibilities and dependencies of each SDK module
- Understand the configuration file loading mechanism

## Prerequisites

- Completed [Start with Example](../02-getting-started/02-start-with-example.md) or [Start with Template](../02-getting-started/03-start-with-template.md)

---

## Project Structure

```text
edge_subnode/
├── src/                    # SDK source code
│   ├── Weda.SubNode.Abstractions/   # Interfaces and contracts
│   ├── Weda.SubNode.Core/           # Core implementations
│   ├── Weda.SubNode.Devices/        # Pre-built device types
│   ├── Weda.SubNode.Host/           # Application hosting
│   ├── Weda.SubNode.Cloud/          # Cloud integration
│   ├── Weda.SubNode.Simulators/     # Test simulators
│   └── Weda.SubNode.WebApi/         # REST API support
├── apps/                   # User applications (created via template)
├── examples/               # Production-ready examples
├── templates/              # dotnet new project templates
├── tools/                  # Development utilities
│   ├── simulator-host/              # Standalone simulator runner
│   ├── nats-check/                  # NATS connection diagnostic
│   └── config-manager/              # Configuration management
├── tests/                  # Unit and integration tests
└── docs/                   # Documentation
```

---

## SDK Modules

### Module Dependencies

```text
┌───────────────────────────────────────────────────────────────────┐
│                        Application Layer                          │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  .Host  (aggregates Cloud + Devices + Core)                 │  │
│  └────────────┬──────────────┬─────────────────────────────────┘  │
│               │              │                                    │
│               │    ┌─────────▼──────────────────────────────┐     │
│               │    │  Optional Packages                     │     │
│               │    │  ┌──────────────┐  ┌────────────────┐  │     │
│               │    │  │  .WebApi     │  │  .Simulators   │  │     │
│               │    │  └──────────────┘  └────────────────┘  │     │
│               │    └────────────────────────────────────────┘     │
│               │                                                   │
├───────────────┼───────────────────────────────────────────────────┤
│               ▼              Ready-to-Use Layer                   │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  .Devices  (pre-built devices, depends on .Core)            │  │
│  │  TcpModbusDevice, MqttDevice, AggregatorDevice ...          │  │
│  └───────────────────────────┬─────────────────────────────────┘  │
│                              │                                    │
├──────────────────────────────┼────────────────────────────────────┤
│                              ▼          Implementation Layer      │
│  ┌──────────────────┐  ┌──────────────────┐                       │
│  │      .Cloud      │  │      .Core       │                       │
│  │  (independent)   │  │ DeviceBase,      │                       │
│  │  NATS, Telemetry │  │ Communication,   │                       │
│  │                  │  │ Protocols,       │                       │
│  │                  │  │ Transforms       │                       │
│  └────────┬─────────┘  └────────┬─────────┘                       │
│           │                     │                                 │
├───────────┼─────────────────────┼─────────────────────────────────┤
│           ▼                     ▼              Contract Layer     │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │              Weda.SubNode.Abstractions                     │   │
│  │              (Interfaces and Contracts)                    │   │
│  └────────────────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────────────┘
```

Key design principles:

- **Host is the aggregator** - Brings together Cloud, Devices, and Core; provides the `WedaApplication` entry point
- **Cloud and Core/Devices are independent** - Host is responsible for composing them together
- **Devices depends on Core** - Devices are pre-assembled ready-to-use solutions (Communication + ProtocolParser + DeviceBase)
- **WebApi and Simulators are optional packages** - Added on demand, do not affect core functionality

### Weda.SubNode.Abstractions

The contract layer of the SDK, defining all core interfaces. All other modules depend on this project, but it depends on no other SDK module.

| Directory | Key Classes | Responsibility |
|-----------|-------------|----------------|
| `Devices/` | `IDevice`, `DeviceConfiguration` | Device lifecycle and configuration |
| `Communication/` | `ICommunication`, `IRequestResponseCommunication`, `IPubSubCommunication` | Transport abstraction |
| `Protocols/` | `IProtocolParser`, `IRequestResponseProtocolParser` | Protocol encoding/decoding |
| `Telemetry/` | `Sensor`, `TelemetryMeasure`, `SensorReport` | Sensor and telemetry data models |
| `Transforms/` | `ITelemetryTransform`, `IConfigurableTransform` | Data transformation interfaces |
| `Dsp/` | `IDspFilter` | Digital signal processing interfaces |
| `Commands/` | `ICommand`, `ICommandHandler` | Command handling interfaces |
| `Events/` | `DataReceivedEvent`, `ConnectionStateChangedEvent` | Event types |
| `Context/` | `IWedaApplicationContext` | Application context |

### Weda.SubNode.Core

Core implementation layer containing DeviceBase, communication, protocol parsing, and data transformation implementations.

| Directory | Key Classes | Responsibility |
|-----------|-------------|----------------|
| `Devices/` | `DeviceBase`, `DeviceOrchestrator`, `DeviceInitializer` | Device base class and scheduling |
| `Communication/` | `TcpCommunication`, `MqttCommunication` | Transport implementations |
| `Protocols/Modbus/` | `ModbusProtocolParser`, `ModbusBatchReader` | Modbus protocol parsing |
| `Protocols/ISensing/` | `ISensingProtocolParser` | Advantech ISensing protocol |
| `Transforms/` | `CalibrationTransform`, `UnitConversionTransform` | Built-in transforms |
| `Dsp/` | `MovingAverageFilter`, `KalmanFilter` | Built-in DSP filters |

### Weda.SubNode.Devices

Pre-built device classes that inherit from `DeviceBase` and combine specific Communication + ProtocolParser.

```text
IDevice
└── DeviceBase
    ├── TcpModbusDevice        # Modbus TCP
    ├── RtuModbusDevice        # Modbus RTU (Serial)
    ├── MqttDevice             # MQTT Pub/Sub
    └── AggregatorDevice       # Multi-source aggregation
```

### Weda.SubNode.Host

Application hosting layer providing `WedaApplication` and `WedaApplicationBuilder`.

| Class | Responsibility |
|-------|----------------|
| `WedaApplication` | `CreateDefaultBuilder` / `CreateBuilder` entry point |
| `WedaApplicationBuilder` | Fluent API: `AddDevice`, `AddTelemetry`, `UseMockCloud`, etc. |
| `WedaApplicationContext` | Application context used by SubNode template |
| `SubNode` | SubNode template lifecycle management (semi-automatic mode) |

### Weda.SubNode.Cloud

Cloud integration, communicating with WedaNode/WedaCore via NATS.

| Class | Responsibility |
|-------|----------------|
| `WedaCloudService` | Real cloud service (Telemetry, Command, Config Sync) |
| `MockCloudService` | Mock cloud for development |
| `Cloud` | Factory: `Cloud.Mock()`, `Cloud.Default()`, `Cloud.WithUserPassword()` |

### Weda.SubNode.Simulators

Simulators for development without hardware.

| Class | Responsibility |
|-------|----------------|
| `TcpModbusSimulator` | Simulates Modbus TCP devices |
| `MqttImageSimulator` | Simulates MQTT image streaming |
| `WebSocketSimulator` | Simulates WebSocket streaming |

---

## Application Project Structure

A typical application created with `dotnet new wedabuilder`:

```text
MySubNode/
├── Program.cs                # Application entry point (*)
├── MyFirstDevice.cs          # Custom device implementation (*)
├── devicecfg.json            # Device and sensor configuration (*)
├── systemcfg.json            # WedaNode connection settings
├── customcfg.json            # Custom application settings (reserved)
├── appsettings.json          # Logging (Serilog) configuration
├── Dockerfile                # Container image build
├── docker-compose.yml        # One-command container deployment
└── MySubNode.csproj          # Project file and ProjectReference
```

> The three files marked `(*)` are the core. See [Start with Example](../02-getting-started/02-start-with-example.md) for details.

---

## Configuration Loading Mechanism

Each configuration file is responsible for its own section -- they are independent and do not override each other:

```text
┌───────────────────────────────────────────────────────────────────────┐
│                    Configuration Sources                              │
├───────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  JSON Files (each maps to its own section)                            │
│  ┌─────────────────────┐  ┌────────────────────────────────────────┐  │
│  │  appsettings.json   │  │  appsettings.{Environment}.json        │  │
│  │  ──> Root           │  │  ──> Root (override)                   │  │
│  └─────────────────────┘  └────────────────────────────────────────┘  │
│  ┌─────────────────────┐  ┌───────────────────┐  ┌─────────────────┐  │
│  │  systemcfg.json     │  │ devicecfg.json    │  │ customcfg.json  │  │
│  │  ──> SystemConfig   │  │ ──> DeviceConfig  │  │ ──>CustomConfig │  │
│  │      :WedaNode      │  │    :SubNode       │  │                 │  │
│  │                     │  │    :DeviceConfigs  │  │                 │  │
│  └─────────────────────┘  └───────────────────┘  └─────────────────┘  │
│                                                                       │
│  Overrides (can override ANY section, highest priority)               │
│  ┌─────────────────────┐  ┌────────────────────────────────────────┐  │
│  │ Environment vars    │  │ Command-line arguments                 │  │
│  │ (e.g. SystemConfig  │  │ (e.g. --SystemConfig:WedaNode:         │  │
│  │  __WedaNode__Url)   │  │        Url=127.0.0.1:4224)             │  │
│  └─────────────────────┘  └────────────────────────────────────────┘  │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

> Environment variables and command-line arguments can override any section. Command-line arguments have the highest priority.

| File | Maps to Section | Cloud Synced | Purpose |
|------|-----------------|--------------|---------|
| `appsettings.json` | Root | No | Logging, Simulator settings |
| `systemcfg.json` | `SystemConfig` | No | WedaNode connection settings |
| `devicecfg.json` | `DeviceConfig` | Yes | Device definitions, sensors |
| `customcfg.json` | `CustomConfig` | Yes | Custom application settings |

---

## Summary

- The SDK consists of 7 modules: Abstractions (contracts), Core (implementations), Devices (pre-built), Host (hosting), Cloud (cloud), Simulators (simulation), WebApi (REST)
- All modules depend on Abstractions, forming a clear layered architecture
- Three core application files: `Program.cs`, `MyFirstDevice.cs`, `devicecfg.json`
- Configuration files map to independent sections; environment variables and command-line arguments can override any section

## See Also

- [Architecture Overview](../01-introduction/02-architecture.md) - System design and data flow
- [Start with Template](../02-getting-started/03-start-with-template.md) - Create a new project
- [Sensor Configuration](../04-configuration/02-configuration-via-json.md) - Configure devices and sensors

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |

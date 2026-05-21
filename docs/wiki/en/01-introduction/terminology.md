---
sidebar_position: 3
sidebar_label: 'Terminology'
hide_title: true
title: 'SubNode Terminology and Key Concepts'
keywords: ['SubNode', 'Terminology', 'Glossary', 'Concepts', 'IoT']
description: 'Glossary of key terms and concepts used in SubNode SDK documentation'
---

# Terminology

> Key terms and concepts used throughout SubNode SDK documentation.

## Core Concepts

### SubNode

A **SubNode** is an edge device application built with the SubNode SDK. It represents a logical grouping of one or more devices that communicate with the WedaCore cloud platform. Each SubNode has:

- A unique identifier (`SubNodeId`)
- Metadata (name, type, manufacturer, model, version)
- One or more Device instances

### WedaCore

**WedaCore** is the cloud platform that SubNode applications connect to. It provides:

- Digital twin management
- Telemetry storage and visualization
- Remote command execution
- Configuration management
- Device monitoring and alerting

### WedaNode

**WedaNode** is a local NATS proxy service installed by the Device Activator. It runs at `127.0.0.1:4224` and acts as a bridge between SubNode applications and WedaCore.

```
┌──────────────┐         ┌──────────────┐         ┌──────────────┐
│   SubNode    │──NATS──>│   WedaNode   │──NATS──>│   WedaCore   │
│ Application  │         │ (127.0.0.1:  │         │   (Cloud)    │
│              │         │     4224)    │         │              │
└──────────────┘         └──────────────┘         └──────────────┘
```

### Device Activator

**Device Activator** is a GUI installation wizard that:

- Deploys SubNode applications to edge devices
- Installs and configures WedaNode
- Manages device credentials and certificates
- Handles system service registration

## Device Components

### Device

A **Device** represents an abstraction of a physical device or data source. In code, devices implement the `IDevice` interface or extend `DeviceBase`. A SubNode application can contain multiple devices.

Example device types:
- `TcpModbusDevice` - Modbus TCP devices
- `MqttDevice` - MQTT-based devices
- `AggregatorDevice` - Combines data from multiple devices
- Custom device classes for specific protocols

### Sensor

A **Sensor** is a data point within a device. Each sensor has:

| Property | Description |
|----------|-------------|
| `ResourceId` | Unique identifier (UUID, auto-generated) |
| `Name` | Human-readable name (e.g., `temperature_sensor1`), required to comploy with IoT DB naming rule |
| `SensorGroup` | Category: AI, DO, DI, SYS, TEMP, PWR |
| `Parameters` | Protocol-specific settings (register address, etc.) |
| `Report` | Reporting configuration of telemetry|
| `Record` | Local storage configuration |
| `SensorInfo` | Schema and display metadata |

### TelemetryMeasure

A **TelemetryMeasure** is a single data reading from a sensor:

```csharp
public record TelemetryMeasure
{
    public string ResourceId { get; }    // Sensor identifier
    public object Value { get; }          // The measured value
    public long Timestamp { get; }        // Unix timestamp (ms)
    public IReadOnlyDictionary<string, object>? Metadata { get; }
}
```

### SensorGroup

Sensors are categorized into groups:

| Group | Description | Example |
|-------|-------------|---------|
| `AI` | Analog Input | Voltage, current, temperature sensors |
| `AO` | Analog Output | Variable output control |
| `DI` | Digital Input | Switches, buttons, contact sensors |
| `DO` | Digital Output | Relays, LEDs, actuators |
| `SYS` | System | CPU usage, memory, health metrics |
| `TEMP` | Temperature | Temperature sensors specifically |
| `PWR` | Power | Power consumption, energy meters |

## Communication

### ICommunication

An interface defining how data is transported between SubNode and physical devices. Three patterns are supported:

| Pattern | Interface | Use Case |
|---------|-----------|----------|
| Request/Response | `IRequestResponseCommunication` | Modbus, HTTP |
| Publish/Subscribe | `IPubSubCommunication` | MQTT, NATS |
| Streaming | `IStreamingCommunication` | WebSocket, gRPC |

### Protocol Parser

A **Protocol Parser** converts between raw protocol data and `TelemetryMeasure` objects. It handles:

- Encoding commands for the device
- Decoding responses into telemetry
- Data type interpretation (registers to values)

Built-in parsers:
- `ModbusProtocolParser` - Modbus registers
- `ISensingProtocolParser` - Advantech ISensing format
- `ImageProtocolParser` - Binary image data

## Data Processing

### Data Pipeline

The **Data Pipeline** is a sequence of processing stages that telemetry data flows through:

```
Raw Value ──> Transforms ──> DSP Filters ──> Final Value
```

### Transform

A **Transform** modifies telemetry values. Configured per sensor in the `Report.Transforms` array.

| Transform | Type Name | Purpose |
|-----------|-----------|---------|
| Calibration | `calibration` | Apply scale and offset |
| Unit Conversion | `unitconversion` | Convert between units |
| Chunking | `chunking` | Batch data points |

Example configuration:
```json
{
  "Transforms": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": {
        "Scale": 0.1,
        "Offset": 10.0
      }
    }
  ]
}
```

### DSP Filter

A **DSP (Digital Signal Processing) Filter** performs signal processing on telemetry data. Common uses include smoothing, averaging, and noise reduction.

## Commands and Configuration

### Command

A **Command** is a remote operation request sent from WedaCore to a device. Commands have:

- `DeviceCmd` - Command identifier (e.g., `set.do`, `report.data`)
- `Parameters` - Command-specific data
- `Timeout` - Maximum execution time

### Command Handler

A **Command Handler** processes incoming commands and returns results. Implement `ICommandHandler<TCommand, TResult>` for custom commands.

### DeviceConfiguration

**DeviceConfiguration** is the runtime representation of a device's settings, loaded from `devicecfg.json`. It includes:

- Device metadata (name, enabled status)
- Communication settings (host, port, credentials)
- Sensor definitions
- Reporting periods

## Configuration Files

### devicecfg.json

The primary configuration file defining devices and sensors:

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice"
  },
  "DeviceConfigs": {
    "DeviceName": {
      "Enabled": true,
      "DeviceCommunication": { ... },
      "Sensors": [ ... ]
    }
  }
}
```

### systemcfg.json

System-level configuration for NATS connections and cloud settings:

```json
{
  "SystemConfig": {
    "NatsUrl": "nats://127.0.0.1:4224",
    "CredentialsFile": "/path/to/creds.creds"
  }
}
```

### appsettings.json

Standard .NET configuration for logging (Serilog) and other runtime settings.

## Application Hosting

### WedaApplication

The main application host that manages device lifecycle:

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<MyDevice>("ConfigKey");
var app = builder.Build();
await app.RunAsync();
```

### WedaApplicationBuilder

Fluent API for configuring and building a `WedaApplication`:

- `AddDevice<T>()` - Register device types
- `UseCloud()` / `UseMockCloud()` - Configure cloud connectivity
- `Build()` - Create the application instance

### IWedaApplicationContext

Provides access to application-wide services and configuration within device implementations:

- Service provider (DI container)
- Configuration root
- Cloud service
- Logging

## See Also

- [Architecture Overview](./architecture.md) - System design and data flow
- [Sensor Configuration](../04-sensor-configuration/configuration-reference.md) - Detailed configuration options
- [Custom Device Development](../09-customization/01-custom-device.md) - Building custom devices

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

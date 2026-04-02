---
sidebar_position: 2
sidebar_label: 'SubNode Hierarchy'
hide_title: true
title: 'SubNode Hierarchy | SubNode SDK'
keywords: ['SubNode', 'Device', 'Sensor', 'Hierarchy', 'Architecture']
description: 'Understand the three-layer hierarchy of SubNode, Device, and Sensor and their responsibilities.'
---

# SubNode Hierarchy

> Understand the three-layer hierarchy of SubNode, Device, and Sensor and their responsibilities.

## Overview

The core data model of SubNode SDK is a three-layer hierarchy: **SubNode > Device > Sensor**. A SubNode application represents an edge node that can contain multiple Devices, each of which can contain multiple Sensors. Understanding this hierarchy is fundamental to correctly configuring and developing SubNode applications.

## What You'll Learn

After reading this article, you will be able to:

- Understand the hierarchy and responsibilities of SubNode, Device, and Sensor
- Understand how the three layers map to configuration files and code
- Understand the data flow in multi-device scenarios

## Prerequisites

- Completed [Start with Example](../02-getting-started/02-start-with-example.md)
- Understand [Project Structure](./01-project-structure.md)

---

## Three-Layer Hierarchy

```text
┌─────────────────────────────────────────────────────────────────┐
│  SubNode                                                        │
│  (Edge Node - one application instance)                         │
│                                                                 │
│  ┌───────────────────────────┐  ┌───────────────────────────┐   │
│  │  Device A                 │  │  Device B                 │   │
│  │  (e.g. Modbus TCP)        │  │  (e.g. MQTT)              │   │
│  │                           │  │                           │   │
│  │  ┌────────┐ ┌────────┐    │  │  ┌────────┐ ┌────────┐    │   │
│  │  │Sensor 1│ │Sensor 2│    │  │  │Sensor 1│ │Sensor 2│    │   │
│  │  │channel │ │channel │    │  │  │  temp  │ │humidity│    │   │
│  │  │  _0    │ │  _1    │    │  │  │ _zone1 │ │_zone1  │    │   │
│  │  └────────┘ └────────┘    │  │  └────────┘ └────────┘    │   │
│  └───────────────────────────┘  └───────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### SubNode

**SubNode** is the top-level entity representing an edge node application. Its responsibilities are:

- Serve as the identity of the entire application (Name, SubNodeType, Manufacturer, etc.)
- Manage the lifecycle of all Devices underneath (Initialize, Start, Stop)
- Perform cloud registration with WedaCore

Maps to the `SubNode` section in `devicecfg.json`:

```json
{
  "SubNode": {
    "Name": "Wise4012",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SwVersion": "1.0.0"
  }
}
```

### Device

**Device** represents an abstraction of a physical device or data source. Its responsibilities are:

- Handle hardware communication (Communication)
- Handle protocol encoding/decoding (ProtocolParser)
- Manage telemetry reading and scheduling for all Sensors underneath
- Process Commands and Config Updates from the cloud

Maps to each entry in `DeviceConfigs` in `devicecfg.json`:

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": { "Host": "172.16.8.122", "Port": 502 },
      "Properties": { "SlaveId": 1 },
      "Sensors": [ ... ]
    }
  }
}
```

Registered in code via `AddDevice`:

```csharp
builder.AddDevice<MyFirstDevice>("MyFirstDevice");
```

### Sensor

**Sensor** is the lowest-level data point representing a specific measurement channel. Its responsibilities are:

- Define the data source (register address, MQTT topic, etc.)
- Define the data processing pipeline (Transform, DSP Filter, Threshold)
- Define reporting behavior (Interval, Enabled)

Maps to the `Sensors` array within a Device in `devicecfg.json`:

```json
{
  "Sensors": [
    {
      "Name": "channel_0",
      "SensorGroup": "AI",
      "Parameters": {
        "RegisterType": "HoldingRegister",
        "RegisterAddress": 0,
        "RegisterCount": 1,
        "DataType": "UInt16"
      },
      "Report": { "Enabled": true, "Interval": 3000 }
    }
  ]
}
```

---

## Configuration File to Hierarchy Mapping

```text
devicecfg.json
├── "SubNode": { ... }                    ──> SubNode metadata
└── "DeviceConfigs":
    ├── "DeviceA": {                      ──> Device A
    │     "DeviceCommunication": { ... }
    │     "Sensors": [
    │       { "Name": "sensor_1" },       ──> Sensor 1
    │       { "Name": "sensor_2" }        ──> Sensor 2
    │     ]
    │   }
    └── "DeviceB": {                      ──> Device B
          "DeviceCommunication": { ... }
          "Sensors": [
            { "Name": "sensor_1" },       ──> Sensor 1
            { "Name": "sensor_2" }        ──> Sensor 2
          ]
        }
```

Corresponding code:

```csharp
// SubNode level
var builder = WedaApplication.CreateDefaultBuilder(args);

// Device level - each AddDevice maps to a DeviceConfigs key
builder.AddDevice<DeviceA>("DeviceA");
builder.AddDevice<DeviceB>("DeviceB");

// Sensor level - defined in devicecfg.json, accessed via:
// device.Configuration.Sensors
```

---

## Multi-Device Data Flow

Each Device reads data independently; the SubNode Host batches and sends telemetry to the cloud:

```text
┌──────────┐     ┌──────────┐     ┌──────────┐
│ Device A │     │ Device B │     │ Device C │
│ (Modbus) │     │  (MQTT)  │     │  (HTTP)  │
└────┬─────┘     └────┬─────┘     └────┬─────┘
     │                │                │
     │  Telemetry     │  Telemetry     │  Telemetry
     │  Measures      │  Measures      │  Measures
     ▼                ▼                ▼
┌──────────────────────────────────────────────┐
│              SubNode Host                    │
│         (batches and sends to cloud)         │
└──────────────────────┬───────────────────────┘
                       │
                       ▼
                  ┌──────────┐
                  │ WedaNode │
                  └──────────┘
```

- Each Device schedules reads independently based on its own `Interval`
- SubNode Host batches telemetry from all Devices and sends to WedaCore
- Devices can subscribe to each other's data via DeviceRegistry (advanced scenario)

---

## Sensor Naming Rules

Sensor Names must follow IoT DB naming rules:

- Only letters, digits, and underscores allowed
- No dots, spaces, or special characters
- Must start with a letter

| Valid | Invalid |
|-------|---------|
| `channel_0` | `channel.0` |
| `temperature_sensor` | `temperature.sensor` |
| `AI1` | `AI-1` |
| `do_0` | `do.0` |
| `do0` | `_do0` |
| `do0` | `0do` |

---

## Summary

- **SubNode** is the top-level edge node, responsible for identity and lifecycle management
- **Device** represents a device abstraction, responsible for communication, protocol, and Command handling
- **Sensor** is the lowest-level data point, responsible for data source definition and processing pipeline
- The three layers map clearly in `devicecfg.json`: `SubNode` > `DeviceConfigs.{key}` > `Sensors[]`
- Sensor Names only allow letters, digits, and underscores

## See Also

- [Project Structure](./01-project-structure.md) - SDK module responsibilities
- [Sensor Configuration](../04-configuration/02-configuration-via-json.md) - Configure devices and sensors
- [Architecture Overview](../01-introduction/02-architecture.md) - System design and data flow

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |

---
sidebar_position: 2
sidebar_label: 'Configuration via JSON'
hide_title: true
title: 'Configuration via JSON | SubNode SDK'
keywords: ['SubNode', 'Sensor', 'Configuration', 'JSON', 'devicecfg']
description: 'Configure devices and sensors using devicecfg.json.'
---

# Configuration via JSON

> Configure devices and sensors using devicecfg.json.

## Overview

JSON is the most common configuration approach for SubNode and the default for the WedaBuilder template. Through `devicecfg.json`, SAs and developers can define device connections, sensors, data transforms, and reporting behavior without modifying code. This article walks through the structure of `devicecfg.json` layer by layer, using the `wise-4012` example as a practical reference.

## What You'll Learn

After reading this article, you will be able to:

- Understand the complete structure of `devicecfg.json`
- Configure device connections, sensors, and reporting intervals
- Configure SensorInfo, TransformPipeline, and DspPipeline
- Map each field to its purpose using the `wise-4012` example

## Prerequisites

- Completed [Start with Example](../02-getting-started/02-start-with-example.md)
- Understand [SubNode Hierarchy](../03-hierarchy/02-aggregation-overview.md)

---

## devicecfg.json Structure Overview

```text
devicecfg.json
├── "SubNode": { ... }                 ──> SubNode metadata
└── "DeviceConfigs":
    └── "<ConfigKey>": {               ──> Device configuration
          "Enabled": true,
          "Dtdl": { ... },
          "DeviceCommunication": { ... },
          "Properties": { ... },
          "Sensors": [                 ──> Sensor array
            {
              "Name": "...",
              "SensorGroup": "...",
              "Parameters": { ... },
              "SensorInfo": { ... },
              "Report": { ... }
            }
          ]
        }
```

---

## SubNode Section

Defines the edge node identity:

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

| Field | Required | Description |
|-------|----------|-------------|
| `Name` | Yes | SubNode display name |
| `SubNodeType` | Yes | Device type identifier |
| `Manufacturer` | No | Manufacturer |
| `Model` | No | Model |
| `SwVersion` | No | Software version |

---

## DeviceConfigs Section

Each Device is identified by a **config key** that maps to `AddDevice<T>("key")` in `Program.cs`:

```csharp
builder.AddDevice<MyFirstDevice>("MyFirstDevice");
//                                 ^^^^^^^^^^^^^^^^
//                                 maps to DeviceConfigs key
```

### Device Configuration Fields

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "Dtdl": {
        "DtdlPath": "examples/wise-4012/dtdl/wise-4012.json",
        "AutoGenEnabled": true
      },
      "DeviceCommunication": {
        "Host": "172.16.8.122",
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      },
      "Sensors": [ ... ]
    }
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `Enabled` | No | Enable/disable device (default `true`) |
| `Dtdl` | Conditional | DTDL settings (see below) |
| `DeviceCommunication` | Yes | Connection parameters (protocol-dependent) |
| `Properties` | No | Protocol-specific properties |
| `Sensors` | Yes | Sensor definition array |

### Dtdl Settings

```json
{
  "Dtdl": {
    "AutoGenEnabled": true,
    "DtdlPath": "path/to/dtdl.json"
  }
}
```

| Field | Description |
|-------|-------------|
| `AutoGenEnabled` | `true`: Auto-generate DTDL from Sensor definitions, `DtdlPath` is optional |
| | `false`: Use a manually authored DTDL file, `DtdlPath` is **required** |
| `DtdlPath` | Path to DTDL JSON file |

### DeviceCommunication (by protocol)

**Modbus TCP:**

```json
{
  "DeviceCommunication": {
    "Host": "172.16.8.122",
    "Port": 502
  },
  "Properties": {
    "SlaveId": 1
  }
}
```

**ISensing MQTT:**

```json
{
  "DeviceCommunication": {
    "BrokerUrl": "mqtt://172.16.8.122:1883",
    "ClientId": "wise4012-isensing-demo",
    "MacAddress": "00D0C9FAC80E",
    "Manufacturer": "Advantech"
  }
}
```

---

## Sensor Configuration

### Basic Sensor

```json
{
  "Name": "channel_0",
  "SensorGroup": "AI",
  "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
  "Parameters": {
    "RegisterType": "HoldingRegister",
    "RegisterAddress": 0,
    "RegisterCount": 1,
    "DataType": "UInt16"
  },
  "Report": {
    "Enabled": true,
    "Interval": 3000
  }
}
```

### Complete Sensor (with SensorInfo and Transform)

```json
{
  "Name": "temperature_zone1",
  "SensorGroup": "TEMP",
  "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
  "Parameters": {
    "RegisterType": "HoldingRegister",
    "RegisterAddress": 0,
    "RegisterCount": 2,
    "DataType": "Float32"
  },
  "SensorInfo": {
    "DisplayName": "Zone 1 Temperature",
    "Description": "Temperature sensor for production zone 1",
    "Schema": "double"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000,
    "Unit": "celsius",
    "TransformPipeline": [
      {
        "Type": "calibration",
        "Enabled": true,
        "Parameters": {
          "Scale": 0.1,
          "Offset": -10.0
        }
      }
    ],
    "DspPipeline": [
      {
        "Type": "movingAverage",
        "Enabled": true,
        "Parameters": {
          "WindowSize": 5
        }
      }
    ],
    "Thresholds": {
      "LowerWarning": 10,
      "LowerCritical": 5,
      "UpperWarning": 35,
      "UpperCritical": 40
    }
  }
}
```

### Sensor Field Reference

| Field | Required | Description |
|-------|----------|-------------|
| `Name` | Yes | Sensor name (letters, digits, and underscores only) |
| `SensorGroup` | Yes | Category: `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` |
| `Dtmi` | Conditional | Digital Twin Model ID; auto-generated when `AutoGenEnabled=true`, **required** when `false` |
| `Parameters` | Yes | Protocol-specific settings (register address, MQTT topic, etc.) |
| `SensorInfo` | Conditional | DTDL metadata; **required** when `AutoGenEnabled=true` (see below) |
| `Report` | Yes | Reporting settings (Interval, Transform, DSP, Threshold) |

---

## SensorInfo

Defines DTDL metadata for the sensor, used for auto-generating DTDL and cloud registration display.

> When `Dtdl.AutoGenEnabled = true`, the framework needs `SensorInfo` to generate DTDL, making it **required**. When `AutoGenEnabled = false`, DTDL is provided by an external file, and `SensorInfo` is optional.

```json
{
  "SensorInfo": {
    "DisplayName": "AI Channel 0",
    "Description": "Analog input channel 0",
    "Schema": "integer"
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `Schema` | Yes | DTDL Schema type |
| `DisplayName` | No | Human-readable name (derived from Name if not specified, e.g., `channel_0` -> `Channel 0`) |
| `Description` | No | Sensor description |

Schema types: `boolean`, `integer`, `double`, `long`, `string`, `application/json`, `application/octetstream`, `image/jpeg`, `image/png`

---

## Report Configuration

### Basic Settings

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "Unit": "celsius"
  }
}
```

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Enabled` | boolean | `true` | Enable/disable reporting |
| `Interval` | number | `1000` | Sampling interval (milliseconds) |
| `Unit` | string | - | Unit of measurement |

### TransformPipeline

Transforms execute in array order:

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": { "Scale": 0.1, "Offset": -10.0 }
    },
    {
      "Type": "unitconversion",
      "Enabled": true,
      "Parameters": { "FromUnit": "celsius", "ToUnit": "fahrenheit" }
    }
  ]
}
```

### DspPipeline

DSP Filters execute after Transforms:

```json
{
  "DspPipeline": [
    {
      "Type": "movingAverage",
      "Enabled": true,
      "Parameters": { "WindowSize": 5 }
    }
  ]
}
```

### Thresholds

```json
{
  "Thresholds": {
    "LowerWarning": 10,
    "LowerCritical": 5,
    "UpperWarning": 35,
    "UpperCritical": 40
  }
}
```

> For details on Transform and DSP Filter types, see [Data Pipeline](../05-data-pipeline/01-overview.md).

---

## Modbus Parameters Reference

| Parameter | Values | Description |
|-----------|--------|-------------|
| `RegisterType` | `HoldingRegister`, `InputRegister`, `Coil`, `DiscreteInput` | Modbus function code |
| `RegisterAddress` | 0-65535 | Starting register address |
| `RegisterCount` | 1-125 | Number of registers |
| `DataType` | `UInt16`, `Int16`, `UInt32`, `Int32`, `Float32`, `Float64`, `Boolean` | Data type |

## ISensing Parameters Reference

| Parameter | Description |
|-----------|-------------|
| `FieldName` | Field name in ISensing JSON payload (e.g., `ai1`, `do1`) |

---

## Complete Examples

Refer to the following example `devicecfg.json` files:

- `examples/wise-4012/devicecfg.json` - Modbus TCP (AI + DO)
- `examples/wise-4012-isensing/devicecfg.json` - ISensing MQTT
- `examples/power-aggregation/devicecfg.json` - Multi-device aggregation

---

## Summary

- `devicecfg.json` is the primary configuration file for SubNode, defining SubNode identity, device connections, and sensors
- The structure follows the SubNode > DeviceConfigs > Sensors three-layer hierarchy
- Each Sensor can independently configure Parameters (protocol), SensorInfo (DTDL), and Report (interval + pipeline)
- Edit JSON to take effect -- no recompilation required

## See Also

- [Configuration via Code](./01-configuration-via-code.md) - Programmatic configuration approach
- [Configuration Examples](./03-configuration-examples.md) - Complete examples for each protocol
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Transform and DSP Filter details
- [SubNode Hierarchy](../03-hierarchy/02-aggregation-overview.md) - Three-layer structure

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

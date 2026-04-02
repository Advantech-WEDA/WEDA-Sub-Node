---
sidebar_position: 2
sidebar_label: 'Configuration via JSON'
hide_title: true
title: 'Sensor Configuration via JSON'
keywords: ['SubNode', 'Sensor', 'Configuration', 'JSON', 'devicecfg']
description: 'Configure sensors using devicecfg.json configuration file'
---

# Configuration via JSON

> Configure sensors using the devicecfg.json configuration file.

## Overview

JSON-based configuration is the recommended approach for most scenarios:
- No code changes required for sensor updates
- Configuration can be synced from WedaCore
- Easy to understand for Solution Architects
- Supports hot-reload capabilities

## Configuration File Structure

### devicecfg.json

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "YourCompany",
    "Model": "Device-v1",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": true,
      "Dtdl": {
        "DtdlPath": "path/to/dtdl.json",
        "AutoGenEnabled": true
      },
      "DeviceCommunication": {
        "Host": "192.168.1.100",
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      },
      "Periods": {
        "ReadTelemetryInterval": 1000,
        "HealthReportInterval": 30000,
        "TelemetrySendInterval": 5000
      },
      "Sensors": [
        {
          "Name": "temperature",
          "SensorGroup": "TEMP",
          "Dtmi": "dtmi:company:sensor:temperature;1",
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

## SubNode Section

Global SubNode metadata:

```json
{
  "SubNode": {
    "Name": "FactoryMonitor",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SwVersion": "2.1.0"
  }
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `Name` | Yes | SubNode display name |
| `SubNodeType` | Yes | Device type identifier |
| `Manufacturer` | No | Manufacturer name |
| `Model` | No | Device model |
| `SwVersion` | No | Software version |

## DeviceConfigs Section

Each device is defined by a configuration key that maps to `AddDevice<T>("key")`:

```json
{
  "DeviceConfigs": {
    "TemperatureSensor": { ... },
    "PressureSensor": { ... },
    "DataAggregator": { ... }
  }
}
```

### Device Configuration Options

```json
{
  "MyDevice": {
    "Enabled": true,
    "Dtdl": {
      "DtdlPath": "dtdl/device.json",
      "AutoGenEnabled": true
    },
    "DeviceCommunication": {
      "Host": "192.168.1.100",
      "Port": 502,
      "Timeout": 5000,
      "RetryCount": 3
    },
    "ConnectionSettings": {
      "Timeout": 5000,
      "RetryCount": 3,
      "RetryDelay": 1000
    },
    "Properties": {
      "SlaveId": 1,
      "ByteOrder": "BigEndian"
    },
    "Periods": {
      "ReadTelemetryInterval": 1000,
      "HealthReportInterval": 30000,
      "TelemetrySendInterval": 5000
    },
    "Sensors": [ ... ]
  }
}
```

| Section | Description |
|---------|-------------|
| `Enabled` | Enable/disable the device |
| `Dtdl` | Digital Twin Definition Language settings |
| `DeviceCommunication` | Connection parameters (host, port) |
| `ConnectionSettings` | Timeout and retry settings |
| `Properties` | Protocol-specific properties |
| `Periods` | Timing intervals |
| `Sensors` | Array of sensor definitions |

## Sensor Configuration

### Basic Sensor

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
      "Report": {
        "Enabled": true,
        "Interval": 3000
      }
    }
  ]
}
```

### Complete Sensor Definition

```json
{
  "Sensors": [
    {
      "Name": "temperature.zone1",
      "ResourceId": "21af0dc4-5389-a7dd-df64d7cf782c",
      "SensorGroup": "TEMP",
      "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
      "Parameters": {
        "RegisterType": "HoldingRegister",
        "RegisterAddress": 0,
        "RegisterCount": 2,
        "DataType": "Float32"
      },
      "SensorInfo": {
        "Schema": "double",
        "DisplayName": "Zone 1 Temperature",
        "Description": "Temperature sensor for production zone 1"
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
      },
      "Record": {
        "Enabled": true,
        "Path": "recordings/temp_zone1"
      }
    }
  ]
}
```

### Sensor Fields Reference

| Field | Required | Description |
|-------|----------|-------------|
| `Name` | Yes | Sensor identifier |
| `ResourceId` | No | UUID (auto-generated if omitted) |
| `SensorGroup` | Yes | Category: AI, AO, DI, DO, TEMP, PWR, SYS |
| `Dtmi` | No | Digital Twin Model ID |
| `Parameters` | Yes | Protocol-specific settings |
| `SensorInfo` | No | DTDL metadata |
| `Report` | Yes | Reporting configuration |
| `Record` | No | Local recording settings |

## Parameters by Protocol

### Modbus TCP/RTU

```json
{
  "Parameters": {
    "RegisterType": "HoldingRegister",
    "RegisterAddress": 0,
    "RegisterCount": 2,
    "DataType": "Float32"
  }
}
```

| Parameter | Values | Description |
|-----------|--------|-------------|
| `RegisterType` | HoldingRegister, InputRegister, Coil, DiscreteInput | Modbus function |
| `RegisterAddress` | 0-65535 | Starting register |
| `RegisterCount` | 1-125 | Number of registers |
| `DataType` | UInt16, Int16, UInt32, Int32, Float32, Float64 | Data type |

### MQTT

```json
{
  "Parameters": {
    "Topic": "sensors/temperature",
    "Qos": 1,
    "JsonPath": "$.data.value"
  }
}
```

## Report Configuration

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "Unit": "celsius",
    "TransformPipeline": [ ... ],
    "DspPipeline": [ ... ],
    "Thresholds": { ... }
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `Enabled` | boolean | Enable/disable reporting |
| `Interval` | number | Sampling interval in milliseconds |
| `Unit` | string | Unit of measurement |
| `TransformPipeline` | array | Data transforms (calibration, etc.) |
| `DspPipeline` | array | DSP filters (moving average, etc.) |
| `Thresholds` | object | Alert thresholds |

## Transform Pipeline

### Calibration

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": {
        "Scale": 0.1,
        "Offset": -273.15
      }
    }
  ]
}
```

### Unit Conversion

```json
{
  "TransformPipeline": [
    {
      "Type": "unitconversion",
      "Enabled": true,
      "Parameters": {
        "FromUnit": "celsius",
        "ToUnit": "fahrenheit"
      }
    }
  ]
}
```

### Multiple Transforms

Transforms execute in array order:

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": { "Scale": 0.1, "Offset": 0 }
    },
    {
      "Type": "unitconversion",
      "Enabled": true,
      "Parameters": { "FromUnit": "celsius", "ToUnit": "fahrenheit" }
    }
  ]
}
```

## DSP Filters

```json
{
  "DspPipeline": [
    {
      "Type": "movingAverage",
      "Enabled": true,
      "Parameters": {
        "WindowSize": 5
      }
    },
    {
      "Type": "kalman",
      "Enabled": true,
      "Parameters": {
        "ProcessNoise": 0.01,
        "MeasurementNoise": 0.1
      }
    }
  ]
}
```

## Thresholds

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

## Complete Example

See [examples/wise-4012/devicecfg.json](https://github.com/your-repo/edge_subnode/blob/main/examples/wise-4012/devicecfg.json) for a complete working example.

```json
{
  "SubNode": {
    "Name": "Wise4012",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "172.16.8.122",
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      },
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

## See Also

- [Configuration via Code](./configuration-via-code.md) - Programmatic configuration
- [Configuration Reference](./configuration-reference.md) - Complete reference
- [Data Pipeline](../05-data-pipeline/pipeline-overview.md) - Transform details

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

---
sidebar_position: 3
sidebar_label: 'Configuration Examples'
hide_title: true
title: 'Configuration Examples | SubNode SDK'
keywords: ['SubNode', 'Configuration', 'Examples', 'Modbus', 'MQTT', 'HTTP']
description: 'Complete devicecfg.json configuration examples for various protocols and scenarios.'
---

# Configuration Examples

> Complete devicecfg.json configuration examples for various protocols and scenarios.

## Overview

This document compiles `devicecfg.json` configurations for various SubNode SDK examples, covering Modbus TCP, ISensing MQTT, HTTP API, MQTT Image, and multi-device aggregation scenarios. Each example highlights protocol-specific configuration points, making it easy to copy and modify for your use case.

## What You'll Learn

After reading this document, you will be able to:

- Choose the appropriate configuration template for different protocols
- Understand the differences in `DeviceCommunication` and `Parameters` across protocols
- Understand how Schema types correspond to different data formats

## Prerequisites

- Completed [Configuration via JSON](./02-configuration-via-json.md)

---

## Modbus TCP - wise-4012

Industrial I/O module for reading Analog Input and Digital Output.

**Protocol highlights**: `DeviceCommunication` uses `Host` + `Port`, `Properties` specifies `SlaveId`, and `Parameters` defines register addresses and data types.

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
      "Dtdl": {
        "AutoGenEnabled": true
      },
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
          "SensorInfo": {
            "DisplayName": "AI Channel 0",
            "Schema": "integer"
          },
          "Report": {
            "Enabled": true,
            "Interval": 3000
          }
        },
        {
          "Name": "do_0",
          "SensorGroup": "DO",
          "Parameters": {
            "RegisterType": "Coil",
            "RegisterAddress": 16,
            "RegisterCount": 1,
            "DataType": "Boolean"
          },
          "SensorInfo": {
            "DisplayName": "DO Channel 0",
            "Schema": "boolean"
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

> Full file: `examples/wise-4012/devicecfg.json`

---

## ISensing MQTT - wise-4012-isensing

Advantech ISensing protocol, receiving device data via MQTT.

**Protocol highlights**: `DeviceCommunication` uses `BrokerUrl` + `MacAddress`, and `Parameters` uses `FieldName` to map fields in the ISensing JSON payload.

```json
{
  "SubNode": {
    "Name": "MyWiseDevice4012SE",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012SE",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "Dtdl": {
        "AutoGenEnabled": false,
        "DtdlPath": "examples/wise-4012-isensing/dtdl/wise-4012.json"
      },
      "DeviceCommunication": {
        "BrokerUrl": "mqtt://172.16.8.122:1883",
        "ClientId": "wise4012-isensing-demo",
        "MacAddress": "00D0C9FAC80E",
        "Manufacturer": "Advantech"
      },
      "Sensors": [
        {
          "Name": "AI1",
          "SensorGroup": "AI",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "Parameters": {
            "FieldName": "ai1"
          },
          "SensorInfo": {
            "DisplayName": "AI Channel 1",
            "Schema": "double"
          },
          "Report": {
            "Enabled": true,
            "Interval": 3000
          }
        },
        {
          "Name": "DO1",
          "SensorGroup": "DO",
          "Dtmi": "dtmi:advantech:EdgeSync:DO;1",
          "Parameters": {
            "FieldName": "do1"
          },
          "SensorInfo": {
            "DisplayName": "DO Channel 1",
            "Schema": "boolean"
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

> Full file: `examples/wise-4012-isensing/devicecfg.json`

---

## HTTP API - stock-monitor

Periodically fetches real-time data via HTTP API (using the Taiwan stock market as an example).

**Protocol highlights**: `DeviceCommunication` uses `Host` + `Port`, and `Parameters` defines API-specific query parameters. Schema is `double`.

```json
{
  "SubNode": {
    "Name": "MyStockMonitor",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "TWSE",
    "Model": "RealTimeQuote",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "StockMonitorConfig": {
      "Enabled": true,
      "Dtdl": {
        "AutoGenEnabled": true
      },
      "DeviceCommunication": {
        "Host": "mis.twse.com.tw",
        "Port": 443
      },
      "Sensors": [
        {
          "Name": "stock_2330",
          "SensorGroup": "AI",
          "Parameters": {
            "StockCode": "2330",
            "Metrics": "Current,Volume,Open,High,Low,Change,ChangePercent"
          },
          "SensorInfo": {
            "DisplayName": "TSMC",
            "Description": "Stock of TSMC",
            "Schema": "double"
          },
          "Report": {
            "Enabled": true,
            "Interval": 10000
          }
        }
      ]
    }
  }
}
```

> Full file: `examples/stock-monitor/devicecfg.json`

---

## MQTT Image - image-sensor

Receives image data via MQTT, using Chunking Transform for chunked transmission.

**Protocol highlights**: `DeviceCommunication` uses `BrokerUrl`, and `Parameters` specifies the MQTT `Topic`. Schema is `image/png`, combined with the `chunking` Transform to handle large binary data.

```json
{
  "SubNode": {
    "Name": "ImageSensorDemo",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "Advantech",
    "Model": "MNIST-Demo",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "ImageSensorConfig": {
      "Enabled": true,
      "Dtdl": {
        "AutoGenEnabled": true
      },
      "DeviceCommunication": {
        "BrokerUrl": "mqtt://localhost:1883",
        "ClientId": "image-sensor-demo"
      },
      "Sensors": [
        {
          "Name": "MnistCamera",
          "SensorGroup": "SYS",
          "Parameters": {
            "Topic": "sensor/image/mnist"
          },
          "SensorInfo": {
            "DisplayName": "MNIST Image Sensor",
            "Description": "Receives MNIST digit images via MQTT",
            "Schema": "image/png"
          },
          "Report": {
            "Enabled": true,
            "Interval": 30000,
            "TransformPipeline": [
              {
                "Type": "chunking",
                "Enabled": true,
                "Parameters": {
                  "chunkSize": 128
                }
              }
            ]
          }
        }
      ]
    }
  }
}
```

> Full file: `examples/image-sensor/devicecfg.json`

---

## HTTP JSON - air-quality-monitor

Fetches environmental monitoring data in JSON format via HTTP API.

**Protocol highlights**: Schema is `application/json`, and the entire JSON response is sent as a single Telemetry entry. `Interval` is set to 3600000 (1 hour), suitable for low-frequency data.

```json
{
  "SubNode": {
    "Name": "AirQualityMonitor",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "MOENV",
    "Model": "AQX-P-136",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "AirQualityConfig": {
      "Enabled": true,
      "Dtdl": {
        "AutoGenEnabled": true
      },
      "Sensors": [
        {
          "Name": "taipei_air_quality",
          "SensorGroup": "TEMP",
          "SensorInfo": {
            "DisplayName": "Taipei Air Quality",
            "Description": "Air quality monitoring data from MOENV",
            "Schema": "application/json"
          },
          "Report": {
            "Enabled": true,
            "Interval": 3600000
          },
          "Record": {
            "Enabled": true,
            "Interval": 3600000
          }
        }
      ]
    }
  }
}
```

> Full file: `examples/air-quality-monitor/devicecfg.json`

---

## Multi-Device Aggregation - power-aggregation

Combines current and voltage sensors to calculate power. Three devices are defined in a single `devicecfg.json`.

**Protocol highlights**: The Aggregator's `DeviceCommunication` uses `ExternalSources` to specify source devices, and `Parameters` uses the `DeviceName/SensorName` format to map sources.

```json
{
  "SubNode": {
    "Name": "MyPowerAggregator-1",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "Advantech",
    "Model": "Power-Calculator",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "CurrentSensor": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "127.0.0.1",
        "Port": 5020
      },
      "Properties": { "SlaveId": 1 },
      "Sensors": [
        {
          "Name": "current001",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 40001,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "SensorInfo": { "DisplayName": "Current Sensor", "Schema": "double" },
          "Report": { "Enabled": true, "Interval": 3000 }
        }
      ]
    },
    "VoltageSensor": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "127.0.0.1",
        "Port": 5021
      },
      "Properties": { "SlaveId": 1 },
      "Sensors": [
        {
          "Name": "voltage001",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 40001,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "SensorInfo": { "DisplayName": "Voltage Sensor", "Schema": "double" },
          "Report": { "Enabled": true, "Interval": 3000 }
        }
      ]
    },
    "PowerAggregator": {
      "Enabled": true,
      "DeviceCommunication": {
        "AggregatorType": "PowerAggregator",
        "ExternalSources": [
          { "DeviceName": "VoltageSensor", "SensorName": "voltage001" },
          { "DeviceName": "CurrentSensor", "SensorName": "current001" }
        ]
      },
      "Sensors": [
        {
          "Name": "power001",
          "SensorGroup": "PWR",
          "Parameters": {
            "DataType": "Float64",
            "VoltageSource": "VoltageSensor/voltage001",
            "CurrentSource": "CurrentSensor/current001"
          },
          "SensorInfo": { "DisplayName": "Power Aggregator", "Schema": "double" },
          "Report": { "Enabled": true, "Interval": 3000 }
        }
      ]
    }
  }
}
```

> Full file: `examples/power-aggregation/devicecfg.json`

---

## Schema Type Quick Reference

| Schema | Data Format | Example Scenario |
|--------|-------------|------------------|
| `boolean` | true/false | DO, DI |
| `integer` | Integer | Modbus UInt16 raw value |
| `double` | Floating point | Temperature, voltage, current |
| `long` | Long integer | Counters, accumulated values |
| `string` | Text | Device status |
| `application/json` | JSON object | Complete air quality data |
| `application/octetstream` | Binary | Generic binary data |
| `image/png` | PNG image | Image sensor |
| `image/jpeg` | JPEG image | Camera |

---

## Summary

- The `DeviceCommunication` and `Parameters` fields differ across protocols; choose the corresponding configuration based on the protocol
- Modbus TCP uses `Host`/`Port`/`SlaveId` + register addresses
- ISensing MQTT uses `BrokerUrl`/`MacAddress` + `FieldName`
- HTTP API and MQTT Image use custom `Parameters`
- The Schema type determines the data format, affecting DTDL generation and cloud storage methods

## See Also

- [Configuration via JSON](./02-configuration-via-json.md) - Detailed explanation of each field
- [Configuration via Code](./01-configuration-via-code.md) - Programmatic configuration approach
- [SubNode Hierarchy](../03-hierarchy/02-aggregation-overview.md) - Multi-device scenario explanation

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

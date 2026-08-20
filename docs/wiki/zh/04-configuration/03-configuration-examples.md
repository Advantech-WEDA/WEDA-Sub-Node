---
sidebar_position: 3
sidebar_label: '設定範例'
hide_title: true
title: '設定範例 | SubNode SDK'
keywords: ['SubNode', 'Configuration', 'Examples', 'Modbus', 'MQTT', 'HTTP']
description: '各協定和場景的完整 devicecfg.json 設定範例。'
---

# 設定範例

> 各協定和場景的完整 devicecfg.json 設定範例。

## Overview

本文彙整 SubNode SDK 各範例的 `devicecfg.json` 設定，涵蓋 Modbus TCP、ISensing MQTT、HTTP API、MQTT Image 和多裝置聚合等場景。每個範例標注協定特有的設定重點，方便你複製後修改使用。

## What You'll Learn

閱讀本文後，你將能夠：

- 針對不同協定選擇合適的設定範本
- 理解各協定的 `DeviceCommunication` 和 `Parameters` 差異
- 理解 Schema 類型如何對應不同的資料格式

## Prerequisites

- 完成[透過 JSON 設定](./02-configuration-via-json.md)

---

## Modbus TCP - wise-4012

工業 I/O 模組，讀取 Analog Input 和 Digital Output。

**協定重點**：`DeviceCommunication` 使用 `Host` + `Port`，`Properties` 指定 `SlaveId`，`Parameters` 定義暫存器位址和資料類型。

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

> 完整檔案：`examples/modbus-wise4012/devicecfg.json`

---

## ISensing MQTT - wise-4012-isensing

研華 ISensing 協定，透過 MQTT 接收裝置資料。

**協定重點**：`DeviceCommunication` 使用 `BrokerUrl` + `MacAddress`，`Parameters` 使用 `FieldName` 對應 ISensing JSON payload 中的欄位。

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
        "DtdlPath": "examples/mqtt-isensing-wise4012/dtdl/wise-4012.json"
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

> 完整檔案：`examples/mqtt-isensing-wise4012/devicecfg.json`

---

## HTTP API - stock-monitor

透過 HTTP API 定期取得即時資料（以台灣股市為例）。

**協定重點**：`DeviceCommunication` 使用 `Host` + `Port`，`Parameters` 定義 API 特有的查詢參數。Schema 為 `double`。

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

> 完整檔案：`examples/http-stock-quotes/devicecfg.json`

---

## MQTT Image - image-sensor

透過 MQTT 接收影像資料，使用 Chunking Transform 分片傳輸。

**協定重點**：`DeviceCommunication` 使用 `BrokerUrl`，`Parameters` 指定 MQTT `Topic`。Schema 為 `image/png`，搭配 `chunking` Transform 處理大型二進位資料。

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

> 完整檔案：`examples/mqtt-image-chunked/devicecfg.json`

---

## HTTP JSON - air-quality-monitor

透過 HTTP API 取得 JSON 格式的環境監測資料。

**協定重點**：Schema 為 `application/json`，整個 JSON response 作為一筆 Telemetry 傳送。`Interval` 設為 3600000（1 小時），適合低頻資料。

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

> 完整檔案：`examples/http-air-quality/devicecfg.json`

---

## 多裝置聚合 - power-aggregation

結合電流和電壓感測器計算功率。三個裝置在同一個 `devicecfg.json` 中定義。

**協定重點**：Aggregator 的 `DeviceCommunication` 使用 `ExternalSources` 指定資料來源裝置，`Parameters` 使用 `DeviceName/SensorName` 格式映射來源。

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

> 完整檔案：`examples/feature-aggregation/devicecfg.json`

---

## Schema 類型速查

| Schema | 資料格式 | 範例場景 |
|--------|----------|----------|
| `boolean` | true/false | DO、DI |
| `integer` | 整數 | Modbus UInt16 raw value |
| `double` | 浮點數 | 溫度、電壓、電流 |
| `long` | 長整數 | 計數器、累積值 |
| `string` | 文字 | 裝置狀態 |
| `application/json` | JSON 物件 | 空氣品質完整資料 |
| `application/octetstream` | 二進位 | 通用二進位資料 |
| `image/png` | PNG 影像 | 影像感測器 |
| `image/jpeg` | JPEG 影像 | 攝影機 |

---

## Summary

- 各協定的 `DeviceCommunication` 和 `Parameters` 欄位不同，需依協定選擇對應設定
- Modbus TCP 使用 `Host`/`Port`/`SlaveId` + 暫存器位址
- ISensing MQTT 使用 `BrokerUrl`/`MacAddress` + `FieldName`
- HTTP API 和 MQTT Image 使用自訂 `Parameters`
- Schema 類型決定資料格式，影響 DTDL 產生和雲端儲存方式

## See Also

- [透過 JSON 設定](./02-configuration-via-json.md) - 各欄位詳細說明
- [透過程式碼設定](./01-configuration-via-code.md) - Programmatic 設定方式
- [SubNode 階層架構](../03-hierarchy/02-aggregation-overview.md) - 多裝置場景說明

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

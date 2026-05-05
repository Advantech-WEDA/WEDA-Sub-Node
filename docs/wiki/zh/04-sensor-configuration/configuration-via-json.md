---
sidebar_position: 2
sidebar_label: '透過 JSON 設定'
hide_title: true
title: '透過 JSON 設定感測器'
keywords: ['SubNode', 'Sensor', 'Configuration', 'JSON', 'devicecfg']
description: '使用 devicecfg.json 設定檔設定感測器'
---

# 透過 JSON 設定

> 使用 devicecfg.json 設定檔設定感測器。

## 概述

JSON 設定方式是大多數場景的建議做法：
- 更新感測器無需修改程式碼
- 設定可從 WedaCore 同步
- 解決方案架構師易於理解
- 支援熱重載功能

## 設定檔結構

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

## SubNode 區段

全域 SubNode 中繼資料：

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

| 欄位 | 必要 | 說明 |
|------|------|------|
| `Name` | 是 | SubNode 顯示名稱 |
| `SubNodeType` | 是 | 裝置類型識別碼 |
| `Manufacturer` | 否 | 製造商名稱 |
| `Model` | 否 | 裝置型號 |
| `SwVersion` | 否 | 軟體版本 |

## DeviceConfigs 區段

每個裝置由設定金鑰定義，對應到 `AddDevice<T>("key")`：

```json
{
  "DeviceConfigs": {
    "TemperatureSensor": { ... },
    "PressureSensor": { ... },
    "DataAggregator": { ... }
  }
}
```

### 裝置設定選項

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

| 區段 | 說明 |
|------|------|
| `Enabled` | 啟用/停用裝置 |
| `Dtdl` | Digital Twin Definition Language 設定 |
| `DeviceCommunication` | 連線參數（主機、連接埠）|
| `ConnectionSettings` | 逾時和重試設定 |
| `Properties` | 協定特定屬性 |
| `Periods` | 時間間隔 |
| `Sensors` | 感測器定義陣列 |

## 感測器設定

### 基本感測器

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

### 完整感測器定義

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

### 感測器欄位參考

| 欄位 | 必要 | 說明 |
|------|------|------|
| `Name` | 是 | 感測器識別碼 |
| `ResourceId` | 否 | UUID（省略時自動產生）|
| `SensorGroup` | 是 | 類別：AI、AO、DI、DO、TEMP、PWR、SYS |
| `Dtmi` | 否 | Digital Twin Model ID |
| `Parameters` | 是 | 協定特定設定 |
| `SensorInfo` | 否 | DTDL 中繼資料 |
| `Report` | 是 | 報告設定 |
| `Record` | 否 | 本機記錄設定 |

## 各協定的 Parameters

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

| 參數 | 值 | 說明 |
|------|-----|------|
| `RegisterType` | HoldingRegister、InputRegister、Coil、DiscreteInput | Modbus 功能 |
| `RegisterAddress` | 0-65535 | 起始暫存器 |
| `RegisterCount` | 1-125 | 暫存器數量 |
| `DataType` | UInt16、Int16、UInt32、Int32、Float32、Float64 | 資料類型 |

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

## Report 設定

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

| 欄位 | 類型 | 說明 |
|------|------|------|
| `Enabled` | boolean | 啟用/停用報告 |
| `Interval` | number | 取樣間隔（毫秒）|
| `Unit` | string | 量測單位 |
| `TransformPipeline` | array | 資料轉換（校正等）|
| `DspPipeline` | array | DSP 濾波器（移動平均等）|
| `Thresholds` | object | 警示閾值 |

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

### 多個 Transform

Transform 按陣列順序執行：

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

## DSP 濾波器

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

## 閾值

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

## 完整範例

請參閱 [examples/wise-4012/devicecfg.json](https://github.com/your-repo/edge_subnode/blob/main/examples/wise-4012/devicecfg.json) 取得完整可運作範例。

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

## 另請參閱

- [透過程式碼設定](./configuration-via-code.md) - 程式化設定
- [設定參考](./configuration-reference.md) - 完整參考
- [Data Pipeline](../05-data-pipeline/pipeline-overview.md) - Transform 詳情

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

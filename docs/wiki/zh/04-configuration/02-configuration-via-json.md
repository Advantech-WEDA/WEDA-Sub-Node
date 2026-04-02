---
sidebar_position: 2
sidebar_label: '透過 JSON 設定'
hide_title: true
title: '透過 JSON 設定 | SubNode SDK'
keywords: ['SubNode', 'Sensor', 'Configuration', 'JSON', 'devicecfg']
description: '使用 devicecfg.json 設定裝置和感測器。'
---

# 透過 JSON 設定

> 使用 devicecfg.json 設定裝置和感測器。

## Overview

JSON 是 SubNode 最常見的設定方式，也是 WedaBuilder 範本的預設方式。透過 `devicecfg.json`，SA 和開發人員可以在不修改程式碼的情況下定義裝置連線、感測器、資料轉換和報告行為。本文逐層解析 `devicecfg.json` 的結構，並以 `wise-4012` 範例作為實際參考。

## What You'll Learn

閱讀本文後，你將能夠：

- 理解 `devicecfg.json` 的完整結構
- 設定裝置連線、感測器和報告間隔
- 設定 SensorInfo、TransformPipeline 和 DspPipeline
- 對照 `wise-4012` 範例理解各欄位的用途

## Prerequisites

- 完成[使用範例開始](../02-getting-started/02-start-with-example.md)
- 了解 [SubNode 階層架構](../03-hierarchy/02-aggregation-overview.md)

---

## devicecfg.json 結構總覽

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

## SubNode 區段

定義邊緣節點的身份識別：

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

| 欄位 | 必要 | 說明 |
|------|------|------|
| `Name` | 是 | SubNode 顯示名稱 |
| `SubNodeType` | 是 | 裝置類型識別碼 |
| `Manufacturer` | 否 | 製造商 |
| `Model` | 否 | 型號 |
| `SwVersion` | 否 | 軟體版本 |

---

## DeviceConfigs 區段

每個 Device 由一個 **config key** 識別，對應到 `Program.cs` 中的 `AddDevice<T>("key")`：

```csharp
builder.AddDevice<MyFirstDevice>("MyFirstDevice");
//                                 ^^^^^^^^^^^^^^^^
//                                 maps to DeviceConfigs key
```

### Device 設定欄位

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

| 欄位 | 必要 | 說明 |
|------|------|------|
| `Enabled` | 否 | 啟用/停用裝置（預設 `true`）|
| `Dtdl` | 條件 | DTDL 設定（見下方說明）|
| `DeviceCommunication` | 是 | 連線參數（依協定不同）|
| `Properties` | 否 | 協定特定屬性 |
| `Sensors` | 是 | 感測器定義陣列 |

### Dtdl 設定

```json
{
  "Dtdl": {
    "AutoGenEnabled": true,
    "DtdlPath": "path/to/dtdl.json"
  }
}
```

| 欄位 | 說明 |
|------|------|
| `AutoGenEnabled` | `true`：從 Sensor 定義自動產生 DTDL，`DtdlPath` 為 optional |
| | `false`：使用手動撰寫的 DTDL 檔案，`DtdlPath` 為 **required** |
| `DtdlPath` | DTDL JSON 檔案路徑 |

### DeviceCommunication（依協定）

**Modbus TCP：**

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

**ISensing MQTT：**

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

## Sensor 設定

### 基本感測器

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

### 完整感測器（含 SensorInfo 和 Transform）

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

### Sensor 欄位參考

| 欄位 | 必要 | 說明 |
|------|------|------|
| `Name` | 是 | 感測器名稱（僅英文字母、數字、底線）|
| `SensorGroup` | 是 | 類別：`AI`、`AO`、`DI`、`DO`、`TEMP`、`PWR`、`SYS` |
| `Dtmi` | 條件 | Digital Twin Model ID；`AutoGenEnabled=true` 時自動產生，`false` 時為 **required** |
| `Parameters` | 是 | 協定特定設定（暫存器位址、MQTT Topic 等）|
| `SensorInfo` | 條件 | DTDL 中繼資料；`AutoGenEnabled=true` 時為 **required**（見下方說明）|
| `Report` | 是 | 報告設定（Interval、Transform、DSP、Threshold）|

---

## SensorInfo

定義感測器的 DTDL 中繼資料，用於自動產生 DTDL 和雲端註冊顯示。

> 當 `Dtdl.AutoGenEnabled = true` 時，框架需要 `SensorInfo` 來產生 DTDL，此時為 **required**。當 `AutoGenEnabled = false` 時，DTDL 由外部檔案提供，`SensorInfo` 為 optional。

```json
{
  "SensorInfo": {
    "DisplayName": "AI Channel 0",
    "Description": "Analog input channel 0",
    "Schema": "integer"
  }
}
```

| 欄位 | 必要 | 說明 |
|------|------|------|
| `Schema` | 是 | DTDL Schema 類型（未指定時從 SensorGroup 推斷）|
| `DisplayName` | 否 | 人類可讀名稱（未指定時從 Name 轉換，如 `channel_0` → `Channel 0`）|
| `Description` | 否 | 感測器說明 |

Schema 類型：`boolean`、`integer`、`double`、`long`、`string`、`application/json`、`application/octetstream`、`image/jpeg`、`image/png`

---

## Report 設定

### 基本設定

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "Unit": "celsius"
  }
}
```

| 欄位 | 類型 | 預設值 | 說明 |
|------|------|--------|------|
| `Enabled` | boolean | `true` | 啟用/停用報告 |
| `Interval` | number | `1000` | 取樣間隔（毫秒）|
| `Unit` | string | - | 量測單位 |

### TransformPipeline

Transform 按陣列順序執行：

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

DSP Filter 在 Transform 之後執行：

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

> 詳細的 Transform 和 DSP Filter 類型請參閱 [Data Pipeline](../05-data-pipeline/01-overview.md)。

---

## Modbus Parameters 參考

| Parameter | 值 | 說明 |
|-----------|-----|------|
| `RegisterType` | `HoldingRegister`、`InputRegister`、`Coil`、`DiscreteInput` | Modbus 功能碼 |
| `RegisterAddress` | 0-65535 | 起始暫存器位址 |
| `RegisterCount` | 1-125 | 暫存器數量 |
| `DataType` | `UInt16`、`Int16`、`UInt32`、`Int32`、`Float32`、`Float64`、`Boolean` | 資料類型 |

## ISensing Parameters 參考

| Parameter | 說明 |
|-----------|------|
| `FieldName` | ISensing JSON payload 中的欄位名稱（如 `ai1`、`do1`）|

---

## 完整範例

請參閱以下範例的 `devicecfg.json`：

- `examples/wise-4012/devicecfg.json` - Modbus TCP（AI + DO）
- `examples/wise-4012-isensing/devicecfg.json` - ISensing MQTT
- `examples/power-aggregation/devicecfg.json` - 多裝置聚合

---

## Summary

- `devicecfg.json` 是 SubNode 最主要的設定檔，定義 SubNode 身份、裝置連線和感測器
- 結構遵循 SubNode > DeviceConfigs > Sensors 三層階層
- 每個 Sensor 可獨立設定 Parameters（協定）、SensorInfo（DTDL）、Report（間隔 + Pipeline）
- 修改 JSON 即可生效，無需重新編譯

## See Also

- [透過程式碼設定](./01-configuration-via-code.md) - Programmatic 設定方式
- [設定範例](./03-configuration-examples.md) - 各協定的完整設定範例
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Transform 和 DSP Filter 詳情
- [SubNode 階層架構](../03-hierarchy/02-aggregation-overview.md) - 三層結構說明

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

---
sidebar_position: 2
sidebar_label: 'SubNode 階層架構'
hide_title: true
title: 'SubNode 階層架構 | SubNode SDK'
keywords: ['SubNode', 'Device', 'Sensor', 'Hierarchy', 'Architecture']
description: '了解 SubNode、Device、Sensor 三層階層架構的關係與職責。'
---

# SubNode 階層架構

> 了解 SubNode、Device、Sensor 三層階層架構的關係與職責。

## Overview

SubNode SDK 的核心資料模型是三層階層結構：**SubNode > Device > Sensor**。一個 SubNode 應用程式代表一個邊緣節點，底下可以包含多個 Device，每個 Device 又可以包含多個 Sensor。理解這個階層關係是正確設定和開發 SubNode 應用程式的基礎。

## What You'll Learn

閱讀本文後，你將能夠：

- 理解 SubNode、Device、Sensor 的階層關係和各自職責
- 理解三層結構在設定檔和程式碼中的對應
- 理解多裝置場景的資料流

## Prerequisites

- 完成[使用範例開始](../02-getting-started/02-start-with-example.md)
- 了解[專案結構](./01-project-structure.md)

---

## 三層階層架構

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

**SubNode** 是最上層的實體，代表一個邊緣節點應用程式。它的職責是：

- 作為整個應用程式的身份識別（Name、SubNodeType、Manufacturer 等）
- 管理底下所有 Device 的生命週期（Initialize、Start、Stop）
- 與 WedaCore 進行雲端註冊

對應 `devicecfg.json` 的 `SubNode` 區段：

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

**Device** 代表一個實體裝置或資料來源的抽象。它的職責是：

- 負責與硬體的通訊（Communication）
- 負責協定的編碼/解碼（ProtocolParser）
- 管理底下所有 Sensor 的遙測讀取和排程
- 處理雲端下發的 Command 和 Config Update

對應 `devicecfg.json` 的 `DeviceConfigs` 中的每個項目：

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

在程式碼中透過 `AddDevice` 註冊：

```csharp
builder.AddDevice<MyFirstDevice>("MyFirstDevice");
```

### Sensor

**Sensor** 是最底層的資料點，代表一個具體的量測通道。它的職責是：

- 定義資料來源（暫存器位址、MQTT Topic 等）
- 定義資料處理流程（Transform、DSP Filter、Threshold）
- 定義報告行為（Interval、Enabled）

對應 `devicecfg.json` 中 Device 內的 `Sensors` 陣列：

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

## 設定檔與階層的對應

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

程式碼中的對應：

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

## 多裝置資料流

每個 Device 獨立讀取資料，透過 Cloud Service 統一上報：

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

- 各 Device 按照自己的 `Interval` 獨立排程讀取
- SubNode Host 將所有 Device 的遙測資料批次傳送至 WedaCore
- Device 之間可以透過 DeviceRegistry 互相訂閱資料（進階場景）

---

## Sensor 命名規則

Sensor Name 必須符合 IoT DB 命名規則：

- 僅允許英文字母、數字、底線
- 不允許句點、空格、特殊字元
- 只允許英文字母作為開頭

| 合法 | 不合法 |
|------|--------|
| `channel_0` | `channel.0` |
| `temperature_sensor` | `temperature.sensor` |
| `AI1` | `AI-1` |
| `do_0` | `do.0` |
| `do0` | `_do0` |
| `do0` | `0do` |

---

## Summary

- **SubNode** 是最上層的邊緣節點，負責身份識別和生命週期管理
- **Device** 代表一個裝置抽象，負責通訊、協定和 Command 處理
- **Sensor** 是最底層的資料點，負責資料來源定義和處理流程
- 三層結構在 `devicecfg.json` 中有清晰的對應：`SubNode` > `DeviceConfigs.{key}` > `Sensors[]`
- Sensor Name 僅允許英文字母、數字、底線

## See Also

- [專案結構](./01-project-structure.md) - SDK 模組職責
- [感測器設定](../04-configuration/02-configuration-via-json.md) - 設定裝置與感測器
- [架構概述](../01-introduction/02-architecture.md) - 系統設計和資料流程

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |

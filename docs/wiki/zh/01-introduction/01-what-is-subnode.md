---
sidebar_position: 1
sidebar_label: 'SubNode 是什麼？'
hide_title: true
title: 'SubNode 是什麼？ | SubNode SDK 文件'
keywords:
  - SubNode SDK
  - 工業物聯網
  - IoT Edge Device
  - WedaCore
  - .NET IoT SDK
description: '了解 SubNode SDK 如何簡化工業設備的雲端整合。支援多種工業協定，提供資料處理 Pipeline 和遠端控制功能。'
---

# SubNode 是什麼？

> 一個輕量級 .NET SDK，用於建構連接工業設備到 WedaCore 雲端平台的邊緣裝置應用程式。

## Overview

SubNode SDK 是專為工業物聯網（IIoT）場景設計的邊緣裝置開發框架。它解決了將現場設備（PLC、感測器、工控機）連接到雲端平台時的常見挑戰：

- 多種工業協定的整合複雜度
- 資料採集、轉換、過濾的重複開發
- 斷線重連、錯誤處理的可靠性需求
- 雲端雙向通訊的實作成本

SubNode 將這些底層細節抽象化，讓開發者專注於業務邏輯。

---

## 核心功能

| 功能 | 說明 |
|------|------|
| **聚合模型** | SubNode 作為 Aggregation Root，管理 1..N 個 Device，每個 Device 包含 1..N 個 Sensor |
| **裝置通訊** | 內建支援 Modbus TCP/RTU、MQTT、HTTP，並可擴展自訂協定 |
| **資料採集** | 以可設定的間隔讀取感測器遙測資料 |
| **資料處理** | Pipeline 架構進行轉換（校正、單位換算）和濾波（移動平均、Kalman） |
| **雲端整合** | 與 WedaCore 同步，支援遙測上傳和遠端命令 |
| **可靠性** | 內建斷線重連、Circuit Breaker、錯誤重試機制 |

---

## 系統架構

```
┌──────────────────┐        ┌──────────────────┐        ┌──────────────────┐
│  Physical Device │        │     SubNode      │        │    WedaCore      │
│                  │        │   Application    │        │     (Cloud)      │
│  ┌────────────┐  │        │  ┌────────────┐  │        │                  │
│  │    PLC     │  │ Modbus │  │   Device   │  │  NATS  │  Digital Twin    │
│  └────────────┘  │<──────>│  └────────────┘  │<──────>│  Management      │
│  ┌────────────┐  │        │  ┌────────────┐  │        │                  │
│  │   Sensor   │  │  MQTT  │  │  Pipeline  │  │        │  Telemetry       │
│  └────────────┘  │<──────>│  └────────────┘  │        │  Storage         │
│  ┌────────────┐  │        │  ┌────────────┐  │        │                  │
│  │  Custom    │  │ Custom │  │   Cloud    │  │        │  Remote          │
│  │  Device    │  │<──────>│  │  Service   │  │        │  Commands        │
│  └────────────┘  │        │  └────────────┘  │        │                  │
└──────────────────┘        └──────────────────┘        └──────────────────┘
```

**雙向資料流**：

1. **上行（Telemetry）**：Device 讀取 → Pipeline 處理 → Cloud Service 上傳至 WedaCore
2. **下行（Command）**：WedaCore 發送命令 → Cloud Service 接收 → Device 執行控制

---

## 適用對象

### 解決方案架構師

透過 JSON 設定即可完成大部分工作，無需撰寫程式碼：

- **設定驅動**：使用 `devicecfg.json` 定義感測器、轉換規則、報告間隔
- **預建裝置類型**：`TcpModbusDevice`、`MqttDevice` 等現成類別
- **範例庫**：從 `examples/` 目錄的可運作範例開始

### .NET 開發人員

提供乾淨、可擴展的架構：

- **現代 .NET 10**：使用最新平台特性
- **依賴注入**：標準 DI 模式，易於測試
- **介面導向**：`IDevice`、`ICommunication`、`IProtocolParser` 等清晰合約
- **Pipeline 架構**：可組合的 Transform 和 DSP Filter

---

## 支援場景

| 場景 | 範例 | 協定 |
|------|------|------|
| 工業 I/O 模組 | WISE-4012 類比/數位輸入 | Modbus TCP |
| 電力監控 | 多電表資料聚合 | Modbus RTU |
| 環境感測 | 空氣品質監測站 | MQTT |
| 視覺系統 | 影像感測器串流 | MQTT / HTTP |
| 自訂整合 | REST API 資料來源 | HTTP |
| 特殊設備 | 專有協定設備 | Custom Protocol |

---

## 快速範例

最小的 SubNode 應用程式：

**Program.cs**
```csharp
using Weda.SubNode.Host;

var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();
```

**devicecfg.json**
```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "ModbusDevice"
  },
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "192.168.1.100",
        "Port": 502
      },
      "Sensors": [
        {
          "Name": "temperature",
          "SensorGroup": "TEMP",
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

執行後，SubNode 會：
1. 連接到 `192.168.1.100:502` 的 Modbus TCP 裝置
2. 每 3 秒讀取 `temperature` 感測器
3. 將遙測資料上傳到 WedaCore

---

## Summary

- SubNode 是連接工業設備與雲端平台的邊緣裝置 SDK
- 採用聚合模型：SubNode（1）→ Device（N）→ Sensor（N）的階層結構
- 內建支援常見工業協定，並可擴展自訂協定
- 提供 Pipeline 架構處理資料轉換和濾波
- 適合解決方案架構師（JSON 設定）和 .NET 開發人員（程式碼擴展）

---

## See Also

- [架構概述](./02-architecture.md) - 了解系統設計和元件
- [術語表](./03-terminology.md) - 關鍵術語和定義
- [使用範例開始](../02-getting-started/02-start-with-example.md) - 執行第一個 SubNode 應用程式

---

import Revision from '@site/src/components/Revision';

<Revision date="Mar-18, 2026" version="v1.0.0" />

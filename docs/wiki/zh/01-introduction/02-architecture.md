---
sidebar_position: 2
sidebar_label: '架構概述'
hide_title: true
title: '架構概述 | SubNode SDK'
keywords: ['SubNode', 'Architecture', 'Device', 'Pipeline', 'Communication', 'Aggregation Model']
description: 'SubNode SDK 核心元件與資料流程的技術架構概述，包含聚合模型、裝置架構和設定系統。'
---

# 架構概述

> 了解 SubNode SDK 的核心元件和資料流程。

## Overview

本文介紹 SubNode SDK 的技術架構，包含系統分層、聚合模型、裝置內部結構和資料流程。理解這些概念有助於正確設計和擴展 SubNode 應用程式。

## What You'll Learn

閱讀本文後，你將能夠：

- 理解 SubNode 的分層系統架構
- 掌握 SubNode → Device → Sensor 的聚合模型
- 了解裝置內部的元件組成（Communication、Protocol Parser、Pipeline）
- 理解遙測上行和命令下行的資料流程

---

## 系統架構

SubNode 採用分層架構，將通訊、協定解析、資料處理和雲端整合的關注點分離。

```
┌─────────────────────┐    ┌─────────────────────────────────────────────────────────┐    ┌─────────────┐
│  Physical Devices   │    │                    WedaApplication                      │    │  WedaCore   │
│                     │    │                                                         │    │   (Cloud)   │
│  ┌───────────────┐  │    │  ┌─────────────┐    ┌─────────────────────────────────┐ │    │             │
│  │  Modbus PLC   │◀─┼────┼─▶│  Device A   │◀──▶│         Host Services           │ │    │ ┌─────────┐ │
│  └───────────────┘  │    │  │  (Modbus)   │    │  ┌───────────────────────────┐  │ │    │ │ Digital │ │
│                     │    │  └─────────────┘    │  │      CloudService         │◀─┼─┼───▶│ │  Twin   │ │
│  ┌───────────────┐  │    │  ┌─────────────┐    │  │  (Telemetry & Commands)   │  NATS   │ └─────────┘ │
│  │  MQTT Sensor  │◀─┼────┼─▶│  Device B   │◀──▶│  └───────────────────────────┘  │ │    │             │
│  └───────────────┘  │    │  │  (MQTT)     │    │  ┌───────────────────────────┐  │ │    │ ┌─────────┐ │
│                     │    │  └─────────────┘    │  │   DeviceOrchestrator      │  │ │    │ │ IoT DB  │ │
│  ┌───────────────┐  │    │  ┌─────────────┐    │  │  (Lifecycle & Scheduling) │  │ │    │ └─────────┘ │
│  │   HTTP API    │◀─┼────┼─▶│  Device C   │◀──▶│  └───────────────────────────┘  │ │    │             │
│  └───────────────┘  │    │  │  (HTTP)     │    │  ┌───────────────────────────┐  │ │    │ ┌─────────┐ │
│                     │    │  └─────────────┘    │  │    RecordingService       │  │ │    │ │ remote  │ │
│  ┌───────────────┐  │    │  ┌─────────────┐    │  │  (Local Storage & Replay) │  │ │    │ │ commands│ │
│  │ Custom Device │◀─┼────┼─▶│  Device D   │◀──▶│  └───────────────────────────┘  │ │    │ └─────────┘ │
│  └───────────────┘  │    │  │  (Custom)   │    │  ┌───────────────────────────┐  │ │    │             │
│                     │    │  └─────────────┘    │  │   ConfigurationManager    │  │ │    │ ┌─────────┐ │
│                     │    │                     │  │  (Runtime Config Update)  │  │ │    │ │ other   │ │
│                     │    │                     │  └───────────────────────────┘  │ │    │ │ services│ │
│                     │    │                     └─────────────────────────────────┘ │    │ └─────────┘ │
└─────────────────────┘    └─────────────────────────────────────────────────────────┘    └─────────────┘
        Edge                                     SubNode                                       Cloud
```

**關鍵概念**：

- 每個 **Physical Device** 對應一個 **Device Instance**（1:1 映射）
- **WedaApplication** 作為容器，管理多個 Device（1:N 關係）
- **Host Services** 提供共享功能：
  - **CloudService** - 遙測上傳與命令接收
  - **DeviceOrchestrator** - 生命週期與排程管理
  - **RecordingService** - 本地儲存與重播
  - **ConfigurationManager** - 執行期設定更新
- **CloudService** 透過 **NATS** 與 WedaCore 通訊

## 聚合模型（Aggregation Model）

SubNode 採用 Domain-Driven Design（DDD）的聚合模式。**SubNode 是 Aggregation Root**，管理其下的多個 Device 實體。

### SubNode 與 Device 的關係

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        SubNode (Aggregation Root)                           │
│                                                                             │
│  SubNodeId: "subnode-001"                                                   │
│  Name: "FactoryMonitor"                                                     │
│  SubNodeType: "AdamEthernet"                                                │
│                                                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐              │
│  │    Device A     │  │    Device B     │  │    Device C     │              │
│  │  (Modbus PLC)   │  │  (MQTT Sensor)  │  │  (HTTP API)     │              │
│  │                 │  │                 │  │                 │              │
│  │  ┌───────────┐  │  │  ┌───────────┐  │  │  ┌───────────┐  │              │
│  │  │ Sensor 1  │  │  │  │ Sensor 1  │  │  │  │ Sensor 1  │  │              │
│  │  │ Sensor 2  │  │  │  │ Sensor 2  │  │  │  │ Sensor 2  │  │              │
│  │  │ Sensor 3  │  │  │  └───────────┘  │  │  └───────────┘  │              │
│  │  └───────────┘  │  │                 │  │                 │              │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 階層關係

| 層級 | 說明 | 數量關係 |
|------|------|----------|
| **SubNode** | 邊緣應用程式的根實體，向 WedaCore 註冊的單位 | 1 個應用程式 = 1 個 SubNode |
| **Device** | 實體裝置或資料來源的抽象 | 1 個 SubNode 包含 1..N 個 Device |
| **Sensor** | 裝置內的資料點 | 1 個 Device 包含 1..N 個 Sensor |

### 設計原則

1. **統一識別**：SubNode 擁有唯一的 `SubNodeId`，所有 Device 和 Sensor 透過此 ID 向雲端註冊
2. **生命週期管理**：SubNode 負責協調所有 Device 的初始化、啟動、停止
3. **共享服務**：所有 Device 共享同一個 Cloud Service 連線
4. **獨立通訊**：每個 Device 可使用不同的協定連接不同的實體裝置

### 程式碼對應

```csharp
// SubNode 層級設定
var builder = WedaApplication.CreateDefaultBuilder(args);

// 註冊多個 Device（1:N 關係）
builder.AddDevice<TcpModbusDevice>("PlcDevice");      // Device A
builder.AddDevice<MqttDevice>("EnvironmentSensor");   // Device B
builder.AddDevice<HttpDevice>("WeatherApi");          // Device C

var app = builder.Build();
await app.RunAsync();
```

```json
{
  "SubNode": {
    "Name": "FactoryMonitor",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "YourCompany",  // 非必要
    "Model": "SubNode-Template",    // 非必要
    "SwVersion": "1.0.0"            // 非必要
  },
  "DeviceConfigs": {
    "PlcDevice": { ... },           // Device A 設定
    "EnvironmentSensor": { ... },   // Device B 設定
    "WeatherApi": { ... }           // Device C 設定
  }
}
```

---

## 裝置架構

SubNode 中的每個 Device 都遵循一致的內部結構：

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  Device                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                        DeviceConfiguration                          │    │
│  │   (from devicecfg.json: sensors, communication, periods, etc.)      │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐          │
│  │  Communication  │───▶│ Protocol Parser │───▶│    Sensors      │          │
│  │   (Transport)   │    │   (Encoding)    │    │  (Data Points)  │          │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘          │
│          │                       │                     │                    │
│          │                       │                     ▼                    │
│          │                       │         ┌─────────────────────────┐      │
│          │                       └────────▶│    Data Pipeline        │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   Transforms    │    │      │
│          │                                 │  └────────┬────────┘    │      │
│          │                                 │           ▼             │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   DSP Filters   │    │      │
│          │                                 │  └─────────────────┘    │      │
│          │                                 └─────────────────────────┘      │
│          │                                             │                    │
│          │                                             ▼                    │
│          │         ┌─────────────────────────────────────────────────┐      │
│          └────────▶│              DeviceOrchestrator                 │      │
│                    │   (Lifecycle, Scheduling, Event Coordination)   │      │
│                    └─────────────────────────────────────────────────┘      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 核心元件

### 通訊層

通訊層處理傳輸層級的關注點：

| 介面 | 模式 | 使用場景 |
|------|------|----------|
| `IRequestResponseCommunication` | Request/Response | Modbus TCP、HTTP API |
| `IPubSubCommunication` | Publish/Subscribe | MQTT、NATS |
| `IStreamingCommunication` | Bidirectional Stream | WebSocket、gRPC |

### Protocol Parser

Protocol Parser 在原始協定資料和遙測量測之間進行轉換：

```
Raw Data (bytes/registers) ◀──▶ Protocol Parser ◀──▶ TelemetryMeasure
```

內建的 Parser 包括：
- **ModbusProtocolParser** - Modbus 暫存器解譯
- **ISensingProtocolParser** - Advantech ISensing 格式
- **ImageProtocolParser** - 二進位影像資料

### Data Pipeline

資料流經可設定的 Pipeline：

```
Raw Value ──▶ Transform ──▶ DSP Filter ──▶ Final Value
              (Scale,       (Smooth,     
               Offset)       Average)    
```

每個階段都是可選的，且可針對每個感測器進行設定。

### DeviceOrchestrator

Orchestrator 協調裝置操作：

- **生命週期管理** - Initialize、Start、Stop 序列
- **週期性任務** - 遙測讀取、健康報告
- **事件分發** - DataReceived、ConnectionStateChanged 等
- **設定更新** - 設定變更的兩階段提交

## 資料流程

### 遙測流程（裝置到雲端）

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ Physical │    │ Protocol │    │   Data   │    │  Cloud   │    │ WedaCore │
│  Device  │───▶│  Parser  │───▶│ Pipeline │───▶│ Service  │───▶│          │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │                  │               │             │
     │  Raw bytes    │ TelemetryMeasure │  Transformed  │   NATS      │
     │  registers    │  (ResourceId,    │  measures     │  message    │
     │               │   Value, Time)   │               │             │
```

### 命令流程（雲端到裝置）

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│ WedaCore │    │  Cloud   │    │ Command  │    │ Protocol │    │ Physical │
│          │───▶│ Service  │───▶│ Handler  │───▶│  Parser  │───▶│  Device  │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │                │               │              │
     │  NATS         │  DeviceCommand │  Execute      │  Encoded     │
     │  message      │  (Name, Params)│  logic        │  bytes       │
```

## 設定架構

SubNode 使用 **Digital Twin Shadow** 概念管理設定。三個設定檔代表不同的關注範圍（Scope），各自獨立，**沒有優先順序**：

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    Configuration Files (Digital Twin Shadow)                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐  │
│  │   systemcfg.json    │  │   devicecfg.json    │  │   customcfg.json    │  │
│  │   (System Scope)    │  │   (Device Scope)    │  │    (App Scope)      │  │
│  ├─────────────────────┤  ├─────────────────────┤  ├─────────────────────┤  │
│  │                     │  │                     │  │                     │  │
│  │  WedaNode:          │  │  SubNode:           │  │  {Custom Settings}  │  │
│  │    Url              │  │    Name             │  │                     │  │
│  │    AuthStrategy     │  │    SubNodeType      │  │  User-defined       │  │
│  │    Username         │  │    Manufacturer     │  │  key-value pairs    │  │
│  │    Password         │  │    Model            │  │  for application    │  │
│  │    ...              │  │    SwVersion        │  │  business logic     │  │
│  │                     │  │                     │  │                     │  │
│  │                     │  │  DeviceConfigs:     │  │                     │  │
│  │                     │  │    {DeviceName}:    │  │                     │  │
│  │                     │  │      Sensors        │  │                     │  │
│  │                     │  │      Communication  │  │                     │  │
│  │                     │  │      Periods        │  │                     │  │
│  │                     │  │      ...            │  │                     │  │
│  │                     │  │                     │  │                     │  │
│  └─────────────────────┘  └─────────────────────┘  └─────────────────────┘  │
│           │                        │                        │               │
│           ▼                        ▼                        ▼               │
│     SystemConfig             DeviceConfig              CustomConfig         │
│       Section                  Section                   Section            │
│                                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│  Override Sources (can override any config file settings):                  │
│                                                                             │
│  - Environment Variables    SystemConfig:WedaNode:Url="nats://..."          │
│  - Command-line Arguments   --SystemConfig:WedaNode:Url="nats://..."        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 設定檔用途

| 檔案 | Scope | 用途 |
|------|-------|------|
| `systemcfg.json` | System | NATS 連線設定（URL、認證方式、憑證） |
| `devicecfg.json` | Device | SubNode 身份、裝置定義、感測器設定 |
| `customcfg.json` | Application | 應用程式自訂設定（商業邏輯、閾值等） |
| `appsettings.json` | Logging | Serilog 日誌設定（不參與 Digital Twin 同步） |

## 事件系統

SubNode 提供豐富的事件系統，用於監控和整合：

| 事件 | 說明 |
|------|------|
| `DataReceived` | 從裝置接收到原始資料 |
| `DataProcessed` | Pipeline 處理後的資料 |
| `ConnectionStateChanged` | 裝置連線狀態變更 |
| `DeviceStatusChanged` | 狀態轉換 |
| `TelemetrySent` | 遙測已傳送至雲端 |
| `ValueChanged` | 感測器值變更 |
| `ConfigurationUpdateReceived` | 從雲端收到設定更新 |

事件預設為停用以提升效能。僅啟用您需要的事件。詳細說明請參閱[事件系統](../09-customization/02-event-system.md)。

## Summary

- SubNode 採用分層架構，分離通訊、協定解析、資料處理和雲端整合
- 聚合模型：SubNode（1）→ Device（N）→ Sensor（N），SubNode 作為 Aggregation Root
- 每個 Device 包含 Communication、Protocol Parser、Data Pipeline 和 DeviceOrchestrator
- 資料流程分為上行（遙測）和下行（命令）兩個方向
- 設定系統採用 Digital Twin Shadow 概念，三個設定檔（system/device/custom）各管不同 scope

## See Also

- [術語表](./03-terminology.md) - 關鍵術語和定義
- [專案結構](../03-hierarchy/01-project-structure.md) - 檔案和資料夾組織
- [感測器設定](../04-configuration/03-configuration-examples.md) - 詳細設定選項
- [事件系統](../09-customization/02-event-system.md) - 客製化事件處理
---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-23 | Rain Hu | Doc created. |

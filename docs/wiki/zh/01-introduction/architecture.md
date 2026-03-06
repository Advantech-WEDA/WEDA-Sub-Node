---
sidebar_position: 2
sidebar_label: '架構概述'
hide_title: true
title: 'SubNode 架構概述'
keywords: ['SubNode', 'Architecture', 'Device', 'Pipeline', 'Communication']
description: 'SubNode SDK 核心元件與資料流程的技術架構概述'
---

# 架構概述

> 了解 SubNode SDK 的核心元件和資料流程。

## 系統架構

SubNode 採用分層架構，將通訊、協定解析、資料處理和雲端整合的關注點分離。

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  WedaCore                                   │
│                              (Cloud Platform)                               │
└─────────────────────────────────────────────────────────────────────────────┘
                                      ▲
                                      │ NATS
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                                  WedaNode                                   │
│                         (Local Proxy: 127.0.0.1:4224)                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      ▲
                                      │ NATS
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SubNode Application                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                         WedaApplication Host                          │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │  │
│  │  │   Device    │  │   Device    │  │   Device    │  │   Device    │   │  │
│  │  │  Instance   │  │  Instance   │  │  Instance   │  │  Instance   │   │  │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘   │  │
│  │         │                │                │                │          │  │
│  │         v                v                v                v          │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │                      Cloud Service                              │  │  │
│  │  │              (Telemetry, Commands, Configuration)               │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                      ▲
                                      │ Various Protocols
                                      │
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Physical Devices                                 │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐                 │
│  │ Modbus    │  │   MQTT    │  │   HTTP    │  │  Custom   │                 │
│  │  Device   │  │  Broker   │  │   API     │  │ Protocol  │                 │
│  └───────────┘  └───────────┘  └───────────┘  └───────────┘                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 裝置架構

SubNode 中的每個裝置都遵循一致的內部結構：

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
│                                    v                                        │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐          │
│  │  Communication  │───>│ Protocol Parser │───>│    Sensors      │          │
│  │   (Transport)   │    │   (Encoding)    │    │  (Data Points)  │          │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘          │
│          │                       │                      │                   │
│          │                       │                      v                   │
│          │                       │         ┌─────────────────────────┐      │
│          │                       └────────>│    Data Pipeline        │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   Transforms    │    │      │
│          │                                 │  └────────┬────────┘    │      │
│          │                                 │           v             │      │
│          │                                 │  ┌─────────────────┐    │      │
│          │                                 │  │   DSP Filters   │    │      │
│          │                                 │  └─────────────────┘    │      │
│          │                                 └─────────────────────────┘      │
│          │                                              │                   │
│          │                                              v                   │
│          │         ┌─────────────────────────────────────────────────┐      │
│          └────────>│              DeviceOrchestrator                 │      │
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
Raw Data (bytes/registers) <──> Protocol Parser <──> TelemetryMeasure
```

內建的 Parser 包括：
- **ModbusProtocolParser** - Modbus 暫存器解譯
- **ISensingProtocolParser** - Advantech ISensing 格式
- **ImageProtocolParser** - 二進位影像資料

### Data Pipeline

資料流經可設定的 Pipeline：

```
Raw Value ──> Transform ──> DSP Filter ──> Final Value
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
│  Device  │───>│  Parser  │───>│ Pipeline │───>│ Service  │───>│          │
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
│          │───>│ Service  │───>│ Handler  │───>│  Parser  │───>│  Device  │
└──────────┘    └──────────┘    └──────────┘    └──────────┘    └──────────┘
     │               │                │               │              │
     │  NATS         │  DeviceCommand │  Execute      │  Encoded     │
     │  message      │  (Name, Params)│  logic        │  bytes       │
```

## 設定架構

SubNode 使用階層式設定系統：

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Configuration Sources                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Priority (lowest to highest):                                              │
│                                                                             │
│  1. appsettings.json          ─── Logging (Serilog) configuration           │
│                                                                             │
│  2. systemcfg.json            ─── System-level settings (NATS, cloud)       │
│         │                                                                   │
│         └──> "SystemConfig" section                                         │
│                                                                             │
│  3. devicecfg.json            ─── Device definitions and sensors            │
│         │                                                                   │
│         ├──> "SubNode" section (metadata)                                   │
│         └──> "DeviceConfigs" section (per-device settings)                  │
│                                                                             │
│  4. Environment Variables     ─── Override any setting                      │
│                                                                             │
│  5. Command-line Arguments    ─── Highest priority overrides                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

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

事件預設為停用以提升效能。僅啟用您需要的事件。

## 另請參閱

- [術語表](./terminology.md) - 關鍵術語和定義
- [專案結構](../03-project-structure.md) - 檔案和資料夾組織
- [感測器設定](../04-sensor-configuration/configuration-reference.md) - 詳細設定選項

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

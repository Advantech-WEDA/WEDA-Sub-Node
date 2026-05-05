---
sidebar_position: 1
sidebar_label: '專案結構'
hide_title: true
title: '專案結構 | SubNode SDK'
keywords: ['SubNode', 'Project Structure', 'SDK', 'Architecture', 'Module']
description: '了解 SubNode SDK 的專案結構、模組職責和設定檔載入順序。'
---

# 專案結構

> 了解 SubNode SDK 的專案結構、模組職責和設定檔載入順序。

## Overview

SubNode SDK 採用模組化架構，將介面定義、核心實作、預建裝置、應用程式託管和雲端整合分別放在獨立的專案中。本文介紹專案的頂層結構、各 SDK 模組的職責，以及應用程式專案的典型佈局。

## What You'll Learn

閱讀本文後，你將能夠：

- 找到 SDK 原始碼、範例、範本和工具的位置
- 理解各 SDK 模組的職責和相依關係
- 了解設定檔的載入順序和優先權

## Prerequisites

- 完成[使用範例開始](../02-getting-started/02-start-with-example.md)或[使用範本開始](../02-getting-started/03-start-with-template.md)

---

## 專案結構

```text
edge_subnode/
├── src/                    # SDK source code
│   ├── Weda.SubNode.Abstractions/   # Interfaces and contracts
│   ├── Weda.SubNode.Core/           # Core implementations
│   ├── Weda.SubNode.Devices/        # Pre-built device types
│   ├── Weda.SubNode.Host/           # Application hosting
│   ├── Weda.SubNode.Cloud/          # Cloud integration
│   ├── Weda.SubNode.Simulators/     # Test simulators
│   └── Weda.SubNode.WebApi/         # REST API support
├── apps/                   # User applications (created via template)
├── examples/               # Production-ready examples
├── templates/              # dotnet new project templates
├── tools/                  # Development utilities
│   ├── simulator-host/              # Standalone simulator runner
│   ├── nats-check/                  # NATS connection diagnostic
│   └── config-manager/              # Configuration management
├── tests/                  # Unit and integration tests
└── docs/                   # Documentation
```

---

## SDK 模組

### 模組相依關係

```text
┌───────────────────────────────────────────────────────────────────┐
│                        Application Layer                          │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  .Host  (aggregates Cloud + Devices + Core)                 │  │
│  └────────────┬──────────────┬─────────────────────────────────┘  │
│               │              │                                    │
│               │    ┌─────────▼──────────────────────────────┐     │
│               │    │  Optional Packages                     │     │
│               │    │  ┌──────────────┐  ┌────────────────┐  │     │
│               │    │  │  .WebApi     │  │  .Simulators   │  │     │
│               │    │  └──────────────┘  └────────────────┘  │     │
│               │    └────────────────────────────────────────┘     │
│               │                                                   │
├───────────────┼───────────────────────────────────────────────────┤
│               ▼              Ready-to-Use Layer                   │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  .Devices  (pre-built devices, depends on .Core)            │  │
│  │  TcpModbusDevice, MqttDevice, AggregatorDevice ...          │  │
│  └───────────────────────────┬─────────────────────────────────┘  │
│                              │                                    │
├──────────────────────────────┼────────────────────────────────────┤
│                              ▼          Implementation Layer      │
│  ┌──────────────────┐  ┌──────────────────┐                       │
│  │      .Cloud      │  │      .Core       │                       │
│  │  (independent)   │  │ DeviceBase,      │                       │
│  │  NATS, Telemetry │  │ Communication,   │                       │
│  │                  │  │ Protocols,       │                       │
│  │                  │  │ Transforms       │                       │
│  └────────┬─────────┘  └────────┬─────────┘                       │
│           │                     │                                 │
├───────────┼─────────────────────┼─────────────────────────────────┤
│           ▼                     ▼              Contract Layer     │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │              Weda.SubNode.Abstractions                     │   │
│  │              (Interfaces and Contracts)                    │   │
│  └────────────────────────────────────────────────────────────┘   │
└───────────────────────────────────────────────────────────────────┘
```

關鍵設計原則：

- **Host 是聚合者** - 引入 Cloud、Devices、Core，提供 `WedaApplication` 進入點
- **Cloud 與 Core/Devices 互不相依** - 由 Host 負責將它們組合在一起
- **Devices 依賴 Core** - Devices 是組裝好的 ready-to-use 解決方案（Communication + ProtocolParser + DeviceBase）
- **WebApi 和 Simulators 是 optional packages** - 按需引入，不影響核心功能

### Weda.SubNode.Abstractions

SDK 的合約層，定義所有核心介面。其他模組都依賴此專案，但此專案不依賴任何其他 SDK 模組。

| 目錄 | 關鍵類別 | 職責 |
|------|----------|------|
| `Devices/` | `IDevice`, `DeviceConfiguration` | 裝置生命週期和設定 |
| `Communication/` | `ICommunication`, `IRequestResponseCommunication`, `IPubSubCommunication` | 傳輸抽象 |
| `Protocols/` | `IProtocolParser`, `IRequestResponseProtocolParser` | 協定編碼/解碼 |
| `Telemetry/` | `Sensor`, `TelemetryMeasure`, `SensorReport` | 感測器和遙測資料模型 |
| `Transforms/` | `ITelemetryTransform`, `IConfigurableTransform` | 資料轉換介面 |
| `Dsp/` | `IDspFilter` | 數位訊號處理介面 |
| `Commands/` | `ICommand`, `ICommandHandler` | 命令處理介面 |
| `Events/` | `DataReceivedEvent`, `ConnectionStateChangedEvent` | 事件類型 |
| `Context/` | `IWedaApplicationContext` | 應用程式 Context |

### Weda.SubNode.Core

核心實作層，包含 DeviceBase、通訊、協定解析和資料轉換的實作。

| 目錄 | 關鍵類別 | 職責 |
|------|----------|------|
| `Devices/` | `DeviceBase`, `DeviceOrchestrator`, `DeviceInitializer` | 裝置基礎類別和排程 |
| `Communication/` | `TcpCommunication`, `MqttCommunication` | 傳輸實作 |
| `Protocols/Modbus/` | `ModbusProtocolParser`, `ModbusBatchReader` | Modbus 協定解析 |
| `Protocols/ISensing/` | `ISensingProtocolParser` | Advantech ISensing 協定 |
| `Transforms/` | `CalibrationTransform`, `UnitConversionTransform` | 內建 Transform |
| `Dsp/` | `MovingAverageFilter`, `KalmanFilter` | 內建 DSP Filter |

### Weda.SubNode.Devices

預建裝置類別，繼承 `DeviceBase` 並組合特定的 Communication + ProtocolParser。

```text
IDevice
└── DeviceBase
    ├── TcpModbusDevice        # Modbus TCP
    ├── RtuModbusDevice        # Modbus RTU (Serial)
    ├── MqttDevice             # MQTT Pub/Sub
    └── AggregatorDevice       # Multi-source aggregation
```

### Weda.SubNode.Host

應用程式託管層，提供 `WedaApplication` 和 `WedaApplicationBuilder`。

| 類別 | 職責 |
|------|------|
| `WedaApplication` | `CreateDefaultBuilder` / `CreateBuilder` 進入點 |
| `WedaApplicationBuilder` | Fluent API：`AddDevice`、`AddTelemetry`、`UseMockCloud` 等 |
| `WedaApplicationContext` | SubNode 範本使用的應用程式 Context |
| `SubNode` | SubNode 範本的生命週期管理（半自動模式）|

### Weda.SubNode.Cloud

雲端整合，透過 NATS 與 WedaNode/WedaCore 通訊。

| 類別 | 職責 |
|------|------|
| `WedaCloudService` | 實際雲端服務（Telemetry、Command、Config Sync）|
| `MockCloudService` | 開發用模擬雲端 |
| `Cloud` | Factory：`Cloud.Mock()`、`Cloud.Default()`、`Cloud.WithUserPassword()` |

### Weda.SubNode.Simulators

無硬體開發用的模擬器。

| 類別 | 職責 |
|------|------|
| `TcpModbusSimulator` | 模擬 Modbus TCP 裝置 |
| `MqttImageSimulator` | 模擬 MQTT 影像串流 |
| `WebSocketSimulator` | 模擬 WebSocket 串流 |

---

## 應用程式專案結構

使用 `dotnet new wedabuilder` 建立的典型應用程式：

```text
MySubNode/
├── Program.cs                # Application entry point (*)
├── MyFirstDevice.cs          # Custom device implementation (*)
├── devicecfg.json            # Device and sensor configuration (*)
├── systemcfg.json            # WedaNode connection settings
├── customcfg.json            # Custom application settings (reserved)
├── appsettings.json          # Logging (Serilog) configuration
├── Dockerfile                # Container image build
├── docker-compose.yml        # One-command container deployment
└── MySubNode.csproj          # Project file and ProjectReference
```

> 標示 `(*)` 的三個檔案是核心。詳細說明請參閱[使用範例開始](../02-getting-started/02-start-with-example.md)。

---

## 設定檔載入機制

各設定檔負責不同的 Section，彼此獨立，不互相覆蓋：

```text
┌───────────────────────────────────────────────────────────────────────┐
│                    Configuration Sources                              │
├───────────────────────────────────────────────────────────────────────┤
│                                                                       │
│  JSON Files (each maps to its own section)                            │
│  ┌─────────────────────┐  ┌────────────────────────────────────────┐  │
│  │  appsettings.json   │  │  appsettings.{Environment}.json        │  │
│  │  ──> Root           │  │  ──> Root (override)                   │  │
│  └─────────────────────┘  └────────────────────────────────────────┘  │
│  ┌─────────────────────┐  ┌───────────────────┐  ┌─────────────────┐  │
│  │  systemcfg.json     │  │ devicecfg.json    │  │ customcfg.json  │  │
│  │  ──> SystemConfig   │  │ ──> DeviceConfig  │  │ ──>CustomConfig │  │
│  │      :WedaNode      │  │    :SubNode       │  │                 │  │
│  │                     │  │    :DeviceConfigs │  │                 │  │
│  └─────────────────────┘  └───────────────────┘  └─────────────────┘  │
│                                                                       │
│  Overrides (can override ANY section, highest priority)               │
│  ┌─────────────────────┐  ┌────────────────────────────────────────┐  │
│  │ Environment vars    │  │ Command-line arguments                 │  │
│  │ (e.g. SystemConfig  │  │ (e.g. --SystemConfig:WedaNode:         │  │
│  │  __WedaNode__Url)   │  │        Url=127.0.0.1:4224)             │  │
│  └─────────────────────┘  └────────────────────────────────────────┘  │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

> 環境變數和命令列參數可覆蓋任何 Section 的設定，命令列參數優先權最高。

| 檔案 | 載入到 Section | 雲端同步 | 用途 |
|------|----------------|----------|------|
| `appsettings.json` | Root | 否 | Logging、Simulator 設定 |
| `systemcfg.json` | `SystemConfig` | 否 | WedaNode 連線設定 |
| `devicecfg.json` | `DeviceConfig` | 是 | 裝置定義、感測器 |
| `customcfg.json` | `CustomConfig` | 是 | 自訂應用程式設定 |

---

## Summary

- SDK 分為 7 個模組：Abstractions（合約）、Core（實作）、Devices（預建裝置）、Host（託管）、Cloud（雲端）、Simulators（模擬）、WebApi（REST）
- 所有模組都依賴 Abstractions，形成清晰的分層架構
- 應用程式核心三檔案：`Program.cs`、`MyFirstDevice.cs`、`devicecfg.json`
- 設定檔有明確的載入順序和 Section 對應，命令列參數優先權最高

## See Also

- [架構概述](../01-introduction/02-architecture.md) - 系統設計和資料流程
- [使用範本開始](../02-getting-started/03-start-with-template.md) - 建立新專案
- [感測器設定](../04-configuration/02-configuration-via-json.md) - 設定裝置與感測器

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |

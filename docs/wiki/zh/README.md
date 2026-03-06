---
sidebar_position: 0
sidebar_label: '首頁'
hide_title: true
title: 'SubNode SDK 文件'
keywords: ['SubNode', 'SDK', 'Documentation', 'IoT', 'Edge Device']
description: 'SubNode SDK 官方文件 - 基於 .NET 的工業物聯網邊緣裝置 SDK'
---

# SubNode SDK 文件

> 一個輕量級 .NET SDK，用於建構連接工業設備到 WedaCore 雲端平台的邊緣裝置應用程式。

## 快速導覽

### 快速開始

SubNode 新手？從這裡開始：

| 指南 | 說明 |
|------|------|
| [SubNode 是什麼？](./01-introduction/what-is-subnode.md) | 概述和功能 |
| [環境準備](./02-getting-started/prerequisites.md) | 開發環境設定 |
| [使用範例開始](./02-getting-started/start-with-example.md) | 執行您的第一個 SubNode 應用程式 |
| [使用範本開始](./02-getting-started/start-with-template.md) | 建立新專案 |

### 核心概念

了解基礎知識：

| 主題 | 說明 |
|------|------|
| [架構概述](./01-introduction/architecture.md) | 系統設計和元件 |
| [術語表](./01-introduction/terminology.md) | 關鍵術語和定義 |
| [專案結構](./03-architecture/project-structure.md) | SDK 組織 |

### 設定

設定您的裝置和感測器：

| 指南 | 說明 |
|------|------|
| [透過 JSON 設定](./04-sensor-configuration/configuration-via-json.md) | JSON 設定方式（建議）|
| [透過程式碼設定](./04-sensor-configuration/configuration-via-code.md) | 程式化設定 |
| [設定參考](./04-sensor-configuration/configuration-reference.md) | 完整選項參考 |

### 資料處理

處理和轉換感測器資料：

| 主題 | 說明 |
|------|------|
| [Pipeline 概述](./05-data-pipeline/pipeline-overview.md) | 資料處理架構 |

### 雲端整合

連接到 WedaCore：

| 指南 | 說明 |
|------|------|
| [連接到 WedaCore](./02-getting-started/connect-to-wedacore.md) | 雲端連線設定 |

## 文件結構

```
docs/wiki/zh/
├── 01-introduction/          # SubNode 是什麼、架構、術語
├── 02-getting-started/       # 環境準備、範例、範本、雲端
├── 03-architecture/          # 專案結構、SDK 模組
├── 04-sensor-configuration/  # JSON、程式碼、參考
├── 05-data-pipeline/         # Transform、DSP 濾波器
├── 06-remote-control/        # 命令、設定（即將推出）
├── 07-custom-device/         # ICommunication、IProtocolParser（即將推出）
├── 08-connection-settings/   # WedaNode 憑證（即將推出）
└── 09-troubleshooting/       # FAQ、常見問題（即將推出）
```

## 目標讀者

本文件專為以下讀者設計：

| 讀者 | 重點領域 |
|------|----------|
| **解決方案架構師** | JSON 設定、範例、雲端整合 |
| **.NET 開發人員** | 程式碼設定、自訂裝置、API 參考 |

## 快速範例

最小的 SubNode 應用程式：

```csharp
using Weda.SubNode.Host;

var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();
```

搭配 `devicecfg.json`：

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice"
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

## 範例

探索 `examples/` 目錄中的可直接使用範例：

| 範例 | 說明 | 協定 |
|------|------|------|
| [wise-4012](https://github.com/example/edge_subnode/tree/main/examples/wise-4012) | 工業 I/O 模組 | Modbus TCP |
| [power-aggregation](https://github.com/example/edge_subnode/tree/main/examples/power-aggregation) | 多裝置聚合 | 多來源 |
| [stock-monitor](https://github.com/example/edge_subnode/tree/main/examples/stock-monitor) | HTTP API 整合 | HTTP |
| [image-sensor](https://github.com/example/edge_subnode/tree/main/examples/image-sensor) | 影像串流 | MQTT |

## 版本

本文件適用於 SubNode SDK v1.0.0。

## 語言

- [English](../en/README.md)
- **繁體中文**（目前）

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

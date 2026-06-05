---
sidebar_position: 1
sidebar_label: 'SubNode 是什麼？'
hide_title: true
title: 'SubNode 是什麼？ - Edge Device SDK 概述'
keywords: ['SubNode', 'Edge SDK', 'IoT', 'Device Integration', 'WedaCore']
description: 'SubNode SDK 介紹 - 基於 .NET 的工業物聯網邊緣設備 SDK'
---

# SubNode 是什麼？

> 一個輕量級 .NET SDK，用於建構連接工業設備到 WedaCore 雲端平台的邊緣裝置應用程式。

## 概述

SubNode 是專為工業物聯網場景設計的邊緣裝置 SDK。它提供標準化框架，用於：

- **裝置通訊** - 透過 Modbus TCP/RTU、MQTT、HTTP 及自訂協定連接各種工業設備
- **資料採集** - 以可設定的間隔和批次讀取感測器遙測資料
- **資料處理** - 透過 Pipeline 架構進行資料轉換和過濾
- **雲端整合** - 與 WedaCore 無縫同步，實現數位分身管理
- **遠端控制** - 從雲端執行命令和更新設定

## 為什麼選擇 SubNode？

### 適合解決方案架構師 (SA)

SubNode 簡化了邊緣裝置整合，無需深入的程式設計知識：

- **設定驅動** - 透過 JSON 檔案定義感測器、轉換和報告
- **預建裝置類型** - 使用現成的裝置類別處理常見協定（Modbus、MQTT）
- **Device Activator** - GUI 安裝精靈處理部署
- **範例庫** - 從可運作的範例開始，根據需求自訂

### 適合 .NET 開發人員

SubNode 提供乾淨、可擴展的架構：

- **現代 .NET 9.0** - 基於最新的 .NET 平台建構
- **依賴注入** - 全程使用標準 DI 模式
- **介面導向設計** - 清晰的自訂合約
- **Pipeline 架構** - 可組合的資料轉換
- **事件驅動** - 豐富的事件系統，用於監控和整合

## 核心能力

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              SubNode SDK                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐              │
│   │   Device     │    │    Data      │    │    Cloud     │              │
│   │Communication │───>│  Pipeline    │───>│ Integration  │              │
│   └──────────────┘    └──────────────┘    └──────────────┘              │
│         │                    │                    │                     │
│         v                    v                    v                     │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐              │
│   │ Modbus TCP   │    │ Transforms   │    │  WedaCore    │              │
│   │ Modbus RTU   │    │ DSP Filters  │    │  Telemetry   │              │
│   │ MQTT         │    │ Thresholds   │    │  Commands    │              │
│   │ HTTP         │    │ Calibration  │    │  Config Sync │              │
│   │ Custom       │    │              │    │              │              │
│   └──────────────┘    └──────────────┘    └──────────────┘              │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

## 支援場景

| 場景 | 範例 | 協定 |
|------|------|------|
| 工業 I/O | WISE-4012 類比/數位輸入 | Modbus TCP |
| 電力監控 | 多電表資料聚合 | Modbus RTU |
| 環境感測 | 空氣品質監測 | MQTT |
| 視覺系統 | 影像感測器串流 | MQTT/HTTP |
| 自訂整合 | 股票市場資料饋送 | HTTP API |

## 快速範例

一個最小的 SubNode 應用程式只需要幾行程式碼：

```csharp
using Weda.SubNode.Host;

var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();
```

搭配 `devicecfg.json` 設定檔，即可建立功能完整的邊緣裝置：

1. 連接到 Modbus TCP 裝置
2. 以指定間隔讀取已設定的感測器
3. 透過 Pipeline 轉換資料
4. 向 WedaCore 報告遙測資料

請參閱[快速開始](../02-getting-started/start-with-example.md)指南，開始建構您的第一個 SubNode 應用程式。

## 下一步

- [架構概述](./architecture.md) - 了解系統設計
- [術語表](./terminology.md) - 學習關鍵概念和術語
- [快速開始](../02-getting-started/start-with-example.md) - 建構您的第一個應用程式

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

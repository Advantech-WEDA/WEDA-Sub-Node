---
title: Weda SubNode SDK 簡介
category: overview
order: 1
parent: null
related:
  - path: how_to_start.md
    title: 如何開始
  - path: architecture_overview.md
    title: 架構概述
  - path: modbus_scanner.md
    title: Modbus 掃描器
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Weda SubNode SDK 簡介 - 完整的 .NET IoT 邊緣設備 SDK
tags: [簡介, 概述, 入門]
---

# Weda SubNode SDK 簡介

## 什麼是 Weda SubNode SDK?

**Weda SubNode SDK** 是一個專為 IoT 邊緣設備開發的 .NET SDK，提供完整的設備連接、資料採集、處理與雲端整合解決方案。

## 核心特性

### 多協定支援
- 內建工業通訊協定支援
- **Modbus TCP/RTU**: 完整實作,包含自動探測掃描器
- **MQTT**: 支援發布/訂閱模式
- 可擴展的協定框架,支援自訂協定

### 靈活的資料處理管道
- **資料校準**: 支援線性與非線性校準
- **單位轉換**: 溫度、壓力等單位轉換
- **DSP 濾波**: Kalman 濾波器、移動平均及自訂濾波器
- **轉換管道**: 可組合的資料轉換工作流程

### 事件驅動架構
- 完整的設備生命週期事件系統
- 透過事件訂閱輕鬆擴展業務邏輯
- 即時資料處理與監控

### 雲端整合
- 無縫整合 Weda EdgeSync Cloud 平台
- 基於 NATS 的訊息傳遞,確保可靠通訊
- 設備註冊與配置管理
- Telemetry 上傳與健康報告

### 簡單易用
- ASP.NET Core 風格的 Host 框架
- 基於配置的設備設定
- 常見場景只需少量程式碼
- 豐富的 API 支援進階使用

## 適用場景

### 工業物聯網資料採集
使用標準協定(如 Modbus)從工業設備(如 PLC、感測器、執行器)採集資料。

### 邊緣運算
在邊緣處理和分析資料後再傳送到雲端,降低延遲和頻寬使用。

### 設備健康監測
即時監控設備健康狀態、連線狀態和效能指標。

### 多設備管理
平行管理多個設備,各自擁有獨立的生命週期和配置。

### 資料品質控制
在上傳雲端前套用校準、濾波和驗證,確保資料品質。

## 架構概述

SDK 採用分層架構:

```
┌─────────────────────────────────────────┐
│       Host Framework                    │
│  - WedaApplication                      │
│  - Configuration Management             │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│       Device Layer                      │
│  - TcpModbusDevice                      │
│  - Custom Devices                       │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│       Core Layer                        │
│  - DeviceBase                           │
│  - ModbusDevice                         │
│  - Transforms & Filters                 │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│       Abstractions                      │
│  - IDevice                              │
│  - ICommunication                       │
│  - ITelemetryTransform                  │
└─────────────────────────────────────────┘
```

## 快速範例

這是一個最簡單的入門範例:

```csharp
using Weda.SubNode.Host;

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
```

在 `appsettings.json` 中配置:

```json
{
  "Devices": [
    {
      "DeviceName": "My Modbus Device",
      "DeviceType": "ModbusTCP",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Parameters": {
            "RegisterAddress": 0,
            "DataType": "Float32"
          }
        }
      ]
    }
  ]
}
```

就這樣! SDK 會自動:
- 連接到 Modbus 設備
- 定期讀取感測器資料
- 套用校準和濾波
- 上傳 telemetry 到雲端
- 報告設備健康狀態


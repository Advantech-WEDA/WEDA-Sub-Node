---
title: 如何開始
category: guide
order: 1
parent: null
related:
  - path: introduction.md
    title: 簡介
  - path: modbus_scanner.md
    title: Modbus 掃描器
  - path: architecture_overview.md
    title: 架構概述
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Weda SubNode SDK 快速入門指南 - 安裝與第一步
tags: [快速開始, 安裝, 教學, 入門]
---

# Weda SubNode SDK 快速入門

本指南將協助您在幾分鐘內開始使用 Weda SubNode SDK。

## 前置需求

- **.NET 9.0 SDK** 或更新版本
- C# 和 .NET 基礎知識
- 用於測試的 Modbus 設備(實體或模擬器)

## 安裝

### 方法一: 使用專案範本(推薦)

安裝 Weda SubNode 專案範本:

```bash
dotnet new install Weda.SubNode.Templates
```

建立新專案:

```bash
dotnet new wedaapi -n MyIoTApp
cd MyIoTApp
```

### 方法二: 手動安裝 NuGet 套件

在現有專案中加入必要的 NuGet 套件:

```bash
dotnet add package Weda.SubNode.Host
dotnet add package Weda.SubNode.Devices
dotnet add package Weda.SubNode.Cloud
```

## 快速開始(5 分鐘)

### 步驟 1: 配置您的設備

編輯 `appsettings.json`:

```json
{
  "Devices": [
    {
      "DeviceName": "我的第一個 Modbus 設備",
      "DeviceType": "ModbusTCP",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502,
        "ProtocolType": "modbus"
      },
      "Properties": {
        "SlaveId": 1
      },
      "Periods": {
        "ReadTelemetry": 2000,
        "SendTelemetry": 5000,
        "ReportHealth": 60000
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          }
        },
        {
          "Name": "humidity",
          "Dtmi": "dtmi:advantech:EdgeSync:Humidity;1",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 2,
            "RegisterCount": 2,
            "DataType": "Float32"
          }
        }
      ]
    }
  ]
}
```

**配置說明:**
- **DeviceName**: 設備的友善名稱
- **Communication.Host**: Modbus 設備的 IP 位址
- **Communication.Port**: Modbus TCP 埠號(預設: 502)
- **Properties.SlaveId**: Modbus 從站 ID
- **Periods.ReadTelemetry**: 讀取設備的頻率(毫秒)
- **Periods.SendTelemetry**: 傳送到雲端的頻率(毫秒)
- **Sensors**: 定義要讀取哪些暫存器

### 步驟 2: 撰寫應用程式程式碼

編輯 `Program.cs`:

```csharp
using Weda.SubNode.Host;

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
```

就這樣! 只需要 3 行程式碼。

### 步驟 3: 執行應用程式

```bash
dotnet run
```

您應該會看到類似以下的輸出:

```
[12:00:00 INF] Starting Weda SubNode Application...
[12:00:01 INF] Connecting to device: 我的第一個 Modbus 設備
[12:00:01 INF] Device connected successfully
[12:00:01 INF] Reading telemetry...
[12:00:01 INF] Temperature: 25.3°C
[12:00:01 INF] Humidity: 60.5%
[12:00:01 INF] Telemetry sent to cloud
```

## 不知道設備配置?

使用 **Modbus 掃描器**自動探測設備的暫存器:

```csharp
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Modbus;

var config = new DeviceConfiguration
{
    DeviceName = "Scanner",
    DeviceType = "ModbusTCP",
    Communication = new Dictionary<string, object>
    {
        ["Host"] = "192.168.1.100",
        ["Port"] = 502,
        ["SlaveId"] = 1
    },
    Sensors = []  // 空的 - 我們將透過掃描來發現
};

var communication = WedaFactory.Communication.Tcp.Create("192.168.1.100", 502);
var device = new ModbusDevice(config, communication, WedaFactory.Cloud.Null);
await device.InitializeAsync();

// 掃描暫存器
var results = await device.ScanRegistersAsync();

// 生成感測器配置
var suggestions = device.GenerateSensorSuggestions(results);
```

詳見 [Modbus 掃描器指南](modbus/modbus_scanner.md)。


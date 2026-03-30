---
sidebar_position: 3
sidebar_label: '使用範本開始'
hide_title: true
title: '使用範本開始 | SubNode SDK'
keywords: ['SubNode', 'Template', 'WedaBuilder', 'Project Creation']
description: '使用專案範本從頭建立新的 SubNode 專案。'
---

# 使用範本開始

> 使用專案範本從頭建立新的 SubNode 專案。

## Overview

SubNode SDK 提供兩種專案範本，讓你快速建立可執行的邊緣裝置應用程式。本文介紹兩者的差異、操作步驟，以及如何在範本基礎上自訂感測器和資料轉換。

## What You'll Learn

閱讀本文後，你將能夠：

- 使用 WedaBuilder 範本建立新專案並執行
- 理解 WedaBuilder 和 SubNode 兩種範本的差異
- 透過 JSON 或程式碼新增感測器和資料轉換

## Prerequisites

- 完成[環境準備](./01-prerequisites.md)

---

## 可用範本

| 範本 | 名稱 | 說明 |
|------|------|------|
| **wedabuilder** | WedaBuilder | Builder 模式，使用 Fluent API（建議使用）|
| **subnode** | WedaSubNode | 傳統 SubNode 模式，具有明確的生命週期控制 |

---

## 使用 WedaBuilder 範本（建議）

`wedabuilder` 範本使用現代 Builder 模式，具有自動生命週期管理。

### 步驟 1：安裝範本

先將 SubNode 範本安裝到 `dotnet new` 範本清單中：

```bash
# In repository root
dotnet new install templates/wedabuilder
```

驗證安裝成功：

```bash
dotnet new list weda
```

```text
Template Name                          Short Name     Language
-------------------------------------  -------------  --------
Weda SubNode Builder Application       wedabuilder    [C#]
```

### 步驟 2：建立新專案

在 `apps/` 目錄下建立新專案：

```bash
mkdir -p apps && cd apps
dotnet new wedabuilder -n MySubNode
cd MySubNode
```

> **Note**: 範本透過 `ProjectReference` 參考 SDK 原始碼（如 `../../src/Weda.SubNode.Host`），因此新專案須建立在儲存庫的 `apps/` 目錄下。

### 步驟 3：檢視專案結構

```text
MySubNode/
├── Program.cs                # Application entry point (*)
├── MyFirstDevice.cs          # Custom device implementation (*)
├── devicecfg.json            # Device and sensor configuration (*)
├── systemcfg.json            # WedaNode connection settings
├── customcfg.json            # Custom application settings (reserved)
├── appsettings.json          # Logging (Serilog) and simulator configuration
├── Dockerfile                # Container image build
├── docker-compose.yml        # One-command container deployment
└── MySubNode.csproj          # Project file and NuGet/ProjectReference
```

> **Quick-start 重點**：標示 `(*)` 的三個檔案是核心，與 [wise-4012 範例](./02-start-with-example.md) 的結構一致。`appsettings.json` 額外包含 Simulator 設定。

### 步驟 4：理解程式碼

**Program.cs：**

```csharp
using Weda.SubNode.Host;
using WedaBuilder;

var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();       // Use mock server (change for production)

builder.AddDevice<MyFirstDevice>("MyFirstDevice");

var app = builder.Build();
await app.RunAsync();
```

**MyFirstDevice.cs：**

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("Received: {ResourceId} = {Value}",
                measure.ResourceId, measure.Value);
        }
    }
}
```

### 步驟 5：設定裝置

編輯 `devicecfg.json`：

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice",
    "Manufacturer": "YourCompany",
    "Model": "MyDevice-v1",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "127.0.0.1",
        "Port": 5020
      },
      "Properties": {
        "SlaveId": 1
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
            "Interval": 5000
          }
        }
      ]
    }
  }
}
```

### 步驟 6：使用 Simulator 執行

範本包含會自動啟動的 Modbus Simulator（設定在 `appsettings.json`）：

```bash
dotnet run
```

**預期輸出：**

```text
[12:00:00 INF] Modbus simulator started on 127.0.0.1:5020
[12:00:01 INF] SubNode started. Press Ctrl+C to stop...
[12:00:06 INF] Received: abc12 = 25.3
[12:00:11 INF] Received: abc12 = 25.5
...
```

---

## 使用 SubNode 範本

`subnode` 範本提供明確的生命週期控制，以程式碼定義裝置設定。

### 程式碼概覽

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Cloud;

// Create context with mock cloud
using var context = new WedaApplicationContext(
    options => options.CloudService = Cloud.Mock());

// Create SubNode with explicit lifecycle
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyFirstDevice(context, ConfigureDeviceConfiguration()));

await subNode.InitializeAsync();
await subNode.StartAsync();

Console.WriteLine("SubNode started. Press Ctrl+C to stop...");
await Task.Delay(Timeout.Infinite);
```

### 透過程式碼設定感測器

```csharp
TcpModbusDeviceConfiguration ConfigureDeviceConfiguration()
{
    var modbusConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",
        Manufacturer = "YourCompany",
        Model = "CustomDevice-v1",
        Host = "127.0.0.1",
        Port = 5020,
        SlaveId = 1
    };

    var tempSensor = new ModbusSensorReporturation
    {
        Name = "temperature_sensor",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };
    tempSensor.Config.Interval = 1000;

    modbusConfig.AddSensor(tempSensor);

    return modbusConfig;
}
```

### 兩種範本的差異

| 面向 | WedaBuilder (recommended) | SubNode |
|------|---------------------------|---------|
| 設定方式 | JSON + programmatic 皆可 | JSON + programmatic 皆可 |
| 生命週期 | 全自動（Host 管理） | 半自動（`StartAsync` 後自動，但可手動呼叫單次操作）|
| Host 功能 | 完整（Telemetry + Command + Config Sync）| Telemetry 為主 |
| 裝置存取 | 透過事件和 DI | 可直接操作 `subNode.Devices[0].ReadTelemetryAsync()` |
| 適用於 | 大多數使用案例 | 需要精細控制或單次操作的場景 |

---

## 自訂範本

### 新增更多感測器

在 `devicecfg.json` 的 `Sensors` 陣列中新增項目：

```json
{
  "Sensors": [
    {
      "Name": "temperature",
      "SensorGroup": "TEMP",
      "Parameters": {
        "RegisterType": "HoldingRegister",
        "RegisterAddress": 0,
        "RegisterCount": 2,
        "DataType": "Float32"
      }
    },
    {
      "Name": "humidity",
      "SensorGroup": "AI",
      "Parameters": {
        "RegisterType": "HoldingRegister",
        "RegisterAddress": 2,
        "RegisterCount": 2,
        "DataType": "Float32"
      }
    }
  ]
}
```

### 新增 Data Transform

在 `Report` 中加入 `TransformPipeline`：

```json
{
  "Name": "temperature",
  "Report": {
    "Enabled": true,
    "Interval": 5000,
    "TransformPipeline": [
      {
        "Type": "calibration",
        "Enabled": true,
        "Parameters": {
          "Scale": 0.1,
          "Offset": -10.0
        }
      }
    ]
  }
}
```

---

## Summary

- **WedaBuilder 範本**（建議）：全自動生命週期，完整 Host 功能（Telemetry + Command + Config Sync）
- **SubNode 範本**：半自動生命週期，可直接操作裝置執行單次動作，以 Telemetry 為主
- 兩者都支援 JSON 和 programmatic 設定方式
- 兩者都包含內建 Modbus Simulator，無需實體裝置即可開發

## See Also

- [連接到 WedaCore](./04-connect-to-wedacore.md) - 設定雲端連線
- [感測器設定](../04-configuration/02-configuration-via-json.md) - 完整 JSON 設定參考
- [Data Pipeline](../05-data-pipeline/01-overview.md) - 資料轉換指南

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |

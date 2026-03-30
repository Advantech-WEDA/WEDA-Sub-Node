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

### 步驟 1：建立新專案

```bash
# Copy template to new location
cp -r templates/wedabuilder ~/projects/my-subnode
cd ~/projects/my-subnode
```

### 步驟 2：還原相依性

```bash
dotnet restore
```

### 步驟 3：檢視專案結構

```text
my-subnode/
├── Program.cs           # Application entry point (builder pattern)
├── MyFirstDevice.cs     # Custom device class
├── devicecfg.json       # Device configuration
├── appsettings.json     # Logging and simulator configuration
└── WedaBuilder.csproj   # Project file
```

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
        Name = "temperature.sensor",
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

| 面向 | WedaBuilder | SubNode |
|------|-------------|---------|
| 設定方式 | JSON 為主 | 程式碼為主 |
| 生命週期 | 自動 | 手動 |
| 彈性 | 固定模式 | 完全控制 |
| 適用於 | 大多數使用案例 | 自訂場景 |

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

- **WedaBuilder 範本**（建議）：Builder 模式 + JSON 設定，自動管理生命週期
- **SubNode 範本**：程式碼設定 + 手動生命週期控制，適合需要完全客製化的場景
- 兩者都包含內建 Modbus Simulator，無需實體裝置即可開發
- 透過 `devicecfg.json` 的 `Sensors` 和 `TransformPipeline` 即可擴展功能

## See Also

- [連接到 WedaCore](./04-connect-to-wedacore.md) - 設定雲端連線
- [感測器設定](../04-configuration/02-configuration-via-json.md) - 完整 JSON 設定參考
- [Data Pipeline](../05-data-pipeline/01-overview.md) - 資料轉換指南

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-06 | Rain Hu | Doc created. |
| 1.1.0 | 2026-03-30 | Rain Hu | Added Overview, What You'll Learn, Summary. Updated code examples and links. |

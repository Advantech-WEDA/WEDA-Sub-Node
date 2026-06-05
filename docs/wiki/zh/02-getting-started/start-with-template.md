---
sidebar_position: 3
sidebar_label: '使用範本開始'
hide_title: true
title: '使用範本開始 - 建立新的 SubNode 專案'
keywords: ['SubNode', 'Template', 'dotnet new', 'Project Creation']
description: '使用 dotnet 範本建立新的 SubNode 專案'
---

# 使用範本開始

> 使用範本從頭建立新的 SubNode 專案。

## 可用範本

SubNode 提供兩種專案範本：

| 範本 | 名稱 | 說明 |
|------|------|------|
| **subnode** | WedaSubNode | 傳統 SubNode 模式，具有明確的生命週期控制 |
| **wedabuilder** | WedaBuilder | Builder 模式，使用 Fluent API（建議使用）|

## 使用 WedaBuilder 範本（建議）

`wedabuilder` 範本使用現代 Builder 模式，具有自動生命週期管理。

### 步驟 1：建立新專案

```bash
# 從儲存庫根目錄
cd templates/wedabuilder

# 或複製到新位置
cp -r templates/wedabuilder ~/projects/my-subnode
cd ~/projects/my-subnode
```

### 步驟 2：還原相依性

```bash
dotnet restore
```

### 步驟 3：檢視專案結構

```
my-subnode/
├── Program.cs           # 使用 builder 模式的應用程式進入點
├── MyFirstDevice.cs     # 自訂裝置類別
├── devicecfg.json       # 裝置設定
├── appsettings.json     # 日誌設定
└── WedaBuilder.csproj   # 專案檔
```

### 步驟 4：理解程式碼

**Program.cs：**

```csharp
using Weda.SubNode.Host;
using WedaBuilder;

var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()           // 主控台和檔案日誌
    .AddTelemetry()         // 上行：遙測報告
    .AddHealthReporting()   // 上行：健康狀態
    .AddCommands()          // 下行：遠端命令
    .AddConfigUpdates()     // 下行：設定同步
    .AddRecording()         // 本機：歷史資料儲存
    .UseMockCloud();        // 使用模擬伺服器（正式環境請更改）

builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");

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
        // 處理接收到的遙測資料
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("Received: {ResourceId} = {Value}",
                measure.ResourceId, measure.Value);
        }
    }
}
```

### 步驟 5：設定您的裝置

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
    "MyFirstDeviceConfig": {
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

### 步驟 6：使用模擬器執行

範本包含會自動啟動的 Modbus 模擬器：

```bash
dotnet run
```

**預期輸出：**

```
[12:00:00 INF] Modbus simulator started on 127.0.0.1:5020
[12:00:01 INF] SubNode started. Press Ctrl+C to stop...
[12:00:06 INF] Received: abc12 = 25.3
[12:00:11 INF] Received: abc12 = 25.5
...
```

## 使用 SubNode 範本

`subnode` 範本提供明確的生命週期控制。

### 步驟 1：建立新專案

```bash
cd templates/subnode
```

### 步驟 2：檢視程式碼

**Program.cs：**

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;

// 使用模擬雲端建立 context
using var context = new WedaApplicationContext(
    options => options.CloudService = WedaFactory.Cloud.Mock);

// 設定並啟動模擬器
var simulator = await ConfigureTcpModbusSimulator(context);

// 使用明確生命週期建立 SubNode
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyFirstDevice(context, ConfigureDeviceConfiguration()));

// 手動生命週期控制
await subNode.InitializeAsync();
await subNode.StartAsync();

Console.WriteLine("SubNode started. Press Ctrl+C to stop...");
await Task.Delay(Timeout.Infinite);
```

### 與 WedaBuilder 的差異

| 面向 | WedaBuilder | SubNode |
|------|-------------|---------|
| 設定 | JSON 為主 | 程式碼為主 |
| 生命週期 | 自動 | 手動 |
| 彈性 | 固定模式 | 完全控制 |
| 適用於 | 大多數使用案例 | 自訂場景 |

## 透過程式碼新增設定

對於 SubNode 範本，以程式方式設定感測器：

```csharp
DeviceConfiguration ConfigureDeviceConfiguration()
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

    // 新增溫度感測器
    var tempSensor = new ModbusSensorReporturation
    {
        Name = "temperature.sensor",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };
    tempSensor.Config.Interval = 5000;

    modbusConfig.AddSensor(tempSensor);

    var deviceConfig = modbusConfig.ToDeviceConfiguration();
    deviceConfig.InitializeDtdl();

    return deviceConfig;
}
```

## 自訂您的裝置

### 新增更多感測器

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

### 新增資料轉換

```json
{
  "Sensors": [
    {
      "Name": "temperature",
      "Report": {
        "Enabled": true,
        "Interval": 5000,
        "Transforms": [
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
  ]
}
```

## 下一步

- [連接到 WedaCore](./connect-to-wedacore.md) - 設定雲端連線
- [感測器設定](../04-sensor-configuration/configuration-reference.md) - 完整設定參考
- [Data Pipeline](../05-data-pipeline/pipeline-overview.md) - 資料轉換指南

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

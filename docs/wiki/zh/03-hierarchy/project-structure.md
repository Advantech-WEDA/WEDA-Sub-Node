---
sidebar_position: 3
sidebar_label: '專案結構'
hide_title: true
title: '專案結構 - SubNode SDK 組織'
keywords: ['SubNode', 'Project Structure', 'SDK', 'Architecture']
description: '了解 SubNode SDK 專案結構和模組組織'
---

# 專案結構

> 了解 SubNode SDK 專案結構和模組組織。

## 儲存庫概述

```
edge_subnode/
├── src/                    # SDK 原始碼
│   ├── Weda.SubNode.Abstractions/    # 介面和合約
│   ├── Weda.SubNode.Core/            # 核心實作
│   ├── Weda.SubNode.Devices/         # 預建裝置類型
│   ├── Weda.SubNode.Host/            # 應用程式託管
│   ├── Weda.SubNode.Cloud/           # 雲端整合
│   ├── Weda.SubNode.Simulators/      # 測試模擬器
│   └── Weda.SubNode.WebApi/          # REST API 支援
├── examples/               # 可直接使用的範例
├── templates/              # 專案範本
├── tutorials/              # 學習教學
├── tests/                  # 單元和整合測試
└── docs/                   # 文件
```

## SDK 模組

### Weda.SubNode.Abstractions

定義 SDK API 的核心介面和合約。

```
Abstractions/
├── Commands/           # 命令介面
│   ├── ICommand.cs
│   └── ICommandHandler.cs
├── Communication/      # 傳輸介面
│   ├── ICommunication.cs
│   ├── IRequestResponseCommunication.cs
│   ├── IPubSubCommunication.cs
│   └── IStreamingCommunication.cs
├── Context/            # 應用程式 Context
│   └── IWedaApplicationContext.cs
├── Devices/            # 裝置介面
│   ├── IDevice.cs
│   └── DeviceConfiguration.cs
├── Events/             # 事件類型
│   ├── DataReceivedEvent.cs
│   └── ConnectionStateChangedEvent.cs
├── Protocols/          # Protocol Parser 介面
│   ├── IProtocolParser.cs
│   └── IRequestResponseProtocolParser.cs
├── Telemetry/          # 遙測類型
│   ├── Sensor.cs
│   ├── TelemetryMeasure.cs
│   └── SensorGroup.cs
└── Transforms/         # Transform 介面
    └── ITelemetryTransform.cs
```

**關鍵介面：**

| 介面 | 用途 |
|------|------|
| `IDevice` | 裝置生命週期和遙測 |
| `ICommunication` | 傳輸抽象 |
| `IProtocolParser` | 資料編碼/解碼 |
| `ITelemetryTransform` | 資料轉換 |
| `ICommandHandler` | 命令處理 |

### Weda.SubNode.Core

核心 SDK 功能的實作。

```
Core/
├── Communication/      # 傳輸實作
│   ├── TcpCommunication.cs
│   ├── MqttCommunication.cs
│   └── HttpCommunication.cs
├── Devices/            # 基礎裝置類別
│   ├── DeviceBase.cs
│   ├── DeviceOrchestrator.cs
│   └── DeviceInitializer.cs
├── Protocols/          # 協定實作
│   └── Modbus/
│       ├── ModbusProtocolParser.cs
│       └── ModbusBatchReader.cs
└── Transforms/         # Transform 實作
    ├── CalibrationTransform.cs
    └── UnitConversionTransform.cs
```

### Weda.SubNode.Devices

常見協定的預建裝置類別。

```
Devices/
├── Generic/
│   ├── TcpModbusDevice.cs      # Modbus TCP 裝置
│   ├── RtuModbusDevice.cs      # Modbus RTU 裝置
│   └── MqttDevice.cs           # MQTT 裝置
└── Aggregator/
    └── AggregatorDevice.cs     # 多來源聚合器
```

**裝置類別階層：**

```
IDevice
    └── DeviceBase
            ├── TcpModbusDevice
            ├── RtuModbusDevice
            ├── MqttDevice
            └── AggregatorDevice
```

### Weda.SubNode.Host

應用程式託管和生命週期管理。

```
Host/
├── WedaApplication.cs          # 主要應用程式類別
├── WedaApplicationBuilder.cs   # Fluent Builder
├── Context/
│   └── WedaApplicationContext.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

### Weda.SubNode.Cloud

雲端整合服務。

```
Cloud/
├── CloudService.cs             # 實際雲端服務
├── MockCloudService.cs         # 測試用模擬
└── Messages/
    ├── TelemetryMessage.cs
    └── CommandMessage.cs
```

### Weda.SubNode.Simulators

無硬體開發的測試模擬器。

```
Simulators/
├── Modbus/
│   ├── TcpModbusSimulator.cs
│   └── TcpModbusSimulatorConfiguration.cs
├── Mqtt/
│   └── MqttSimulator.cs
└── Image/
    └── ImageSimulator.cs
```

## 應用程式專案結構

典型的 SubNode 應用程式具有以下結構：

```
my-subnode-app/
├── Program.cs              # 進入點
├── MyDevice.cs             # 自訂裝置類別
├── devicecfg.json          # 裝置設定
├── systemcfg.json          # 系統設定
├── appsettings.json        # 日誌設定
└── MySubNode.csproj        # 專案檔
```

### Program.cs

使用 Builder 模式的進入點：

```csharp
using Weda.SubNode.Host;

var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<MyDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();
```

### MyDevice.cs

自訂裝置實作：

```csharp
public class MyDevice : TcpModbusDevice
{
    public MyDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
    }

    // 根據需要覆寫生命週期方法
    protected override async Task OnAfterStartAsync()
    {
        // 自訂初始化
    }
}
```

### devicecfg.json

裝置和感測器設定：

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice"
  },
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": true,
      "DeviceCommunication": { ... },
      "Sensors": [ ... ]
    }
  }
}
```

### 專案檔 (.csproj)

套件參考：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Weda.SubNode.Host" Version="1.0.0" />
    <PackageReference Include="Weda.SubNode.Devices" Version="1.0.0" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## 設定檔

### 設定載入順序

設定按以下順序載入（後者覆蓋前者）：

1. `appsettings.json` - 基礎日誌設定
2. `appsettings.{Environment}.json` - 環境特定
3. `systemcfg.json` - 系統設定（WedaNode、雲端）
4. `devicecfg.json` - 裝置定義
5. 環境變數
6. 命令列參數

### 檔案用途

| 檔案 | 用途 | 雲端同步 |
|------|------|----------|
| `appsettings.json` | 日誌（Serilog）| 否 |
| `systemcfg.json` | WedaNode 連接 | 否 |
| `devicecfg.json` | 裝置/感測器設定 | 是 |

## Examples 目錄

示範各種場景的可直接使用範例：

| 範例 | 說明 |
|------|------|
| `wise-4012/` | 基本 Modbus TCP 裝置 |
| `power-aggregation/` | 多裝置資料聚合 |
| `stock-monitor/` | HTTP API 整合 |
| `image-sensor/` | MQTT 影像串流 |
| `air-quality-monitor/` | 感測器融合 |

每個範例包含：
- `Program.cs` - 應用程式進入點
- `devicecfg.json` - 可運作的設定
- 裝置類別實作
- README 包含特定說明

## 另請參閱

- [感測器設定](./04-sensor-configuration/configuration-reference.md) - 設定詳情
- [自訂裝置開發](./07-custom-device/device-base.md) - 建立裝置
- [範例](./02-getting-started/start-with-example.md) - 執行範例

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

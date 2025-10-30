---
title: 架構概述
category: architecture
order: 1
parent: null
related:
  - path: telemetry_transform.md
    title: 轉換管道
  - path: modbus_scanner.md
    title: Modbus 掃描器
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Weda SubNode SDK 架構概述 - 理解分層架構與設計原則
tags: [架構, 設計, 分層, 抽象]
---

# 架構概述

本文件說明 Weda SubNode SDK 的架構設計，包括分層結構、設計原則以及組件關係。

## 架構原則

SDK 遵循以下核心原則:

### 關注點分離
每一層都有明確的職責，不混合關注點。通訊邏輯與協定解析分離，協定解析與業務邏輯分離。

### 依賴反轉
高層模組依賴抽象(介面)而非具體實作。這使得擴展和測試更容易。

### 開放封閉原則
SDK 對擴展開放，對修改封閉。您可以新增設備、協定和轉換，而無需修改現有程式碼。

### 單一職責
每個類別和組件都有單一、明確定義的職責。

## 分層架構

SDK 由五個不同的層組成:

```
┌─────────────────────────────────────────────────┐
│                Host Layer                       │
│  - Application Framework                        │
│  - Configuration Management                     │
│  - Dependency Injection                         │
└────────────────┬────────────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────────────┐
│               Devices Layer                     │
│  - Concrete Device Implementations              │
│  - TcpModbusDevice, Adam6052, etc.              │
└────────────────┬────────────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────────────┐
│                Core Layer                       │
│  - Base Implementations                         │
│  - DeviceBase, ModbusDevice                     │
│  - Transforms, Filters                          │
└────────────────┬────────────────────────────────┘
                 │ implements
┌────────────────▼────────────────────────────────┐
│           Abstractions Layer                    │
│  - Interfaces and Contracts                     │
│  - IDevice, ICommunication, etc.                │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│               Cloud Layer                       │
│  - Cloud Platform Integration                   │
│  - NATS-based Communication                     │
└────────────────┬────────────────────────────────┘
                 │ implements
┌────────────────▼────────────────────────────────┐
│           Abstractions Layer                    │
│  - IWedaCloudService                            │
└─────────────────────────────────────────────────┘
```

## 層級詳細說明

### 1. 抽象層 (Abstractions)

**位置**: `Weda.SubNode.Abstractions`

**目的**: 定義所有介面和契約，不包含任何實作。

**關鍵組件**:
- **IDevice**: 核心設備介面
- **ICommunication**: 通訊抽象
- **IProtocolParser**: 協定解析介面
- **ITelemetryTransform**: 資料轉換介面
- **IDspFilter**: 數位訊號處理介面
- **IWedaCloudService**: 雲端服務介面

**特性**:
- 不依賴其他層
- 只包含介面和資料模型
- 定義事件和資料結構

### 2. 核心層 (Core)

**位置**: `Weda.SubNode.Core`

**目的**: 提供基礎實作和通用功能。

**關鍵組件**:
- **DeviceBase**: 具生命週期管理的基礎設備實作
- **ModbusDevice**: Modbus 協定設備實作
- **CommunicationBase**: 具重試和超時的基礎通訊
- **TelemetryTransformPipeline**: 可組合的資料轉換
- **DSP Filters**: KalmanFilter、MovingAverageFilter 等

**特性**:
- 實作 Abstractions 的介面
- 提供可擴展的基底類別
- 包含通用工具和輔助函式

### 3. 設備層 (Devices)

**位置**: `Weda.SubNode.Devices`

**目的**: 可直接使用的具體設備實作。

**關鍵組件**:
- **TcpModbusDevice**: Modbus TCP 設備
- **Adam6052**: 研華 ADAM-6052 專用實作
- **Custom Devices**: 使用者自訂設備

**特性**:
- 擴展 Core 層類別
- 提供供應商特定實作
- 包含預設配置

### 4. 雲端層 (Cloud)

**位置**: `Weda.SubNode.Cloud`

**目的**: 雲端平台整合。

**關鍵組件**:
- **WedaCloudService**: 基於 NATS 的雲端通訊
- **DeviceAgentClient**: 設備管理客戶端
- **TelemetryClient**: Telemetry 上傳客戶端
- **FileDeviceIdRepository**: 本地設備 ID 儲存

**特性**:
- 獨立於設備實作
- 處理設備註冊
- 管理 telemetry 上傳和健康報告

### 5. 框架層 (Host)

**位置**: `Weda.SubNode.Host`

**目的**: 應用程式框架和託管。

**關鍵組件**:
- **WedaApplication**: 主應用程式類別
- **WedaApplicationBuilder**: 建構器模式配置
- **DeviceHostedService**: 設備背景服務
- **LoggingConfigurator**: 日誌設定

**特性**:
- ASP.NET Core 風格 API
- 基於配置的設備設定
- 依賴注入容器

## 資料流架構

### 設備初始化流程

```
WedaApplication
    │
    ├─> 載入配置 (appsettings.json)
    │
    ├─> 建立設備實例
    │   └─> TcpModbusDevice
    │       ├─> ICommunication (TcpCommunication)
    │       ├─> IProtocolParser (ModbusProtocolParser)
    │       └─> IWedaCloudService (WedaCloudService)
    │
    ├─> 初始化設備
    │   └─> IDevice.InitializeAsync()
    │       ├─> 連接實體設備
    │       ├─> 註冊到雲端
    │       └─> 訂閱雲端事件
    │
    └─> 啟動設備
        └─> IDevice.StartAsync()
            ├─> 啟動 telemetry 讀取迴圈
            └─> 啟動健康報告迴圈
```

### Telemetry 資料流

```
實體設備
    │
    ▼
ICommunication.ReadAsync()
    │ (返回 byte[])
    ▼
IProtocolParser.Parse()
    │ (返回型別化數值)
    ▼
ITelemetryTransform.ExecuteAsync()
    │ (套用校準、單位轉換)
    ▼
IDspFilter.ApplyAsync()
    │ (套用濾波)
    ▼
TelemetryData
    │
    ▼
IWedaCloudService.SendTelemetryAsync()
    │
    ▼
Weda Cloud
```

## 擴展點

SDK 提供多個擴展點:

### 1. 自訂設備實作

```csharp
public class MyCustomDevice : ModbusDevice
{
    protected override async Task<TelemetryData> ReadTelemetryAsync(
        CancellationToken cancellationToken = default)
    {
        // 自訂讀取邏輯
        var data = await base.ReadTelemetryAsync(cancellationToken);

        // 後處理
        return data;
    }
}
```

### 2. 自訂通訊協定

```csharp
public class MyCustomCommunication : CommunicationBase
{
    protected override async Task<bool> ConnectCoreAsync(
        CancellationToken cancellationToken = default)
    {
        // 自訂連接邏輯
    }

    protected override async Task<byte[]> ReadCoreAsync(
        CancellationToken cancellationToken = default)
    {
        // 自訂讀取邏輯
    }
}
```

### 3. 自訂轉換

```csharp
public class MyTransform : ITelemetryTransform
{
    public Task<List<TelemetryMeasure>> ExecuteAsync(
        List<TelemetryMeasure> measures，
        TelemetryTransformContext context，
        CancellationToken cancellationToken = default)
    {
        // 轉換邏輯
    }
}
```

### 4. 自訂 DSP 濾波器

```csharp
public class MyFilter : IDspFilter
{
    public Task<object> ApplyAsync(
        object value，
        CancellationToken cancellationToken = default)
    {
        // 濾波邏輯
    }
}
```

## 使用的設計模式

### 建構器模式 (Builder Pattern)
用於 `WedaApplicationBuilder` 提供流暢的配置。

### 工廠模式 (Factory Pattern)
用於 `WedaFactory` 建立實例。

### 策略模式 (Strategy Pattern)
用於 `ITelemetryTransform` 和 `IDspFilter` 提供可插拔演算法。

### 觀察者模式 (Observer Pattern)
用於事件系統處理設備生命週期事件。

### 範本方法模式 (Template Method Pattern)
用於 `DeviceBase` 定義設備生命週期。

### 儲存庫模式 (Repository Pattern)
用於 `IDeviceIdRepository` 儲存設備 ID。

## 執行緒安全

### 並行操作
- 每個設備在自己的背景任務中執行
- 多個設備可以平行運行
- 通訊操作是執行緒安全的

### 狀態管理
- 設備狀態使用鎖保護
- 事件處理器非同步調用
- 使用取消標記優雅關閉

## 效能考量

### 資源使用
- 通訊連接池
- 使用物件池提高記憶體效率
- 熱路徑中最小化配置

### 可擴展性
- 單一程序支援數百個設備
- 可配置 telemetry 和健康週期
- 批次 telemetry 上傳提高效率

## 測試策略

### 單元測試
- 獨立測試每一層
- 使用介面模擬依賴
- 不依賴 I/O 測試業務邏輯

### 整合測試
- 使用真實設備測試通訊
- 使用測試環境測試雲端整合
- 測試完整資料流

### 測試結構範例

```csharp
public class DeviceBaseTests
{
    private readonly Mock<ICommunication> _communicationMock;
    private readonly Mock<IWedaCloudService> _cloudServiceMock;

    [Fact]
    public async Task StartAsync_ShouldStartTelemetryLoop()
    {
        // Arrange
        var device = new TestDevice(...);

        // Act
        await device.StartAsync();

        // Assert
        // 驗證 telemetry 讀取已啟動
    }
}
```

## 總結

Weda SubNode SDK 架構:
- 遵循 SOLID 原則
- 使用分層架構實現關注點分離
- 提供清晰的擴展點
- 支援所有層級的測試
- 有效擴展以管理多個設備

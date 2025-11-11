---
title: "使用 subnode 模板建立自訂裝置"
description: "使用物件導向架構建立第一個繼承自 ModbusDevice 的自訂裝置"
author: "Rain Hu"
date: "2025-11-09"
lang: "zh"
parent: "README"
prev: "01_install_templates"
next: "03_wedaapi_basic"
translations:
  - lang: "en"
    path: "../../en/01_quick_start/02_subnode_basic.md"
examples:
  - "examples/basic-modbus"
templates:
  - "subnode"
---

# 使用 subnode 模板建立自訂裝置

學習如何使用 `subnode` 模板建立自訂 IoT 裝置，採用物件導向的繼承架構。

**所需時間**：15 分鐘
**難度**：中等
**適合對象**：熟悉物件導向程式設計的開發者

---

## 什麼是 subnode 模板？

`subnode` 模板建立一個**物件導向**專案，您可以：

- ✅ **繼承** `TcpModbusDevice` 基礎類別
- ✅ **覆寫** 事件處理方法（`OnDataReceived`、`OnBeforeCommandAsync` 等）
- ✅ **完全控制** 裝置行為和業務邏輯
- ✅ **搭配 Simulator** 進行本機測試

### 何時使用 subnode 模板？

選擇 subnode 模板當您需要：
- 📌 實作自訂業務邏輯
- 📌 攔截並處理裝置事件
- 📌 完全控制資料流程
- 📌 使用物件導向設計模式

---

## 先決條件

開始前請確認：

- ✅ 已安裝 .NET 9.0 SDK
- ✅ 已安裝 Weda SubNode 模板（參考 [安裝模板](01_install_templates.md)）
- ✅ 基本了解 C# 物件導向程式設計

---

## 步驟 1：建立專案

使用 `subnode` 模板建立新專案：

```bash
# 建立專案目錄
mkdir -p devices/HelloFirstDevice
cd devices/HelloFirstDevice

# 使用模板建立專案
dotnet new subnode -n HelloFirstDevice
```

**預期輸出**：
```
The template "Weda SubNode Custom Device" was created successfully.
```

### 加入方案並還原套件（建議）

如果您在較大的解決方案中工作：

```bash
# 導航到方案根目錄
cd ../../

# 將專案加入方案
dotnet sln add devices/HelloFirstDevice/HelloFirstDevice.csproj

# 還原相依性
dotnet restore

# 返回專案目錄
cd devices/HelloFirstDevice
```

這確保所有相依性都正確解析，並且專案已整合到您的解決方案中。

### 專案結構

```
HelloFirstDevice/
├── MyFirstDevice.cs          # 自訂裝置類別
├── Program.cs                # 應用程式入口點
├── appsettings.json          # Serilog 日誌設定
└── HelloFirstDevice.csproj   # 專案檔
```

---

## 步驟 2：理解核心概念

這個步驟將深入解釋 subnode 模板的架構。理解這些概念後，您就能輕鬆擴展並自訂您的裝置。

### 2.1 MyFirstDevice.cs - 自訂裝置類別（完整解析）

開啟 `MyFirstDevice.cs`，讓我們逐行解釋：

```csharp
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Devices.Generic;

namespace HelloFirstDevice;

/// <summary>
/// MyFirstDevice - 您的自訂裝置實作
/// 繼承自 TcpModbusDevice 以獲得 Modbus TCP 協定支援
/// </summary>
public class MyFirstDevice : TcpModbusDevice  // ← 繼承點：獲得所有 Modbus 功能
{
    // ===== 建構子：初始化與事件訂閱 =====
    public MyFirstDevice(
        IWedaApplicationContext context,      // ← SDK 提供的執行環境
        DeviceConfiguration configuration)    // ← 您的裝置設定
        : base(context, configuration)        // ← 呼叫父類別建構子
    {
        // ★ 這裡是您訂閱事件的地方 ★
        // SDK 會在特定時機觸發這些事件，您只需要註冊處理器
        DataReceived += OnDataReceived;
    }

    // ===== 事件處理器：處理遙測資料 =====
    /// <summary>
    /// 當裝置接收到遙測資料時會被呼叫
    /// 觸發時機：SDK 定期從 Modbus 裝置讀取資料後
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // e.Data 包含所有感測器的最新數值
        _logger.LogInformation("📊 Telemetry received:");

        foreach (var measure in e.Data)
        {
            _logger.LogInformation("  - {Name}: {Value}",
                measure.Name,      // 感測器名稱 (例如: "temperature.sensor")
                measure.Value);    // 感測器數值 (例如: 25.3)
        }

        // ★ 您的自訂邏輯寫在這裡 ★
        // 範例：
        // - 檢查閾值並發送警報
        // - 將資料寫入本地資料庫
        // - 觸發其他業務邏輯
    }

    // ===== 解構子：清理資源 =====
    ~MyFirstDevice()
    {
        // 取消訂閱事件，避免記憶體洩漏
        DataReceived -= OnDataReceived;
    }
}
```

#### 🔑 關鍵概念詳解

##### 1. **繼承 TcpModbusDevice - 您不需要寫的程式碼**

當您繼承 `TcpModbusDevice` 時，SDK 已經為您處理：

| 功能 | SDK 自動處理 | 您不需要做 |
|------|-------------|-----------|
| TCP 連線管理 | ✅ 自動連接、重連、斷線偵測 | ❌ 不需手動管理連線 |
| Modbus 協定 | ✅ 讀寫 holding registers、coils 等 | ❌ 不需實作 Modbus 協定 |
| 資料解析 | ✅ 從 raw bytes 解析成 float、int 等 | ❌ 不需手動轉換位元組 |
| 錯誤重試 | ✅ 自動重試與 circuit breaker | ❌ 不需處理暫時性錯誤 |
| 生命週期 | ✅ Initialize → Start → Stop → Dispose | ❌ 不需管理狀態機 |

##### 2. **Event 訂閱模式 - 您需要寫的程式碼**

SDK 提供以下事件，您可以選擇性訂閱：

```csharp
public MyFirstDevice(IWedaApplicationContext context, DeviceConfiguration config)
    : base(context, config)
{
    // ★ 這些是 SDK 提供的事件，您可以選擇訂閱 ★

    DataReceived += OnDataReceived;                              // 遙測資料接收
    CommandReceived += OnCommandReceived;                        // 雲端命令接收
    ConnectionStateChanged += OnConnectionStateChanged;          // 連線狀態變更
    DeviceStatusChanged += OnDeviceStatusChanged;                // 裝置狀態變更
    ConfigurationUpdateReceived += OnConfigurationUpdateReceived; // 設定更新接收
}
```

**何時訂閱？何時不訂閱？**

| 事件 | 訂閱時機 | 用途範例 |
|------|---------|---------|
| `DataReceived` | 如果需要在裝置端記錄遙測資料 | 處理遙測資料、檢查閾值、本地存儲 |
| `CommandReceived` | 如果需要雲端控制 | 接收遠端命令、執行動作 |
| `ConnectionStateChanged` | 如果需要監控連線 | 記錄斷線事件、發送通知 |
| `DeviceStatusChanged` | 如果需要狀態追蹤 | 監控裝置健康狀態 |
| `ConfigurationUpdateReceived` | 如果支援動態設定 | 熱更新裝置參數 |

##### 3. **建構子參數 - SDK 與您的分工**

```csharp
public MyFirstDevice(
    IWedaApplicationContext context,      // SDK 提供
    DeviceConfiguration configuration)    // 您提供
```

**IWedaApplicationContext - SDK 提供的執行環境**
- ✅ Logger Factory：用於記錄日誌
- ✅ Cloud Service：連接到 Weda.Core 或 Mock
- ✅ Configuration：讀取 appsettings.json

**DeviceConfiguration - 您提供的裝置設定**
- 📝 DeviceName: 裝置名稱（必填）
- 📝 Host/Port: Modbus 伺服器位址（必填）
- 📝 Sensors: 感測器定義（必填）
- 📝 Properties: 自訂屬性（選填）

#### 🎯 未來擴展指引

##### 情境 1：我想連接真實的 Modbus 裝置

**需要修改的程式碼：** `Program.cs` 中的 `ConfigureDeviceConfiguration()`

```csharp
var modbusDeviceConfig = new TcpModbusDeviceConfiguration
{
    Host = "192.168.1.100",  // ← 改成真實裝置 IP
    Port = 502,              // ← 改成真實裝置 Port
    SlaveId = 1              // ← 改成真實裝置 Slave ID
};
```

**不需要修改的程式碼：** `MyFirstDevice.cs`（完全不用動！）

##### 情境 2：我想添加自訂業務邏輯

**需要修改的程式碼：** `MyFirstDevice.cs` 中的事件處理器

```csharp
private void OnDataReceived(object? sender, DataReceivedEvent e)
{
    // ★ 在這裡添加您的邏輯 ★

    // 範例 1: 溫度超過閾值發送警報
    var temp = e.Data.FirstOrDefault(m => m.Name == "temperature.sensor");
    if (temp?.Value is double t && t > 30.0)
    {
        SendAlert($"Temperature too high: {t}°C");
    }

    // 範例 2: 寫入本地資料庫
    await _database.SaveTelemetryAsync(e.Data);

    // 範例 3: 觸發其他系統
    await _notificationService.NotifyAsync(e.Data);
}
```

**不需要修改的程式碼：** SDK 的通訊、解析、生命週期管理

##### 情境 3：我想連接真實的 Weda.Core 雲端服務

**需要修改的程式碼：
1. 移除 `options.CloudService = WedaFactory.Cloud.Mock`
2. 提供 Nats 連線。

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ❌ 移除這行
    // options.CloudService = WedaFactory.Cloud.Mock;

    // 🟡 方法 2: 透過 NatsConnectionSettings 設定 URL
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://172.22.160.197:4224",
        CredFile = ""  // 如果需要認證，填入 .creds 檔案路徑
    };
});
```

**💡 SDK 預設行為：**
- 如果移除 `options.CloudService = WedaFactory.Cloud.Mock;`，系統會自動連接真實的 Weda.Core，等效於 `options.CloudService = WedaFactory = WedaFactory.Cloud.Default;`
- 預設連接到 `nats://localhost:4222`
- 可以透過 `options.NatsConnectionSettings` 調整連線設定

**📝 方法 3: 從 appsettings.json 讀取（生產環境推薦）**

在 `appsettings.json` 中加入 `Nats` 設定區段：

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      }
    ]
  },
  "Nats": {
    "Url": "nats://172.22.160.197:4224",
    "CredFile": "",
    "Name": "default",
    "SerializerType": "json"
  }
}
```

然後在 `Program.cs` 中，**只需移除 Mock，SDK 會自動讀取 appsettings.json**：

```csharp
using var context = new WedaApplicationContext(options =>
{
    // ✅ SDK 會自動載入 appsettings.json
    // - 自動讀取 Serilog section 建立 LoggerFactory
    // - 自動讀取 Nats section 建立 CloudService
    // 完全不需要手動建立任何東西！

    // ❌ 移除 Mock
    // options.CloudService = WedaFactory.Cloud.Mock;
});
```

**💡 SDK 自動載入行為（已大幅改進）：**
- SDK 會**自動**從當前目錄載入 `appsettings.json`
- 自動從 `Serilog` section 讀取並建立 LoggerFactory
- 自動從 `Nats` section 讀取 NATS 連線設定
- **不需要**手動建立 `ConfigurationBuilder`、`IConfiguration` 或 `LoggerFactory`
- **可選覆蓋**：手動設定的 `options.LoggerFactory` 或 `options.NatsConnectionSettings` 會優先使用

**不需要修改的程式碼：** `MyFirstDevice.cs`、資料收集邏輯

##### 情境 4：我想更換通訊協定（不用 Modbus）

**需要修改的程式碼：** 改繼承不同的基礎類別

```csharp
// 原本: Modbus TCP
public class MyFirstDevice : TcpModbusDevice { }

// 改成: MQTT
public class MyFirstDevice : MqttDevice { }

// 改成: OPC UA
public class MyFirstDevice : OpcUaDevice { }

// 改成: 完全自訂
public class MyFirstDevice : DeviceBase { }  // 需要更多實作
```

#### 📊 程式碼分類總結

| 程式碼區域 | 誰負責 | 修改頻率 | 用途 |
|-----------|-------|---------|------|
| `TcpModbusDevice` 基礎類別 | SDK | ❌ 永不修改 | 通訊協定實作 |
| `MyFirstDevice` 建構子 | 您 | 🟡 偶爾（新增事件訂閱） | 事件註冊 |
| Event Handlers (OnXxx) | 您 | ✅ 經常 | 業務邏輯 |
| `ConfigureDeviceConfiguration()` | 您 | ✅ 經常 | 裝置設定 |
| `Program.cs` 生命週期管理 | SDK Template | 🟡 偶爾（切換雲端） | 應用程式啟動 |

### 2.2 Program.cs - 應用程式設定（完整解析）

`Program.cs` 是應用程式的入口點。讓我們逐段解釋每個部分的作用：

```csharp
// ===== 第 1 部分：設定與日誌初始化 =====
// 這部分是標準的 .NET 設定模式，通常不需要修改
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())       // 設定基礎路徑
    .AddJsonFile("appsettings.json", optional: false)   // 載入 appsettings.json
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)              // 從 appsettings.json 讀取日誌設定
    .CreateLogger();

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
```

#### 📝 第 1 部分說明

| 程式碼 | 作用 | 是否需要修改 |
|-------|------|------------|
| `ConfigurationBuilder` | 讀取 appsettings.json | ❌ 不需要 |
| `LoggerConfiguration` | 設定 Serilog 日誌 | ❌ 不需要（在 appsettings.json 調整） |
| `loggerFactory` | 提供給 SDK 使用的日誌工廠 | ❌ 不需要 |

```csharp
try
{
    // ===== 第 2 部分：Simulator 設定（測試用） =====
    // ★ 這部分是為了本地測試，連接真實裝置時可以移除 ★
    var simulator = await ConfigureTcpModbusSimulator();

    // ===== 第 3 部分：Device Configuration 設定 =====
    // ★ 這裡定義您的裝置參數（Host、Port、Sensors） ★
    var deviceConfig = ConfigureDeviceConfiguration();

    // ===== 第 4 部分：ApplicationContext 建立 =====
    // ★ 這裡設定 SDK 的執行環境 ★
    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;           // 提供日誌
        options.CloudService = WedaFactory.Cloud.Mock;   // ★ 切換雲端服務的地方 ★
    });
```

#### 📝 第 2-4 部分說明

**第 2 部分：Simulator（測試環境）**

```csharp
var simulator = await ConfigureTcpModbusSimulator();
```

| 用途 | 何時使用 | 何時移除 |
|------|---------|---------|
| 模擬 Modbus 裝置 | ✅ 本地開發測試 | ❌ 部署到生產環境 |
| 產生假資料 | ✅ 沒有真實硬體時 | ❌ 連接真實裝置時 |

**第 3 部分：Device Configuration（裝置設定）**

```csharp
var deviceConfig = ConfigureDeviceConfiguration();
```

這個方法定義在檔案底部，包含：
- ✅ **必須修改**：當您連接真實裝置時
- ✅ **經常修改**：調整 Host/Port、新增 Sensors

**第 4 部分：ApplicationContext（執行環境）**

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;        // ❌ 不需修改
    options.CloudService = WedaFactory.Cloud.Mock; // ✅ 切換雲端時需修改
});
```

| 選項 | 作用 | 修改時機 |
|------|------|---------|
| `LoggerFactory` | 提供日誌功能 | ❌ 永不修改 |
| `CloudService` | 雲端服務連接 | ✅ Mock → Real Cloud |

```csharp
    // ===== 第 5 部分：Device 生命週期管理 =====
    var device = new MyFirstDevice(context, deviceConfig);  // 建立裝置實例
    await device.InitializeAsync();                         // 初始化（連接、註冊）
    await device.StartAsync();                              // 啟動（開始讀取資料）

    Log.Information("MyFirstDevice started. Press Ctrl+C to stop...");

    // ===== 第 6 部分：等待與 Graceful Shutdown =====
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;   // 防止立即終止
        cts.Cancel();      // 觸發取消訊號
    };
    await Task.Delay(Timeout.Infinite, cts.Token);  // 持續執行直到 Ctrl+C

    // ===== 第 7 部分：優雅關閉 =====
    await device.StopAsync();       // 停止讀取資料
    device.Dispose();               // 釋放資源
    await simulator.StopAsync();    // 關閉 Simulator（如果有）
}
catch (OperationCanceledException)
{
    Log.Information("Application stopped");
}
finally
{
    await Log.CloseAndFlushAsync();  // 確保所有日誌寫入
}
```

#### 📝 第 5-7 部分說明

**第 5 部分：Device 生命週期（手動管理）**

```csharp
var device = new MyFirstDevice(context, deviceConfig);
await device.InitializeAsync();  // ← 步驟 1
await device.StartAsync();       // ← 步驟 2
```

| 方法 | 作用 | SDK 內部行為 |
|------|------|------------|
| `new MyFirstDevice()` | 建立實例 | 訂閱事件、準備資源 |
| `InitializeAsync()` | 初始化 | 連接 Modbus、註冊雲端 |
| `StartAsync()` | 啟動 | 開始定期讀取資料 |

**第 6 部分：等待訊號（為何這樣寫？）**

```csharp
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;   // ← 關鍵！防止程式立即終止
    cts.Cancel();
};
await Task.Delay(Timeout.Infinite, cts.Token);
```

| 程式碼 | 作用 | 為何需要 |
|-------|------|---------|
| `e.Cancel = true` | 防止預設終止行為 | 讓我們有時間優雅關閉 |
| `cts.Cancel()` | 觸發取消訊號 | 跳出 `Task.Delay` |
| `Task.Delay(Infinite)` | 無限等待 | 保持程式執行 |

**第 7 部分：優雅關閉（Graceful Shutdown）**

```csharp
await device.StopAsync();       // ← 步驟 1: 停止背景工作
device.Dispose();               // ← 步驟 2: 釋放資源
await simulator.StopAsync();    // ← 步驟 3: 關閉 Simulator
```

**為何順序很重要？**
1. 先停止讀取資料（避免新的作業進來）
2. 再釋放資源（關閉連線、取消訂閱）
3. 最後關閉 Simulator（確保裝置已完全停止）

#### 🎯 Program.cs 擴展指引

##### 情境 1：移除 Simulator，連接真實裝置

**需要修改的程式碼：**

```csharp
// ❌ 移除這行
// var simulator = await ConfigureTcpModbusSimulator();

// 修改 DeviceConfiguration
var deviceConfig = ConfigureDeviceConfiguration();  // 裡面改 Host/Port

// 建立 device（不變）
var device = new MyFirstDevice(context, deviceConfig);
await device.InitializeAsync();
await device.StartAsync();

// ❌ 移除 simulator 關閉
// await simulator.StopAsync();
```

##### 情境 2：切換到真實雲端服務

**需要修改的程式碼：**

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ❌ 移除或註解掉 Mock
    // options.CloudService = WedaFactory.Cloud.Mock;

    // ✅ 設定真實 NATS 連線
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://your-nats-server:4222",
        CredFile = ""
    };
});
```

##### 情境 3：改用 HostedService 模式（類似 wedaapi）

如果您想要更自動化的生命週期管理，可以改用 `wedaapi` 或 `wedaapi-c` 模板。

**subnode vs wedaapi 比較：**

| 特性 | subnode | wedaapi |
|------|---------|---------|
| 生命週期 | 手動管理 | 自動管理（HostedService） |
| 控制度 | ✅ 完全控制每個步驟 | 🟡 較少控制 |
| 程式碼量 | 🟡 較多（~80 行） | ✅ 極少（3 行） |
| 適合場景 | 學習、原型、完全客製 | 快速部署、標準場景 |

#### 📊 Program.cs 程式碼分類

| 程式碼區段 | 是否需要修改 | 修改時機 | 修改內容 |
|-----------|------------|---------|---------|
| Configuration & Logging | ❌ 永不 | - | - |
| Simulator 設定 | ✅ 是 | 連接真實裝置 | 移除或註解 |
| DeviceConfiguration | ✅ 經常 | 每個專案不同 | Host/Port/Sensors |
| CloudService | ✅ 是 | 部署到生產 | Mock → NATS |
| 生命週期管理 | ❌ 很少 | - | 除非改架構 |
| Graceful Shutdown | ❌ 永不 | - | - |

---

### 2.3 Helper Methods - 設定輔助方法

在 `Program.cs` 底部，有兩個 helper methods：

#### ConfigureDeviceConfiguration() - 裝置設定

```csharp
DeviceConfiguration ConfigureDeviceConfiguration()
{
    // ★ 這裡是您最常修改的地方 ★
    var modbusDeviceConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",     // ← 裝置名稱（會顯示在雲端）
        Manufacturer = "Advantech",    // ← 製造商
        Model = "CustomDevice-v1",     // ← 型號
        Host = "127.0.0.1",           // ← ★ 改成真實 IP ★
        Port = 5020,                  // ← ★ 改成真實 Port ★
        SlaveId = 1                   // ← ★ Modbus Slave ID ★
    };

    // ★ 定義感測器 ★
    var tempSensor = new ModbusSensorConfiguration
    {
        Name = "temperature.sensor",                   // ← 感測器識別名稱
        Dtmi = "dtmi:advantech:EdgeSync:Temperature;1", // ← Digital Twin 定義
        RegisterAddress = 0,                           // ← Modbus 起始位址
        RegisterCount = 2,                             // ← 讀取 2 個 registers (float32)
        DataType = ModbusDataType.Float32,            // ← 資料型別
        RegisterType = ModbusRegisterType.HoldingRegister, // ← Register 類型
        SensorGroup = SensorGroup.TEMP                // ← 感測器群組
    };

    modbusDeviceConfig.AddSensor(tempSensor);
    return modbusDeviceConfig.ToDeviceConfiguration();
}
```

**修改指引：**

| 欄位 | 用途 | 範例 | 何時修改 |
|------|------|------|---------|
| `Host` | Modbus 裝置 IP | "192.168.1.100" | ✅ 連接真實裝置 |
| `Port` | Modbus Port | 502 | ✅ 如果非標準 port |
| `SlaveId` | Modbus Slave ID | 1 | ✅ 根據裝置設定 |
| `RegisterAddress` | 起始位址 | 0, 100, 400 | ✅ 根據裝置手冊 |
| `DataType` | 資料型別 | Float32, Int16 | ✅ 根據裝置手冊 |
| `DeviceName` | 識別名稱 | "MyDevice" | 🟡 每個裝置不同 |

#### ConfigureTcpModbusSimulator() - Simulator 設定

```csharp
async Task<TcpModbusSimulator> ConfigureTcpModbusSimulator()
{
    // ★ 這部分只用於本地測試，連接真實裝置時可移除 ★
    var simulatorConfig = new TcpModbusSimulatorConfiguration
    {
        TcpConnection = new TcpConnectionSettings
        {
            IpAddress = "127.0.0.1",  // ← Simulator 監聽位址
            Port = 5020               // ← 必須與 Device 設定一致
        },
        Sensors = new List<SimulatedSensor>
        {
            new SimulatedSensor
            {
                Name = "TemperatureSensor",
                StartAddress = 0,              // ← 必須與 Device 設定一致
                DataType = SimulatedDataType.Float32,
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 18.0,           // ← 最小值
                    MaxValue = 32.0,           // ← 最大值
                    InitialValue = 25.0,       // ← 初始值
                    ChangeRate = 0.2,          // ← 每次變化幅度
                    NoiseLevel = 0.1           // ← 隨機雜訊
                }
            }
        }
    };

    var simulator = new TcpModbusSimulator(simulatorConfig, loggerFactory.CreateLogger<TcpModbusSimulator>());
    await simulator.StartAsync();
    return simulator;
}
```

**Simulator 用途：**
- ✅ **開發階段**：沒有真實硬體時測試程式碼
- ✅ **CI/CD**：自動化測試不依賴真實硬體
- ❌ **生產環境**：不需要 Simulator

---

### ✅ 步驟 2 總結

現在您應該理解：

1. **MyFirstDevice.cs** - 您的業務邏輯放在這裡
   - ✅ 繼承 `TcpModbusDevice` 獲得通訊能力
   - ✅ 訂閱 Event 處理資料
   - ✅ SDK 負責底層，您負責業務邏輯

2. **Program.cs** - 應用程式啟動與設定
   - ✅ 手動控制生命週期（學習用）
   - ✅ 修改 CloudService 切換測試/生產環境
   - ✅ Graceful Shutdown 確保資料安全

3. **Helper Methods** - 裝置與 Simulator 設定
   - ✅ `ConfigureDeviceConfiguration()` - 連接真實裝置時修改
   - ✅ `ConfigureTcpModbusSimulator()` - 測試用，可移除

**下一步：** 了解 ApplicationContext 進階設定

---

### 2.4 IWedaApplicationContext 進階設定

`IWedaApplicationContext` 是 SDK 的核心，它管理所有框架級服務。讓我們深入了解它的設定選項。

#### 什麼是 ApplicationContext？

ApplicationContext 是 SDK 的**執行環境**，類似於 ASP.NET Core 的 `IServiceProvider`，但專為 IoT 裝置設計。

```csharp
using var context = new WedaApplicationContext(options =>
{
    // 在這裡設定所有 SDK 行為
});
```

#### WedaContextOptions 完整選項

`WedaContextOptions` 包含以下設定項目：

##### 1. **CloudService** - 雲端服務設定

```csharp
options.CloudService = WedaFactory.Cloud.Mock;  // 或
options.NatsConnectionSettings = new NatsConnectionSettings { ... };  // 或
```

| 選項 | 用途 | 使用時機 |
|------|------|---------|
| `Mock` | 模擬雲端服務 | ✅ 本地開發、測試 |
| `NatsConnectionSettings` | 設定 NATS 連線參數 | ✅ 生產環境 |
| 自訂 `IWedaCloudService` | 完全自訂實作 | 🟡 特殊需求 |

**範例：切換雲端服務**

```csharp
// 開發環境 - 使用 Mock
options.CloudService = WedaFactory.Cloud.Mock;

// 生產環境 - 設定真實 NATS 連線
options.NatsConnectionSettings = new NatsConnectionSettings
{
    Url = "nats://production-server:4222",
    CredFile = "/path/to/nats.creds"  // 如需認證
};

```

##### 2. **LoggerFactory** - 日誌工廠

```csharp
options.LoggerFactory = loggerFactory;
```

| 用途 | 說明 |
|------|------|
| SDK 內部日誌 | 所有 SDK 元件使用此 factory 記錄日誌 |
| 裝置日誌 | 您的 `_logger` 也來自這個 factory |
| 統一管理 | 集中控制日誌等級、輸出目標 |

**❌ 通常不需要修改**，除非您要替換日誌框架。

##### 3. **DeviceOptions** - 裝置功能控制（Application Layer）

```csharp
options.DeviceOptions = new DeviceOptions
{
    DefaultPollingIntervalMs = 1000,      // 遙測輪詢間隔
    EnableTelemetry = true,               // 啟用遙測上傳
    EnableHealthReporting = true,         // 啟用健康報告
    EnableCommands = true,                // 啟用命令接收
    EnableConfigUpdates = true            // 啟用設定更新
};
```

##### 📊 DeviceOptions 詳細說明

| 選項 | 預設值 | 用途 | 何時啟用 |
|------|--------|------|---------|
| `DefaultPollingIntervalMs` | 1000ms | 多久讀取一次裝置資料 | 總是設定 |
| `EnableTelemetry` | `false` | 發送遙測到雲端（上行） | ✅ 需要監控資料時 |
| `EnableHealthReporting` | `false` | 發送健康狀態（上行） | ✅ 需要監控裝置狀態時 |
| `EnableCommands` | `false` | 接收雲端命令（下行） | ✅ 需要遠端控制時 |
| `EnableConfigUpdates` | `false` | 接收設定更新（下行） | ✅ 需要動態調整參數時 |

**實際範例：不同場景的設定**

```csharp
// 場景 1: 只監控，不控制
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = true,           // ✅ 上傳資料
    EnableHealthReporting = true,     // ✅ 回報健康
    EnableCommands = false,           // ❌ 不接受命令
    EnableConfigUpdates = false       // ❌ 不動態調整
};

// 場景 2: 完全離線（本地測試）
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = false,          // ❌ 不上傳
    EnableHealthReporting = false,    // ❌ 不回報
    EnableCommands = false,           // ❌ 不接受命令
    EnableConfigUpdates = false       // ❌ 不更新
};

// 場景 3: 全功能（生產環境）
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = true,           // ✅ 上傳資料
    EnableHealthReporting = true,     // ✅ 回報健康
    EnableCommands = true,            // ✅ 接受命令
    EnableConfigUpdates = true        // ✅ 動態調整
};
```

**⚠️ 注意事項：**
- 這些選項**只在連接真實雲端時生效**
- 使用 `Mock` 雲端時，這些開關無效（Mock 不會真的發送資料）
- 可以在執行時期動態調整（進階用法）

##### 4. **ConnectionOptions** - 連線重試設定（Communication Layer）

```csharp
options.ConnectionOptions = new ConnectionOptions
{
    MaxRetryAttempts = 3,           // 最大重試次數
    RetryDelayMs = 1000,            // 初始重試延遲（指數退避）
    ConnectionTimeoutMs = 30000     // 連線逾時（30秒）
};
```

##### 📊 ConnectionOptions 詳細說明

| 選項 | 預設值 | 用途 | 調整時機 |
|------|--------|------|---------|
| `MaxRetryAttempts` | 3 | 連線失敗後重試幾次 | 🟡 不穩定網路環境增加 |
| `RetryDelayMs` | 1000ms | 首次重試延遲（會指數成長） | 🟡 根據網路狀況調整 |
| `ConnectionTimeoutMs` | 30000ms | 連線逾時時間 | 🟡 慢速裝置增加 |

**指數退避（Exponential Backoff）說明：**

```
第 1 次重試: 等待 1000ms
第 2 次重試: 等待 2000ms (1000 × 2)
第 3 次重試: 等待 4000ms (2000 × 2)
```

**實際範例：不同場景的設定**

```csharp
// 場景 1: 穩定區域網路（預設值即可）
options.ConnectionOptions = ConnectionOptions.Default;

// 場景 2: 不穩定的 Wi-Fi 環境
options.ConnectionOptions = new ConnectionOptions
{
    MaxRetryAttempts = 5,           // 增加重試次數
    RetryDelayMs = 2000,            // 增加初始延遲
    ConnectionTimeoutMs = 60000     // 增加逾時（1分鐘）
};

// 場景 3: 快速失敗（測試用）
options.ConnectionOptions = new ConnectionOptions
{
    MaxRetryAttempts = 1,           // 只試一次
    RetryDelayMs = 100,             // 短延遲
    ConnectionTimeoutMs = 5000      // 5秒逾時
};
```

##### 5. **Configuration** - 應用程式設定

```csharp
options.Configuration = configuration;  // IConfiguration 實例
```

| 用途 | 說明 |
|------|------|
| 讀取 appsettings.json | 訪問應用程式設定 |
| 環境變數 | 讀取環境變數設定 |
| 自訂設定 | 您的應用程式特定設定 |

**範例：從 Configuration 讀取自訂設定**

```csharp
// appsettings.json
{
  "MyApp": {
    "AlertThreshold": 30.0,
    "NotificationEmail": "admin@example.com"
  }
}

// 在您的 device 中使用
var threshold = context.Configuration["MyApp:AlertThreshold"];
var email = context.Configuration["MyApp:NotificationEmail"];
```

##### 6. **其他進階選項**

```csharp
options.NatsConnectionSettings = new NatsConnectionSettings
{
    Url = "nats://localhost:4222",
    CredFile = "/path/to/nats.creds"
};

options.AutoLoadDtdl = true;                    // 自動載入 DTDL 定義
options.DisposeServices = true;                 // Context 釋放時自動清理服務
options.DeviceConfigurationKey = "Device1";     // 從 DeviceConfigs 中選擇哪個裝置
```

#### 🎯 實際應用範例

##### 範例 1：開發環境設定

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;
    options.CloudService = WedaFactory.Cloud.Mock;  // 使用 Mock 雲端

    // 關閉所有雲端功能（本地測試）
    options.DeviceOptions = new DeviceOptions
    {
        DefaultPollingIntervalMs = 500,  // 快速輪詢方便測試
        EnableTelemetry = false,
        EnableHealthReporting = false,
        EnableCommands = false,
        EnableConfigUpdates = false
    };

    // 快速失敗設定（開發時即時發現問題）
    options.ConnectionOptions = new ConnectionOptions
    {
        MaxRetryAttempts = 1,
        RetryDelayMs = 100,
        ConnectionTimeoutMs = 5000
    };
});
```

##### 範例 2：生產環境設定（使用 appsettings.json）

在 `appsettings.json` 中設定：

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/production-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  },
  "Nats": {
    "Url": "nats://production:4222",
    "CredFile": "/path/to/production.creds",
    "Name": "production",
    "SerializerType": "json"
  }
}
```

```csharp
using var context = new WedaApplicationContext(options =>
{
    // ✅ SDK 會自動載入 appsettings.json
    // - Serilog 會自動設定為輸出到 Console 和 File
    // - NATS 會自動連接到 production server

    // 啟用所有雲端功能
    options.DeviceOptions = new DeviceOptions
    {
        DefaultPollingIntervalMs = 1000,
        EnableTelemetry = true,
        EnableHealthReporting = true,
        EnableCommands = true,
        EnableConfigUpdates = true
    };

    // 穩健的重試設定
    options.ConnectionOptions = new ConnectionOptions
    {
        MaxRetryAttempts = 5,
        RetryDelayMs = 2000,
        ConnectionTimeoutMs = 60000
    };
});
```

##### 範例 3：邊緣場景（不穩定網路）

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // 連接邊緣 NATS 伺服器
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://edge-gateway:4222",
        CredFile = ""
    };

    // 只上傳資料，不接受控制（安全考量）
    options.DeviceOptions = new DeviceOptions
    {
        DefaultPollingIntervalMs = 5000,  // 降低頻率節省頻寬
        EnableTelemetry = true,
        EnableHealthReporting = true,
        EnableCommands = false,           // 不接受命令
        EnableConfigUpdates = false       // 不動態更新
    };

    // 加強重試機制
    options.ConnectionOptions = new ConnectionOptions
    {
        MaxRetryAttempts = 10,            // 增加重試
        RetryDelayMs = 5000,              // 長延遲
        ConnectionTimeoutMs = 120000      // 2分鐘逾時
    };
});
```

#### 📊 設定決策樹

```
是否需要連接雲端？
├─ 否 → CloudService = Mock, 所有 DeviceOptions = false
└─ 是 → 移除 Mock, 設定 NatsConnectionSettings
    ├─ 需要上傳資料？
    │   ├─ 是 → EnableTelemetry = true
    │   └─ 否 → EnableTelemetry = false
    ├─ 需要監控健康？
    │   ├─ 是 → EnableHealthReporting = true
    │   └─ 否 → EnableHealthReporting = false
    ├─ 需要遠端控制？
    │   ├─ 是 → EnableCommands = true
    │   └─ 否 → EnableCommands = false
    └─ 需要動態調整？
        ├─ 是 → EnableConfigUpdates = true
        └─ 否 → EnableConfigUpdates = false
```

#### ✅ Context Options 總結

| 選項 | 層級 | 修改頻率 | 用途 |
|------|------|---------|------|
| `CloudService` | 框架 | 🟡 環境切換時 | 選擇雲端服務 |
| `LoggerFactory` | 框架 | ❌ 幾乎不改 | 日誌系統 |
| `DeviceOptions` | 應用 | ✅ 經常調整 | 控制裝置功能 |
| `ConnectionOptions` | 通訊 | 🟡 網路環境變化時 | 連線重試策略 |
| `Configuration` | 應用 | ❌ 通常由 DI 提供 | 存取設定檔 |

**重要提醒：**
- `DeviceOptions` 和 `ConnectionOptions` 是**兩個不同層級**的設定
  - `DeviceOptions`：應用層（控制業務功能）
  - `ConnectionOptions`：通訊層（控制底層連線）
- 在 `wedaapi` 模板中，可以透過 Builder 設定這些選項：
  ```csharp
  builder.AddTelemetry();           // 等同於 EnableTelemetry = true
  builder.AddCommands();            // 等同於 EnableCommands = true
  builder.ConfigurePollingInterval(1000);  // 等同於 DefaultPollingIntervalMs = 1000
  ```

---

## 步驟 3：設定裝置

在 `Program.cs` 中找到 `ConfigureDeviceConfiguration()` 方法：

```csharp
static DeviceConfiguration ConfigureDeviceConfiguration()
{
    var modbusDeviceConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",
        Manufacturer = "Advantech",
        Model = "CustomDevice-v1",
        Host = "127.0.0.1",     // Simulator 位址
        Port = 5020,            // Simulator 連接埠
        SlaveId = 1
    };

    // 定義感測器
    var tempSensor = new ModbusSensorConfiguration
    {
        Name = "temperature.sensor",
        Dtmi = "dtmi:advantech:EdgeSync:Temperature;1",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };

    modbusDeviceConfig.AddSensor(tempSensor);
    return modbusDeviceConfig.ToDeviceConfiguration();
}
```

### 自訂設定

您可以修改：
- **DeviceName**: 裝置識別名稱
- **Host/Port**: Modbus TCP 伺服器位址
- **Sensors**: 添加更多感測器

---

## 步驟 4：建置並執行

### 建置專案

```bash
dotnet build
```

**預期輸出**：
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 執行應用程式

```bash
dotnet run
```

**預期輸出**：
```
[15:30:21 INF] Starting SubNode application...
[15:30:21 INF] Modbus Simulator started on 127.0.0.1:5020
[15:30:22 INF] Device 'MySubNode' initialized successfully
[15:30:22 INF] 📊 Telemetry received:
[15:30:22 INF]   - temperature.sensor: 25.3 (DTMI: dtmi:advantech:EdgeSync:Temperature;1)
[15:30:27 INF] 📊 Telemetry received:
[15:30:27 INF]   - temperature.sensor: 25.5 (DTMI: dtmi:advantech:EdgeSync:Temperature;1)
```

**成功！** 您的裝置正在從 Simulator 讀取溫度資料 🎉

按 `Ctrl+C` 停止應用程式。

---

## 步驟 5：自訂業務邏輯

讓我們添加一些自訂邏輯：溫度過高時發出警告。

### 修改 MyFirstDevice.cs

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    private const double TemperatureThreshold = 30.0;

    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // 訂閱事件
        DataReceived += OnDataReceived;
        CommandReceived += OnCommandReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogInformation("📊 Telemetry received:");

        foreach (var measure in e.Data)
        {
            _logger.LogInformation("  - {Name}: {Value}",
                measure.Name,
                measure.Value);

            // 自訂邏輯：檢查溫度閾值
            if (measure.Name == "temperature.sensor" &&
                measure.Value is double temp &&
                temp > TemperatureThreshold)
            {
                _logger.LogWarning("🔥 HIGH TEMPERATURE ALERT! {Temp}°C exceeds threshold {Threshold}°C",
                    temp,
                    TemperatureThreshold);
            }
        }
    }

    private void OnCommandReceived(object? sender, ExecuteCommandEvent e)
    {
        _logger.LogInformation("🎮 Command received: {CommandName}", e.Command.Name);

        // 可在這裡添加命令驗證邏輯
    }

    ~MyFirstDevice()
    {
        // 取消訂閱事件
        DataReceived -= OnDataReceived;
        CommandReceived -= OnCommandReceived;
    }
}
```

### 測試自訂邏輯

修改 Simulator 設定讓溫度超過 30°C：

```csharp
static TcpModbusSimulatorConfiguration ConfigureTcpModbusSimulator()
{
    return new TcpModbusSimulatorConfiguration
    {
        // ...其他設定...
        Sensors = new List<SimulatedSensor>
        {
            new SimulatedSensor
            {
                Name = "TemperatureSensor",
                Type = SensorType.Temperature,
                StartAddress = 0,
                RegisterCount = 2,
                DataType = SimulatedDataType.Float32,
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 28.0,    // 提高最小值
                    MaxValue = 35.0,    // 提高最大值
                    InitialValue = 32.0, // 起始值超過閾值
                    ChangeRate = 0.5,
                    NoiseLevel = 0.2
                }
            }
        }
    };
}
```

重新執行：

```bash
dotnet run
```

**預期輸出**：
```
[15:35:22 INF] 📊 Telemetry received:
[15:35:22 INF]   - temperature.sensor: 32.1
[15:35:22 WRN] 🔥 HIGH TEMPERATURE ALERT! 32.1°C exceeds threshold 30°C
```

---

## 步驟 6：連接真實雲端服務

預設使用 Mock Cloud Service，切換到真實 NATS 雲端：

### 修改 Program.cs

找到這行：

```csharp
options.CloudService = WedaFactory.Cloud.Mock;
```

**✅ 方法 1: 移除 Mock（最簡單！）**

SDK 會在 `CloudService` 未設定時自動連接 NATS：

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ❌ 移除這行（或註解掉）
    // options.CloudService = WedaFactory.Cloud.Mock;

    // SDK 會自動連接到 nats://localhost:4222
});
```

**💡 SDK 預設行為：**
- 如果 `CloudService` 未設定，SDK 會自動建立真實 NATS 連線
- 預設連接到 `nats://localhost:4222`
- 可透過 `NatsConnectionSettings` 調整連線設定（參見 Section 2.4）

**🟡 方法 2: 透過 NatsConnectionSettings 設定（推薦）**

自訂 NATS 伺服器位址，使用程式化設定：

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ❌ 移除 Mock
    // options.CloudService = WedaFactory.Cloud.Mock;

    // ✅ 設定 NATS 連線
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://172.22.160.197:4224",
        CredFile = ""  // 如需認證，提供 .creds 檔案路徑
    };
});
```

**📝 方法 3: 從 appsettings.json 讀取（生產環境推薦）**

在 `appsettings.json` 中加入 `Nats` 設定區段：

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "Nats": {
    "Url": "nats://172.22.160.197:4224",
    "CredFile": "",
    "Name": "default",
    "SerializerType": "json"
  }
}
```

然後在 `Program.cs` 中，**SDK 會自動讀取 appsettings.json（最簡化版本）**：

```csharp
using var context = new WedaApplicationContext(options =>
{
    // ✅ SDK 會自動載入 appsettings.json
    // - 自動讀取 Serilog section 建立 LoggerFactory
    // - 自動讀取 Nats section 建立 CloudService
    // 完全不需要手動建立任何東西！

    // ❌ 移除 Mock
    // options.CloudService = WedaFactory.Cloud.Mock;
});
```

**如果需要自訂 Logger，仍可手動提供：**

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/myapp.log")  // 自訂輸出位置
    .CreateLogger();

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);

using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;  // 手動提供會覆蓋自動載入
});
```

**💡 SDK 自動載入行為（已大幅改進）：**
- SDK 會**自動**從當前目錄載入 `appsettings.json`
- 自動從 `Serilog` section 讀取並建立 LoggerFactory（使用 Serilog）
- 自動從 `Nats` section 讀取 NATS 連線設定
- 如果 `appsettings.json` 不存在，使用預設值：
  - LoggerFactory: `NullLoggerFactory`（不輸出任何 log）
  - NATS URL: `nats://localhost:4222`
- **不需要**手動建立 `ConfigurationBuilder`、`IConfiguration` 或 `LoggerFactory`
- **可選覆蓋**：手動設定的 `options.LoggerFactory` 或 `options.NatsConnectionSettings` 會優先使用
- 這讓使用方式更簡潔，並與 wedaapi template 的行為保持一致

### 設定環境變數（選擇性）

如果使用環境變數搭配方法 2：

```bash
export NATS_URL="nats://your-nats-server:4222"
export NATS_CREDS="/path/to/nats.creds"
dotnet run
```

然後在程式碼中：

```csharp
options.NatsConnectionSettings = new NatsConnectionSettings
{
    Url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222",
    CredFile = Environment.GetEnvironmentVariable("NATS_CREDS") ?? ""
};
```

---

## 進階功能

### 1. 訂閱更多事件

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // 訂閱所有事件
        DataReceived += OnDataReceived;
        CommandReceived += OnCommandReceived;
        ConnectionStateChanged += OnConnectionStateChanged;
        DeviceStatusChanged += OnDeviceStatusChanged;
        ConfigurationUpdateReceived += OnConfigurationUpdateReceived;
    }

    // 連線狀態變更
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEvent e)
    {
        _logger.LogInformation("Connection state: {OldState} → {NewState}",
            e.OldState,
            e.NewState);
    }

    // 裝置狀態變更
    private void OnDeviceStatusChanged(object? sender, DeviceStatusChangedEvent e)
    {
        _logger.LogInformation("Device status: {OldStatus} → {NewStatus}",
            e.OldStatus,
            e.NewStatus);
    }

    // 設定更新接收
    private void OnConfigurationUpdateReceived(object? sender, UpdateConfigurationEvent e)
    {
        _logger.LogInformation("Configuration update received");
    }

    ~MyFirstDevice()
    {
        // 取消訂閱所有事件
        DataReceived -= OnDataReceived;
        CommandReceived -= OnCommandReceived;
        ConnectionStateChanged -= OnConnectionStateChanged;
        DeviceStatusChanged -= OnDeviceStatusChanged;
        ConfigurationUpdateReceived -= OnConfigurationUpdateReceived;
    }
}
```

### 2. 添加自訂屬性與狀態追蹤

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    private int _telemetryCount = 0;
    private DateTime _lastAlertTime = DateTime.MinValue;

    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _telemetryCount++;
        _logger.LogInformation("Telemetry count: {Count}", _telemetryCount);

        // 限制警報頻率（每分鐘最多一次）
        if (DateTime.Now - _lastAlertTime > TimeSpan.FromMinutes(1))
        {
            // 發送警報邏輯
            _lastAlertTime = DateTime.Now;
        }
    }

    ~MyFirstDevice()
    {
        DataReceived -= OnDataReceived;
    }
}
```

---

## 疑難排解

### 問題 1：連線失敗

**症狀**：
```
Failed to connect to Modbus server at 127.0.0.1:5020
```

**解決方式**：
1. 確認 Simulator 已啟動（檢查日誌中的 "Simulator started" 訊息）
2. 確認 Host/Port 設定正確
3. 檢查防火牆設定

### 問題 2：沒有遙測資料

**症狀**：裝置啟動但沒有 "Telemetry received" 日誌

**解決方式**：
1. 檢查 `DeviceOptions` 是否啟用遙測：
```csharp
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = true  // 確保啟用
};
```
2. 檢查 Simulator 設定中的 `EnableValueChanges = true`

### 問題 3：編譯錯誤

**症狀**：
```
error CS0246: The type or namespace name 'TcpModbusDevice' could not be found
```

**解決方式**：
```bash
dotnet restore
dotnet build
```

---

## 最佳實踐

### ✅ 建議作法

1. **使用 async/await**：所有事件處理方法都是 async
2. **記錄日誌**：使用 `_logger` 記錄關鍵事件
3. **錯誤處理**：在自訂邏輯中使用 try-catch
4. **呼叫 base**：覆寫事件後記得呼叫 `await base.OnXxx()`

### ❌ 避免事項

1. **不要阻塞**：避免在事件處理中使用 `Thread.Sleep()`
2. **不要忽略錯誤**：捕獲並記錄所有例外
3. **不要直接修改 base class**：使用覆寫而非修改基礎類別

---

## 下一步

您已掌握 subnode 模板！探索其他選項：

### 想要更簡單的方式？
**[→ wedaapi - 簡易 API](03_wedaapi_basic.md)**
- 零程式碼
- 設定檔驅動
- 快速原型開發

### 需要完全控制？
**[→ wedaapi-c - 進階 API](04_wedaapi_c_basic.md)**
- 完整 DI 容器控制
- 手動服務註冊
- 企業級應用

### 深入學習
**[→ 使用案例](../../02_use_cases/README.md)**
- 真實世界場景
- 最佳實踐
- 進階模式

---

## 總結

在本教學中，您學會了：

- ✅ 使用 `subnode` 模板建立專案
- ✅ 理解物件導向裝置架構
- ✅ 覆寫事件處理方法
- ✅ 實作自訂業務邏輯
- ✅ 本機測試與除錯
- ✅ 連接真實雲端服務

**恭喜！** 您已建立第一個自訂 IoT 裝置 🎉

---

**版本**: 1.0.0
**最後更新**: 2025-11-09
**維護者**: Rain Hu

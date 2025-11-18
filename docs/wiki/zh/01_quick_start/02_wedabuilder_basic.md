---
title: "使用 wedabuilder 模板快速開始"
description: "使用 Web API 風格的模板建立 production 就緒的 SubNode 應用程式"
author: "Rain Hu"
date: "2025-11-12"
lang: "zh"
parent: "README"
prev: "01_install_templates"
next: "03_subnode_basic"
translations:
  - lang: "en"
    path: "../../en/01_quick_start/02_wedabuilder_basic.md"
examples:
  - "examples/basic-modbus"
templates:
  - "wedabuilder"
---

# 使用 wedabuilder 模板快速開始

學習如何使用 `wedabuilder` 模板建立 IoT 裝置應用程式。`wedabuilder` 是一個 **Web API 風格的模板**，適合 production 環境的多裝置管理。

## 模板類比

- **wedabuilder** ↔ **Web API**：production 時，用於 multi device 一次建立
- **subnode** ↔ **Console App**：方便用於單一 device 的開發、調試

**所需時間**: 10 分鐘

---

## 學習目標

完成本教學後，您將學會:

1. 使用 `dotnet new wedabuilder` 建立專案並執行
2. 理解 `CreateDefaultBuilder` 模式的自動化配置
3. 透過 `appsettings.json` 設定裝置與雲端連接

---

## 先決條件

- 已安裝 .NET 9.0 SDK
- 已安裝 Weda SubNode 模板（參考 [安裝模板](01_install_templates.md)）

---

## 目標 1: 建立並執行 wedabuilder 專案

### 步驟 1: 建立專案

```bash
# 確保您在 solution 根目錄下
# 切換到 devices 目錄(或其它自定目錄下)
mkdir devices
cd devices

# 建立專案
dotnet new wedabuilder -n MyFirstApi

# 回到 solution 根目錄
cd ..

# 加入到 solution
dotnet sln add devices/MyFirstApi/MyFirstApi.csproj

# 進入專案目錄
cd devices/MyFirstApi

# 還原 NuGet 套件
dotnet restore
```

**重要提示**：
- 由於 template 使用 project reference，專案**必須建立在 `devices/` 目錄或其它相同深度的目錄下**
- 這樣可以確保 project reference 正確解析到上層的 SDK 專案
- 必須先 `cd devices` 再執行 `dotnet new`，因為 `-n` 參數不能包含路徑分隔符

### 步驟 2: 查看專案結構

```
devices/MyFirstApi/
├── appsettings.json        # 配置檔案（包含裝置設定）
├── MyFirstApi.csproj       # 專案檔（包含 project reference）
├── MyFirstDevice.cs        # 裝置類別（繼承自 TcpModbusDevice）
└── Program.cs              # 應用程式進入點（含 Simulator 設定）
```

**重點說明**：
- 裝置配置直接寫在 `appsettings.json` 中
- `Program.cs` 包含 Modbus Simulator 的設定
- `MyFirstDevice.cs` 是範例裝置類別
- `MyFirstApi.csproj` 使用 project reference 參考 SDK 專案

### 步驟 3: 執行專案

```bash
dotnet run
```

**預期輸出**:
+ 會看到模擬器啟動的訊息
+ 會看到 subnode 啟動，並連接上雲與裝置的訊息
+ 會看到每 5 秒收到裝置的資料讀值
```
[16:31:30 INF] Starting Weda SubNode Application
[16:31:30 INF] Registered 1 device(s)
[16:31:30 INF] Initialized sensor TemperatureSensor (Temperature): Address=0, InitialValue=25.00 °C
[16:31:30 INF] Starting Modbus Simulator...
[16:31:30 INF] Starting Modbus TCP Simulator on 127.0.0.1:5020 (Slave ID: 1)
[16:31:30 INF] Modbus TCP Simulator started successfully
[16:31:30 INF] ────────────────────────────────────────────────────────
...
[16:31:30 INF] Data received from device, Count=1
[16:31:30 INF] temperature.sensor: 25
[16:31:35 INF] Data received from device, Count=1
[16:31:35 INF] temperature.sensor: 24.835852
[16:31:40 INF] Data received from device, Count=1
[16:31:40 INF] temperature.sensor: 24.680866
```

按 `Ctrl+C` 停止應用程式。

---

## 目標 2: 理解 wedabuilder 架構

### Program.cs - 簡潔的進入點

類似於 Web API，基本的結構是
```csharp
var builder = WedaApplication.CreateDefaultBuilder(args);

var app = builder.Build();
await app.RunAsync();
```

打開 `Program.cs`，您會看到以下的程式碼：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Host;
using Weda.SubNode.Simulators.Modbus;

var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();

// 註冊 Modbus simulator（自動啟動）
builder.Services.AddHostedService(sp =>
{
    var config = ConfigureTcpModbusSimulator();
    var logger = sp.GetRequiredService<ILogger<TcpModbusSimulator>>();
    return new TcpModbusSimulatorHostedService(config, logger);
});

var app = builder.Build();
await app.RunAsync();

// Simulator 配置（開發測試用）
static TcpModbusSimulatorConfiguration ConfigureTcpModbusSimulator()
{
    return new TcpModbusSimulatorConfiguration
    {
        TcpConnection = new TcpConnectionSettings
        {
            IpAddress = "127.0.0.1",
            Port = 5020
        },
        ModbusProtocol = new ModbusProtocolSettings
        {
            SlaveId = 1,
            UseModbusAddressing = false,
            HoldingRegisterBase = 0
        },
        Simulation = new SimulationSettings
        {
            GlobalUpdateIntervalSeconds = 5,
            EnableValueChanges = true
        },
        Sensors =
        [
            new SimulatedSensor
            {
                Name = "TemperatureSensor",
                Type = SensorType.Temperature,
                StartAddress = 0,
                RegisterCount = 2,
                DataType = SimulatedDataType.Float32,
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 18.0,
                    MaxValue = 32.0,
                    InitialValue = 25.0,
                    ChangeRate = 0.2,
                    NoiseLevel = 0.1
                }
            }
        ]
    };
}
```

**程式碼分析**：
1. `CreateDefaultBuilder(args)` - 自動載入配置、日誌、裝置
2. `.UseMockCloud()` - 使用 Mock 雲端服務（不需真實 NATS）
3. `AddHostedService` - 註冊 Modbus Simulator（開發測試用）

### WedaApplicationBuilder 介紹

`WedaApplicationBuilder` 提供類似 `WebApplicationBuilder` 的 DI container 功能，讓您可以註冊服務、配置依賴注入。

**內建的 SubNode 專用方法**：
- `AddDevice<TDevice>()` - 註冊自訂裝置
- `AddLogging(configure)` - 配置日誌設定

**重要提示**：
- 在 `CreateDefaultBuilder` 中，這些功能**預設都已啟用**
- 裝置會**自動從 `appsettings.json` 掃描載入**
- 日誌會自動從 `Serilog` 配置載入
- 在此階段**您不需要做任何修改**
- 進階用法（手動註冊裝置、自訂日誌）將在後續章節介紹

**範例**（進階用法，初學者可跳過）：
```csharp
// 手動註冊裝置（會自動從 appsettings.json 讀取配置）
// 方式 1: 使用類型名稱作為 config key
builder.AddDevice<MyCustomDevice>();
// SDK 會在 DeviceConfigs 中尋找 key == "MyCustomDevice" 的 section

// 方式 2: 明確指定 config section name
builder.AddDevice<MyCustomDevice>("MyFirstDevice");
// SDK 會在 DeviceConfigs 中尋找 key == "MyFirstDevice" 的 section

// appsettings.json 中應該有對應的配置：
// "DeviceConfigs": {
//   "MyCustomDevice": {  // 對應方式 1
//     "Enabled": true,
//     "DeviceName": "My Device",
//     "Communication": { "Host": "192.168.1.100", "Port": 502 },
//     ...
//   },
//   "MyFirstDevice": {  // 對應方式 2
//     "Enabled": true,
//     "DeviceName": "Another Device",
//     ...
//   }
// }

// 程式化配置日誌
builder.AddLogging(logging =>
{
    logging.SetMinimumLevel(Serilog.Events.LogEventLevel.Debug);
    logging.WriteToConsole();
});
```


### CreateDefaultBuilder 做了什麼？

`CreateDefaultBuilder` 自動幫您完成：

| 功能 | 說明 |
|------|------|
| **讀取配置** | 自動載入 `appsettings.json` |
| **設定日誌** | 根據 Serilog 配置建立 Logger |
| **連接雲端** | 根據配置連接 NATS（或使用 Mock） |
| **載入裝置** | 從 `appsettings.json` 的 `DeviceConfigs` 載入裝置 |
| **啟動服務** | 使用 Hosted Service 模式管理生命週期 |

### appsettings.json - 配置中心

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.Hosting.Lifetime": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceName": "My Modbus Device",
      "DeviceType": "AdamEthernet",
      "DeviceCapabilities": {
        "Manufacturer": "Advantech",
        "Model": "Demo"
      },
      "Communication": {
        "Host": "127.0.0.1",
        "Port": 5020,
        "SlaveId": 1
      },
      "Periods": {
        "ReadTelemetry": 2000,
        "SendTelemetry": 5000,
        "ReportHealth": 60000,
        "PollCommands": 1000
      },
      "Sensors": [
        {
          "Name": "temperature.sensor",
          "SensorGroup": "TEMP",
          "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000
          }
        }
      ]
    }
  }
}
```

**配置說明**：
- `DeviceConfigs.MyFirstDevice` - 裝置配置區塊（可以有多個裝置）
- `Communication` - 連接資訊（Host, Port, SlaveId，依裝置使用的通訊類型而定。
- `Sensors` - 感測器定義（使用 DTDL 標準）
- `Dtmi` - 參考 DTDL 定義檔（位於 `assets/dtdl/`）

### DTDL 定義檔

SDK 提供標準的 DTDL 定義檔案在 `assets/dtdl/dtmi/advantech/edgesync/devicesensors-1.json`：

```json
{
  "@id": "dtmi:advantech:EdgeSync:Temperature;1",
  "@type": "Telemetry",
  "name": "temperature",
  "displayName": "Temperature",
  "schema": "double",
  "unit": "degreeCelsius",
  "description": "Temperature measurement"
}
```

**常用的 DTMI**：
- `dtmi:advantech:EdgeSync:Temperature;1` - 溫度
- `dtmi:advantech:EdgeSync:RelativeHumidity;1` - 濕度（float）
- `dtmi:advantech:EdgeSync:Voltage;1` - 電壓
- `dtmi:advantech:EdgeSync:AI;1` - 類比輸入
- `dtmi:advantech:EdgeSync:DI;1` - 數位輸入

完整清單請參考 `assets/dtdl/dtmi/advantech/edgesync/devicesensors-1.json`

---

## 目標 3: 新增更多裝置

### 在 appsettings.json 中新增第二個裝置

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      // ... 保留原有配置
    },
    "MySecondDevice": {
      "Enabled": true,
      "DeviceName": "Humidity Sensor",
      "DeviceType": "AdamEthernet",
      "DeviceCapabilities": {
        "Manufacturer": "Advantech",
        "Model": "Demo"
      },
      "Communication": {
        "Host": "127.0.0.1",
        "Port": 5021,
        "SlaveId": 2
      },
      "Periods": {
        "ReadTelemetry": 2000,
        "SendTelemetry": 5000,
        "ReportHealth": 60000,
        "PollCommands": 1000
      },
      "Sensors": [
        {
          "Name": "humidity.sensor",
          "SensorGroup": "HUMIDITY",
          "Dtmi": "dtmi:advantech:EdgeSync:RelativeHumidity;1",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000
          }
        }
      ]
    }
  }
}
```

### 重新執行

```bash
dotnet run
```

**預期輸出**:
```
[15:30:21 INF] Found 2 device configurations
[15:30:21 INF] Device 'My Modbus Device' initialized successfully
[15:30:21 INF] Device 'Humidity Sensor' initialized successfully
[15:30:27 INF] Telemetry from My Modbus Device: temperature.sensor: 25.3
[15:30:27 INF] Telemetry from Humidity Sensor: humidity.sensor: 65.2
```

**就是這麼簡單！** 只需在 `appsettings.json` 新增配置即可。

---

## 目標 4: 配置方式比較

### 方式 1: 使用 appsettings.json（推薦）

**優點**：
- 不需修改程式碼
- 適合 production 環境
- 支援環境變數覆蓋
- 配置集中管理

**範例**：切換到真實 NATS 雲端

```json
{
  "Nats": {
    "Url": "nats://production-server:4222",
    "CredFile": "/path/to/nats.creds"
  }
}
```

然後移除 `Program.cs` 中的 `.UseMockCloud()`：

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args);
    // .UseMockCloud();  // 註解掉這行
```

### 方式 2: Programmatic 配置

如果需要動態配置，可以修改 `Program.cs`：

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args);

// 程式化覆蓋配置
builder.ConfigureNats(options =>
{
    options.Url = "nats://dynamic-server:4222",
    options.CredFile = "/path/to/nats.creds";
});

var app = builder.Build();
await app.RunAsync();
```

**使用時機**：
- 需要根據環境動態決定配置
- 需要從外部系統讀取配置
- 需要在執行時調整參數

---

## 生產環境部署建議

### 移除 Simulator

在 production 環境，移除 `Program.cs` 中的 Simulator 設定：

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args);
    // 移除 .UseMockCloud()

// 註解掉或刪除 Simulator 相關程式碼
// builder.Services.AddHostedService(sp => { ... });

var app = builder.Build();
await app.RunAsync();
```

### 使用環境變數

```bash
# 設定 NATS 連接
export Nats__Url="nats://production:4222"
export Nats__CredFile="/path/to/prod.creds"

# 執行應用程式
dotnet run
```

### 連接真實 Modbus 裝置

修改 `appsettings.json` 中的 `Communication.Host`:

```json
{
  "Communication": {
    "Host": "192.168.1.100",  // 真實裝置 IP
    "Port": 502,              // Modbus 標準 Port
    "SlaveId": 1
  }
}
```

---

## wedabuilder vs subnode 比較

| 特性 | wedabuilder | subnode |
|------|---------|---------|
| **風格** | Web API | Console App |
| **程式碼量** | 少 | 中等（需寫類別） |
| **配置方式** | appsettings.json 或 Programmatic| appsettings.json 或 Programmatic |
| **多裝置支援** | 原生支援 | 需自行實作 |
| **生命週期** | 自動管理 | 手動管理 |
| **Simulator** | Hosted Service | 手動管理 |
| **適用場景** | Production 部署 | 開發與調試 |
| **學習曲線** | 簡單 | 中等 |

---

## 總結

1. **建立專案**: `dotnet new wedabuilder` 快速建立 Web API 風格專案
2. **簡潔程式碼**: 使用 `CreateDefaultBuilder` 自動化配置
3. **配置驅動**: 所有設定都在 `appsettings.json` 的 `DeviceConfigs`
4. **多裝置管理**: 在 JSON 新增配置區塊即可加入新裝置
5. **DTDL 標準**: 使用標準化的 sensor 定義

---

## 下一步

- [subnode - Console App 風格](03_subnode_basic.md) - 需要更多控制與自訂邏輯時
- [配置方式詳解](04_configuration.md)（選讀）- 深入了解 appsettings.json vs programmatic

---

**版本**: 1.0.0
**最後更新**: 2025-11-12
**維護者**: Rain Hu

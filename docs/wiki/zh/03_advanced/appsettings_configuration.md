---
title: "appsettings.json 完整配置指南"
description: "Weda SubNode SDK 的 appsettings.json 完整配置參考文檔"
author: "Rain Hu"
date: "2025-11-17"
lang: "zh"
translations:
  - lang: "en"
    path: "../../en/03_advanced/appsettings_configuration.md"
---

# appsettings.json 完整配置指南

本文檔提供 Weda SubNode SDK 的 `appsettings.json` 完整配置參考，涵蓋所有可配置的欄位及其用途。

---

## 目錄

- [基本結構](#基本結構)
- [Serilog 日誌配置](#serilog-日誌配置)
- [NATS 訊息配置](#nats-訊息配置)
- [裝置配置 (DeviceConfigs)](#裝置配置-deviceconfigs)
  - [基本裝置欄位](#基本裝置欄位)
  - [背景任務週期設定 (Periods)](#背景任務週期設定-periods)
  - [裝置能力 (DeviceCapabilities)](#裝置能力-devicecapabilities)
  - [通訊設定 (Communication)](#通訊設定-communication)
  - [感測器配置 (Sensors)](#感測器配置-sensors)
  - [感測器 Parameters](#感測器-parameters)
  - [感測器 Config](#感測器-config)
  - [Transform Pipeline](#transform-pipeline)
  - [DSP Filter Pipeline](#dsp-filter-pipeline)
  - [Thresholds 閾值設定](#thresholds-閾值設定)
- [完整範例](#完整範例)

---

## 基本結構

`appsettings.json` 檔案包含三個主要區塊:

```json
{
  "Serilog": { ... },      // 日誌配置
  "Nats": { ... },         // NATS 訊息傳遞配置
  "DeviceConfigs": { ... } // 裝置配置（可多個裝置）
}
```

---

## Serilog 日誌配置

控制應用程式的日誌輸出行為。

```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.File",
      "Serilog.Sinks.Debug"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Weda.SubNode": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### 欄位說明

| 欄位 | 類型 | 說明 | 預設值 |
|------|------|------|--------|
| `Using` | string[] | 使用的 Serilog sink 套件 | - |
| `MinimumLevel.Default` | string | 預設最低日誌等級（Verbose/Debug/Information/Warning/Error/Fatal） | Information |
| `MinimumLevel.Override` | object | 針對特定命名空間覆蓋日誌等級 | - |
| `WriteTo` | array | 日誌輸出目標（Console, File, Debug 等） | - |

### 建議配置

- **開發環境**: `Weda.SubNode`: `Debug`
- **生產環境**: `Weda.SubNode`: `Information` 或 `Warning`

---

## NATS 訊息配置

配置 NATS 訊息傳遞連線設定,用於與雲端服務通訊。

```json
{
  "Nats": {
    "Url": "nats://172.22.160.197:4224",
    "Name": "default",
    "SerializerType": "json",
    "AuthStrategy": "None"
  }
}
```

### 基本欄位

| 欄位 | 類型 | 必填 | 說明 | 預設值 |
|------|------|------|------|--------|
| `Url` | string | 是 | NATS 伺服器位址 | `nats://localhost:4222` |
| `Name` | string | 否 | 連線名稱（用於識別） | `default` |
| `SerializerType` | string | 否 | 序列化類型（json/protobuf） | `json` |
| `AuthStrategy` | string | 否 | 認證策略（見下方說明） | `None` |

### 認證策略 (AuthStrategy)

SDK 支援以下四種 NATS 認證策略：

| 策略 | 說明 | 所需配置欄位 |
|------|------|-------------|
| `None` | 匿名連線（無認證） | 無 |
| `UserPassword` | 使用者名稱/密碼認證 | `Username`, `Password` |
| `Token` | Token 認證 | `Token` |
| `CredFile` | 憑證檔案認證（JWT + NKey） | `CredFile` |
| `TlsCert` | TLS 客戶端憑證認證 | `TlsCertPath`, `TlsKeyPath`, `TlsCaPath`（選填）|

### 認證配置範例

#### 1. 匿名連線 (None)

```json
{
  "Nats": {
    "Url": "nats://localhost:4222",
    "AuthStrategy": "None"
  }
}
```

#### 2. 使用者名稱/密碼認證 (UserPassword)

```json
{
  "Nats": {
    "Url": "nats://localhost:4222",
    "AuthStrategy": "UserPassword",
    "Username": "myuser",
    "Password": "mypassword"
  }
}
```

#### 3. Token 認證 (Token)

```json
{
  "Nats": {
    "Url": "nats://localhost:4222",
    "AuthStrategy": "Token",
    "Token": "your-auth-token"
  }
}
```

#### 4. 憑證檔案認證 (CredFile) - **生產環境推薦**

```json
{
  "Nats": {
    "Url": "nats://nats.example.com:4222",
    "AuthStrategy": "CredFile",
    "CredFile": "/path/to/credentials.creds"
  }
}
```

#### 5. TLS 客戶端憑證認證 (TlsCert) - Mutual TLS

```json
{
  "Nats": {
    "Url": "tls://nats.example.com:4222",
    "AuthStrategy": "TlsCert",
    "TlsCertPath": "/path/to/client-cert.pem",
    "TlsKeyPath": "/path/to/client-key.pem",
    "TlsCaPath": "/path/to/ca-cert.pem"
  }
}
```

### 認證欄位說明

| 欄位 | 類型 | 說明 |
|------|------|------|
| `Username` | string | UserPassword 策略的使用者名稱 |
| `Password` | string | UserPassword 策略的密碼 |
| `Token` | string | Token 策略的認證 token |
| `CredFile` | string | CredFile 策略的憑證檔案路徑（.creds 檔案） |
| `TlsCertPath` | string | TlsCert 策略的客戶端憑證路徑（PEM 格式） |
| `TlsKeyPath` | string | TlsCert 策略的私鑰路徑（PEM 格式） |
| `TlsCaPath` | string | TlsCert 策略的 CA 憑證路徑（選填，用於驗證伺服器） |

### 使用案例

- **開發環境**: 使用 `WedaFactory.Cloud.Mock` 或是 `UseMockCloud`，無需 NATS
- **測試環境**: 使用 `None` 或 `UserPassword` 策略
- **生產環境**: 連接到 Weda.Core 的 NATS server，推薦使用 `CredFile` 或 `TlsCert` 策略

---

## 裝置配置 (DeviceConfigs)

`DeviceConfigs` 是一個字典，key 為配置名稱(作為 SubNodeTypeName)，value 為裝置配置物件。

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      // 裝置配置...
    },
    "MySecondDevice": {
      // 另一個裝置配置...
    }
  }
}
```

### 基本裝置欄位

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceName": "MyWiseDevice4012",
      "SubNodeType": "adamEthernet",
      "DtdlPath": "assets/dtdl/dtmi/advantech/edgesync/wise-4012.json",
      "Periods": {
        "ReadTelemetry": 5000,
        "SendTelemetry": 0
      }
    }
  }
}
```

| 欄位 | 類型 | 必填 | 說明 | 預設值 |
|------|------|------|------|--------|
| `Enabled` | boolean | 否 | 是否啟用此裝置（僅在自動掃描時有效） | true |
| `DeviceName` | string | 是 | 裝置名稱（用於識別）會登錄到 Weda.Core | - |
| `SubNodeType` | string | 是 | 裝置類型（AdamEthernet/SerialDevice/DaqDevice/SystemMonitor/CustomDevice） | - |
| `DtdlPath` | string | 是 | DTDL 檔案路徑（相對於專案根目錄） | - |
| `Periods` | object | 否 | 背景任務週期設定（見下方說明） | 見預設值 |

#### 重要說明

- **DeviceId**: 不應設定在 appsettings.json，由雲端分配或從 localStorage 取得
- **SubNodeTypeName**: 不應設定在 appsettings.json，自動從 Config Key (如 "MyFirstDevice") 指派
- **Enabled**: 僅在使用自動掃描時有效（見下方說明）

---

### 背景任務週期設定 (Periods)

`Periods` 配置控制裝置的背景任務執行週期，包括遙測讀取、上傳和健康狀態報告。

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Periods": {
        "ReadTelemetry": 5000,
        "SendTelemetry": 0,
        "ReportHealth": 60000,
        "PollCommands": 5000,
        "ReportConfiguration": 300000
      }
    }
  }
}
```

#### Periods 欄位說明

| 欄位 | 類型 | 說明 | 預設值 |
|------|------|------|--------|
| `ReadTelemetry` | number | 遙測採樣週期（毫秒），作為 Sensor 未設定 `Interval` 時的預設值 | 5000 |
| `SendTelemetry` | number | 遙測上傳模式控制（見下方說明） | 0 |
| `ReportHealth` | number | 健康狀態報告週期（毫秒） | 60000 |
| `PollCommands` | number | 命令輪詢週期（毫秒） | 5000 |
| `ReportConfiguration` | number | 配置同步週期（毫秒），詳見下方說明 | 1800000 (30分鐘) |

#### ReadTelemetry 採樣週期

`ReadTelemetry` 定義裝置層級的預設採樣週期。當 Sensor 的 `Config.Interval` 未設定（值為 0 或負數）時，會使用此值作為 fallback。

**優先順序**:
1. `Sensor.Config.Interval`（如有設定且 > 0）
2. `Device.Periods.ReadTelemetry`（fallback 預設值）

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "Periods": {
        "ReadTelemetry": 5000  // 預設 5 秒採樣一次
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Config": {
            "Interval": 1000  // ✅ 使用 1 秒（覆蓋預設值）
          }
        },
        {
          "Name": "humidity",
          "Config": {
            "Interval": 0     // ✅ 使用 5 秒（fallback 到 ReadTelemetry）
          }
        }
      ]
    }
  }
}
```

#### SendTelemetry 上傳模式

`SendTelemetry` 控制遙測資料的上傳行為，提供兩種模式：

| 值 | 模式 | 說明 |
|----|------|------|
| `0` | 實時上傳 | 每次採樣後立即上傳資料到雲端 |
| `> 0` | 批次上傳 | 收集多次採樣資料，按指定週期批次上傳 |

##### 實時模式 (SendTelemetry = 0)

```json
{
  "Periods": {
    "ReadTelemetry": 1000,
    "SendTelemetry": 0  // 實時模式
  }
}
```

**行為**: 每 1 秒採樣一次，採樣完成後立即上傳到雲端。

**適用場景**:
- 需要即時監控的應用
- 資料即時性要求高
- 網路穩定，頻繁上傳不是問題

##### 批次模式 (SendTelemetry > 0)

```json
{
  "Periods": {
    "ReadTelemetry": 1000,
    "SendTelemetry": 10000  // 批次模式：每 10 秒上傳一次
  }
}
```

**行為**: 每 1 秒採樣一次並存入內部緩衝區，每 10 秒將緩衝區內的所有資料批次上傳。

**適用場景**:
- 網路資源有限或不穩定
- 需要減少網路請求次數
- 降低雲端服務負載
- 行動設備省電需求

#### 架構說明

SDK 採用統一的 **SensorCache 架構**，無論是 Pull 模式（如 Modbus）或 Push 模式（如 MQTT、WebSocket），資料流程皆相同：

```
┌─────────────────────────────────────────────────────────────────┐
│  Pull Mode (Modbus)           Push Mode (MQTT/WebSocket)        │
│  ┌──────────┐                 ┌──────────┐                      │
│  │ Device   │                 │ External │                      │
│  │ Request  │                 │ Message  │                      │
│  └────┬─────┘                 └────┬─────┘                      │
│       │                            │                            │
│       ▼                            ▼                            │
│  ┌──────────────────────────────────────────┐                   │
│  │            SensorCache                    │                   │
│  │  (類似 Modbus Register 的統一緩存機制)    │                   │
│  └────────────────────┬─────────────────────┘                   │
│                       │                                         │
│                       ▼  ReadTelemetry (採樣)                   │
│  ┌──────────────────────────────────────────┐                   │
│  │         Device Sampling Task             │                   │
│  │  (依 SensorConfig.Interval 週期讀取)     │                   │
│  └────────────────────┬─────────────────────┘                   │
│                       │                                         │
│         ┌─────────────┴─────────────┐                           │
│         │                           │                           │
│         ▼                           ▼                           │
│   SendTelemetry = 0           SendTelemetry > 0                 │
│   ┌─────────────┐             ┌─────────────┐                   │
│   │ 實時上傳    │             │ 批次上傳    │                   │
│   │ (立即送出)  │             │ (定時送出)  │                   │
│   └─────────────┘             └─────────────┘                   │
└─────────────────────────────────────────────────────────────────┘
```

**關鍵概念**:
- **SensorCache**: 所有資料來源（Pull/Push）都先寫入 cache，統一處理
- **採樣任務**: 依照 `SensorConfig.Interval` 或 `Periods.ReadTelemetry` 週期從 cache 讀取
- **上傳任務**: 根據 `SendTelemetry` 決定實時或批次上傳

#### 完整範例

```json
{
  "DeviceConfigs": {
    "HighFrequencyDevice": {
      "Periods": {
        "ReadTelemetry": 100,     // 100ms 高頻採樣
        "SendTelemetry": 5000,    // 每 5 秒批次上傳（避免網路擁塞）
        "ReportHealth": 30000
      },
      "Sensors": [
        {
          "Name": "vibration",
          "Config": {
            "Interval": 50       // 50ms 超高頻採樣（覆蓋預設值）
          }
        }
      ]
    },
    "LowPowerDevice": {
      "Periods": {
        "ReadTelemetry": 60000,   // 1 分鐘採樣一次
        "SendTelemetry": 300000,  // 每 5 分鐘批次上傳（省電模式）
        "ReportHealth": 600000
      }
    },
    "RealtimeMonitor": {
      "Periods": {
        "ReadTelemetry": 1000,    // 1 秒採樣
        "SendTelemetry": 0,       // 實時上傳（即時監控）
        "ReportHealth": 60000
      }
    }
  }
}
```

#### ReportConfiguration 配置同步週期

`ReportConfiguration` 控制設備定期向雲端回報當前配置狀態的週期。這是 **Digital Twin (數位雙胞胎)** 架構中維持 **Reported State** 同步的重要機制。

**為什麼需要配置同步？**

在雲端下發配置更新的流程中，可能發生以下情況：
1. 雲端下發新配置 → 設備成功套用 → **回報成功時網路斷線**
2. 此時雲端的 Desired State 與 Reported State 不一致
3. 透過定期配置同步，設備會主動上報當前配置，確保最終一致性

**配置限制**:

| 限制 | 值 | 說明 |
|------|-----|------|
| 最小值 | 60000ms (1分鐘) | 避免過於頻繁的回報造成網路負擔 |
| 預設值 | 1800000ms (30分鐘) | 平衡即時性與資源消耗 |
| 最大值 | 86400000ms (24小時) | 建議不超過此值以確保同步效果 |
| 停用 | ❌ 不允許 | 此功能為必要功能，不可停用 |

**範例配置**:

```json
{
  "Periods": {
    "ReportConfiguration": 1800000  // 每 30 分鐘同步一次（預設值）
  }
}
```

**驗證規則**:

SDK 在啟動時會驗證 `ReportConfiguration` 的值：
- 若值小於 60000ms，會拋出 `InvalidOperationException` 並終止啟動
- 錯誤訊息會明確指出允許的範圍

---

### 裝置註冊方式

SDK 提供兩種方式來註冊裝置：**自動掃描**和**手動添加**。

#### 方式 1: 自動掃描裝置配置

使用自動掃描時，SDK 會讀取 `appsettings.json` 中的 `DeviceConfigs`，並**根據 `Enabled` 欄位決定是否加入裝置**。

有兩種方式啟用自動掃描：

**選項 A: 使用 `CreateDefaultBuilder()`（推薦用於生產環境）**

```csharp
var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();
// CreateDefaultBuilder 內部會自動呼叫 ScanDevicesFromConfiguration()
```

**選項 B: 手動呼叫 `ScanDevicesFromConfiguration()`**

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .ScanDevicesFromConfiguration();  // 啟用自動掃描
var app = builder.Build();
```

**範例配置與結果**:

```json
{
  "DeviceConfigs": {
    "ProductionDevice": {
      "Enabled": true,      // ✅ 會被加入
      "DeviceName": "WISE-4012-A",
      "SubNodeType": "adamEthernet",
      "Communication": { ... }
    },
    "TestDevice": {
      "Enabled": false,     // ❌ 不會被加入
      "DeviceName": "WISE-4012-B",
      "SubNodeType": "adamEthernet",
      "Communication": { ... }
    }
  }
}
```

```csharp
// Program.cs
var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
// 結果: 只有 ProductionDevice 會被加入，TestDevice 會被跳過
```

**適用情境**:
- **生產環境**，需要動態控制多個裝置的啟用/停用
- **開發/測試環境**，需要快速切換裝置配置

---

#### 方式 2: 手動添加裝置

使用 `AddDevice<T>()` 手動指定要添加的裝置。此方式會從 `appsettings.json` 讀取配置，但**不會檢查 `Enabled` 欄位**，裝置會直接被加入。

**基本用法**:

```csharp
var builder = WedaApplication.CreateBuilder(args);

// 手動添加裝置，從 DeviceConfigs["MyDevice"] 讀取配置
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
```

**完整範例**:

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": false,     // ⚠️ 不會被檢查
      "DeviceName": "WISE-4012",
      "SubNodeType": "adamEthernet",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [ ... ]
    }
  }
}
```

```csharp
// Program.cs
var builder = WedaApplication.CreateBuilder(args);

// 手動添加 MyDevice，即使 Enabled: false 也會被加入
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();

// 結果: MyDevice 會被加入
// 所有配置（DeviceName, Communication, Sensors）都會被正確讀取
// 但 Enabled: false 不會被檢查
```

**使用工廠函數（進階用法）**:

當裝置需要自訂初始化邏輯時，可使用工廠函數：

```csharp
var builder = WedaApplication.CreateBuilder(args);

// 使用工廠函數創建裝置
builder.AddDevice(context =>
{
    var config = new TcpModbusDeviceConfiguration
    {
        DeviceName = "CustomDevice",
        Host = "192.168.1.100",
        Port = 502,
        // ... 其他配置
    };

    return new TcpModbusDevice(context, config.ToDeviceConfiguration());
});

var app = builder.Build();
```

**適用情境**:
- 單一裝置應用，完全由程式碼控制
- 需要複雜的初始化邏輯
- 需要在程式碼中動態決定裝置配置

---

#### 方式比較

| 特性 | 自動掃描 | 手動添加 |
|------|----------|----------|
| **API** | `CreateDefaultBuilder()` 或 `ScanDevicesFromConfiguration()` | `AddDevice<T>()` |
| **Enabled 欄位** | ✅ 會檢查 | ❌ 不檢查 |
| **配置來源** | `appsettings.json` | `appsettings.json` 或程式碼 |
| **適用情境** | 多裝置動態配置 | 單一裝置或自訂初始化 |
| **彈性** | 中等 | 高 |
| **程式碼複雜度** | 低 | 中等 |

#### 使用建議

| 使用情境 | 建議作法 |
|----------|----------|
| 生產環境，多裝置動態配置 | 使用 `CreateDefaultBuilder()` |
| 開發/測試，需要快速開關裝置 | 使用 `ScanDevicesFromConfiguration()` |
| 單一裝置，完全程式碼控制 | 使用 `AddDevice<T>("sectionName")` |
| 複雜初始化邏輯 | 使用 `AddDevice(factory)` |

---

### 裝置能力 (DeviceCapabilities)

描述裝置的基本資訊與能力。

```json
{
  "DeviceCapabilities": {
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SubNodeSwVersion": "0.0.1",
    "DeviceInfo": {
      "Version": "1.0.0",
      "Description": "WISE-4012 Ethernet I/O Module"
    }
  }
}
```

| 欄位 | 類型 | 必填 | 說明 |
|------|------|------|------|
| `Manufacturer` | string | 是 | 製造商名稱 |
| `Model` | string | 是 | 裝置型號 |
| `SubNodeSwVersion` | string | 是 | SubNode 軟體版本 |
| `DeviceInfo` | object | 否 | 額外裝置資訊（自訂欄位） |

---

### 通訊設定 (Communication)

根據不同 `SubNodeType` 需要不同的通訊參數。

#### Modbus TCP (SubNodeType: "adamEthernet")

```json
{
  "Communication": {
    "Host": "172.16.8.122",
    "Port": 502,
    "SlaveId": 1
  }
}
```

| 欄位 | 類型 | 必填 | 說明 | 預設值 |
|------|------|------|------|--------|
| `Host` | string | 是 | Modbus TCP 裝置 IP 位址 | - |
| `Port` | number | 否 | Modbus TCP 通訊埠 | 502 |
| `SlaveId` | number | 否 | Modbus Slave/Unit ID | 1 |

#### ISensing MQTT (SubNodeType: "adamEthernet")

```json
{
  "Communication": {
    "BrokerUrl": "mqtt://172.16.8.100:1883",
    "ClientId": "myClient",
    "Username": "user",
    "Password": "pass"
  }
}
```

---

## 感測器配置 (Sensors)

`Sensors` 是一個陣列,包含裝置上所有感測器的配置。

```json
{
  "Sensors": [
    {
      "Name": "channel.0",
      "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
      "SensorGroup": "AI",
      "Parameters": { ... },
      "Config": { ... },
      "Metadata": { ... }
    }
  ]
}
```

### 基本感測器欄位

| 欄位 | 類型 | 必填 | 說明 |
|------|------|------|------|
| `Name` | string | 是 | 感測器名稱/識別碼（如 "channel.0", "ai.channel[0]"） |
| `Dtmi` | string | 是 | 數位孿生模型識別碼（DTDL v2 規範） |
| `SensorGroup` | string | 是 | 感測器邏輯分組（AI/DI/DO/AO/TEMP/PWR/SYS） |
| `ResourceId` | string | 否 | 感測器資源 ID（自動生成） |
| `Parameters` | object | 是 | 協定特定參數（見下節） |
| `Config` | object | 是 | 感測器配置（採樣、轉換、濾波等） |
| `Metadata` | object | 否 | 額外的中繼資料 |

---

### 感測器 Parameters

協定特定的參數,不同裝置類型有不同的參數需求。

#### Modbus 感測器 Parameters

```json
{
  "Parameters": {
    "RegisterType": "HoldingRegister",
    "RegisterAddress": 0,
    "RegisterCount": 1,
    "DataType": "UInt16",
    "Batch": 1
  }
}
```

| 欄位 | 類型 | 必填 | 說明 | 可選值 | 預設值 |
|------|------|------|------|--------|--------|
| `RegisterType` | string | 是 | Modbus 暫存器類型 | HoldingRegister, InputRegister, Coil, DiscreteInput | - |
| `RegisterAddress` | number | 是 | 起始暫存器位址（0-based） | 0-65535 | - |
| `RegisterCount` | number | 否 | 讀取暫存器數量 | 1-125 | 1 |
| `DataType` | string | 是 | 資料解析類型 | UInt16, Int16, UInt32, Int32, Float, Double, Boolean | UInt16 |
| `Batch` | number | 否 | 批次最佳化 ID（通常不需要設定） | 任意整數 | - |

> **注意**: SDK 已內建**自動批次最佳化演算法**，會根據 RegisterAddress 和 RegisterCount 自動將相近的感測器合併為批次讀取，因此**通常不需要手動設定 `Batch` 參數**。只有在特殊情況下（如希望強制特定感測器分開讀取）才需要手動設定。

#### DataType 對應表

| DataType | 暫存器數量 | 位元組 | 說明 |
|----------|-----------|--------|------|
| `Boolean` | 1 | 2 | 布林值（0 或 1） |
| `UInt16` | 1 | 2 | 無符號 16 位元整數（0-65535） |
| `Int16` | 1 | 2 | 有符號 16 位元整數（-32768 至 32767） |
| `UInt32` | 2 | 4 | 無符號 32 位元整數 |
| `Int32` | 2 | 4 | 有符號 32 位元整數 |
| `Float` | 2 | 4 | 32 位元浮點數 |
| `Double` | 4 | 8 | 64 位元浮點數 |

#### 自動批次最佳化

SDK 內建**自動批次最佳化演算法**，會自動將連續或相近的暫存器合併為單次讀取，**無需手動設定 `Batch` 參數**。

**自動最佳化範例**:

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0, "RegisterCount": 1 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1, "RegisterCount": 1 } },
    { "Name": "ch2", "Parameters": { "RegisterAddress": 2, "RegisterCount": 1 } }
  ]
}
```

**SDK 自動處理**:
- SDK 會自動偵測這三個感測器使用連續的暫存器位址（0, 1, 2）
- 自動合併為單次批次讀取：`ReadBatch(0, count=3)`
- **結果**: 3 個感測器只需 1 次 Modbus 請求（而非 3 次）

**最佳化演算法**:

SDK 會根據以下規則自動合併感測器：

1. **按 RegisterType 分組**: 相同類型的暫存器（如 HoldingRegister）才會合併
2. **按 RegisterAddress 排序**: 自動排序感測器位址
3. **計算 Gap**: 計算感測器之間的間隔
4. **智慧合併**:
   - 如果 gap ≤ `MaxGapSize`（預設 2）且 批次大小 ≤ `MaxBatchSize`（預設 125）
   - 則自動合併為同一批次

> **為什麼 MaxBatchSize = 125?**
>
> 這是 **Modbus TCP/RTU 協定的限制**:
> - Modbus 協定規定單次請求最多可讀取 **125 個 holding/input registers**
> - 由 Modbus PDU (Protocol Data Unit) 最大 253 bytes 限制決定
> - 每個 register 2 bytes，扣除協定開銷後，實際可讀取約 125 個 registers
>
> **MaxGapSize 和 MaxBatchSize 的關係**:
> - `MaxGapSize = 2`: 如果兩個感測器之間間隔 ≤ 2 個暫存器，會嘗試合併
> - `MaxBatchSize = 125`: 即使間隔很小，如果合併後超過 125 個暫存器，仍會分批

**進階範例（有間隔的感測器）**:

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1 } },
    { "Name": "ch2", "Parameters": { "RegisterAddress": 2 } },
    { "Name": "ch10", "Parameters": { "RegisterAddress": 10 } }  // 間隔 8 個暫存器
  ]
}
```

**SDK 自動處理**:
- 批次 1: `ReadBatch(0, count=3)` → 讀取 ch0, ch1, ch2
- 批次 2: `ReadBatch(10, count=1)` → 讀取 ch10
- **原因**: ch2 和 ch10 之間的 gap = 8，小於 MaxGapSize(10)，但為了避免讀取未使用的暫存器，SDK 可能會分成兩個批次

#### 手動 Batch 參數（進階用法）

在極少數情況下，如果需要**強制特定感測器分開讀取**，可以手動設定 `Batch` 參數：

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0, "Batch": 1 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1, "Batch": 2 } }  // 強制分開
  ]
}
```

**何時需要手動設定**:
- 某些 Modbus 裝置在批次讀取時有問題
- 需要不同的讀取頻率（但建議用不同的 `Interval` 設定）
- **大部分情況下不需要手動設定**

---

### 感測器 Config

控制感測器的採樣、轉換、濾波和閾值設定。

```json
{
  "Config": {
    "Enabled": true,
    "Interval": 1000,
    "Unit": "celsius",
    "TransformPipeline": [ ... ],
    "DspPipeline": [ ... ],
    "Thresholds": { ... }
  }
}
```

| 欄位 | 類型 | 必填 | 說明 | 預設值 |
|------|------|------|------|--------|
| `Enabled` | boolean | 否 | 啟用/停用感測器 | true |
| `Interval` | number | 否 | 採樣間隔（毫秒） | 1000 |
| `Unit` | string | 否 | 測量單位（通常在 DTDL 中定義） | "" |
| `TransformPipeline` | array | 否 | 數據轉換 pipeline（在 DSP 之前） | [] |
| `DspPipeline` | array | 否 | DSP 濾波器 pipeline（在轉換之後） | [] |
| `Thresholds` | object | 否 | 閾值告警設定 | null |

---

### Transform Pipeline

資料轉換 pipeline 在 DSP 濾波器之前執行，用於校正、單位轉換等。

```json
{
  "TransformPipeline": [
    {
      "Type": "Calibration",
      "Enabled": true,
      "Parameters": {
        "Scale": 1.0,
        "Offset": 0.0
      }
    },
    {
      "Type": "UnitConversion",
      "Enabled": true,
      "Parameters": {
        "FromUnit": "celsius",
        "ToUnit": "fahrenheit"
      }
    }
  ]
}
```

> **執行順序**: Transform 的執行順序由 JSON array 的 index 決定（index 0 先執行，index 1 後執行）。不需要 `Order` 參數。

#### TransformConfig 欄位

| 欄位 | 類型 | 必填 | 說明 | 預設值 |
|------|------|------|------|--------|
| `Type` | string | 是 | 轉換類型（Calibration, UnitConversion, Custom） | - |
| `Enabled` | boolean | 否 | 啟用/停用此轉換 | true |
| `Parameters` | object | 否 | 轉換特定參數 | {} |

#### 常見轉換類型

##### 1. Calibration (校正)

```json
{
  "Type": "Calibration",
  "Enabled": true,
  "Parameters": {
    "Scale": 0.1,
    "Offset": -5.0
  }
}
```

**公式**: `output = (input * Scale) + Offset`

**使用案例**:
- 感測器校正
- 線性調整（y = mx + b）

##### 2. UnitConversion (單位轉換)

```json
{
  "Type": "UnitConversion",
  "Enabled": true,
  "Parameters": {
    "FromUnit": "celsius",
    "ToUnit": "fahrenheit"
  }
}
```

**支援的單位轉換**:
- 溫度: celsius ↔ fahrenheit ↔ kelvin
- 壓力: pascal ↔ bar ↔ psi
- 長度: meter ↔ feet ↔ inch
- 等等...

---

### DSP Filter Pipeline

DSP 濾波器 pipeline 在轉換之後執行,用於降噪和訊號處理。

```json
{
  "DspPipeline": [
    {
      "Type": "movingAverage",
      "Enabled": true,
      "Parameters": {
        "WindowSize": 5
      }
    },
    {
      "Type": "kalman",
      "Enabled": true,
      "Parameters": {
        "ProcessNoise": 0.01,
        "MeasurementNoise": 0.1
      }
    }
  ]
}
```

> **執行順序**: DSP Filter 的執行順序由 JSON array 的 index 決定（index 0 先執行，index 1 後執行）。不需要 `Order` 參數。

#### DspFilterConfig 欄位

| 欄位 | 類型 | 必填 | 說明 | 預設值 |
|------|------|------|------|--------|
| `Type` | string | 是 | 濾波器類型（movingAverage, kalman, lowpass, highpass） | - |
| `Enabled` | boolean | 否 | 啟用/停用此濾波器 | true |
| `Parameters` | object | 否 | 濾波器特定參數 | {} |

#### 常見濾波器類型

##### 1. movingAverage (移動平均)

```json
{
  "Type": "movingAverage",
  "Enabled": true,
  "Parameters": {
    "WindowSize": 5
  }
}
```

**參數**:
- `WindowSize`: 視窗大小（樣本數）

**使用案例**:
- 平滑化噪音資料
- 簡單的低通濾波

##### 2. kalman (卡爾曼濾波)

```json
{
  "Type": "kalman",
  "Enabled": true,
  "Parameters": {
    "ProcessNoise": 0.01,
    "MeasurementNoise": 0.1
  }
}
```

**參數**:
- `ProcessNoise`: 過程噪音（數值越小,越信任模型）
- `MeasurementNoise`: 測量噪音（數值越小,越信任測量值）

**使用案例**:
- 高精度感測器資料處理
- 預測與平滑化

##### 3. lowpass / highpass (低通/高通濾波)

```json
{
  "Type": "lowpass",
  "Enabled": true,
  "Parameters": {
    "CutoffFrequency": 10.0,
    "SamplingRate": 100.0
  }
}
```

**參數**:
- `CutoffFrequency`: 截止頻率（Hz）
- `SamplingRate`: 採樣率（Hz）

---

### Thresholds 閾值設定

設定感測器數值的告警閾值。

```json
{
  "Thresholds": {
    "UpperCritical": 100.0,
    "UpperWarning": 80.0,
    "LowerWarning": 20.0,
    "LowerCritical": 0.0
  }
}
```

| 欄位 | 類型 | 說明 |
|------|------|------|
| `UpperCritical` | number | 上限嚴重閾值 |
| `UpperWarning` | number | 上限警告閾值 |
| `LowerWarning` | number | 下限警告閾值 |
| `LowerCritical` | number | 下限嚴重閾值 |

#### 閾值等級

SDK 會自動檢查數值並回傳閾值等級:

| 閾值等級 | 條件 |
|----------|------|
| `Normal` | 數值在警告範圍內 |
| `LowerWarning` | 數值 ≤ LowerWarning |
| `UpperWarning` | 數值 ≥ UpperWarning |
| `LowerCritical` | 數值 ≤ LowerCritical |
| `UpperCritical` | 數值 ≥ UpperCritical |

---

## 完整範例

以下是一個完整的 `appsettings.json` 範例,展示所有可配置欄位:

```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.File",
      "Serilog.Sinks.Debug"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Weda.SubNode": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "Nats": {
    "Url": "nats://172.22.160.197:4224",
    "CredFile": "",
    "Name": "default",
    "SerializerType": "json"
  },
  "DeviceConfigs": {
    "MyWiseDevice": {
      "Enabled": true,
      "DeviceName": "WISE-4012-Factory-Floor",
      "SubNodeType": "adamEthernet",
      "DtdlPath": "assets/dtdl/dtmi/advantech/edgesync/wise-4012.json",
      "DeviceCapabilities": {
        "Manufacturer": "Advantech",
        "Model": "WISE-4012",
        "SubNodeSwVersion": "1.0.0",
        "DeviceInfo": {
          "Version": "1.0.0",
          "Description": "WISE-4012 Ethernet I/O Module",
          "Location": "Factory Floor A"
        }
      },
      "Communication": {
        "Host": "172.16.8.122",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 1,
            "DataType": "UInt16"
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "Unit": "celsius",
            "TransformPipeline": [
              {
                "Type": "Calibration",
                "Enabled": true,
                
                "Parameters": {
                  "Scale": 0.1,
                  "Offset": -5.0
                }
              }
            ],
            "DspPipeline": [
              {
                "Type": "movingAverage",
                "Enabled": true,
                
                "Parameters": {
                  "WindowSize": 5
                }
              }
            ],
            "Thresholds": {
              "UpperCritical": 80.0,
              "UpperWarning": 70.0,
              "LowerWarning": 10.0,
              "LowerCritical": 0.0
            }
          },
          "Metadata": {
            "Description": "Temperature sensor for monitoring factory floor",
            "Location": "Zone A"
          }
        },
        {
          "Name": "humidity",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 1,
            "RegisterCount": 1,
            "DataType": "UInt16",
            "Batch": 1
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "Unit": "percent",
            "TransformPipeline": [
              {
                "Type": "Calibration",
                "Enabled": true,
                
                "Parameters": {
                  "Scale": 0.01,
                  "Offset": 0.0
                }
              }
            ],
            "Thresholds": {
              "UpperWarning": 80.0,
              "LowerWarning": 30.0
            }
          }
        },
        {
          "Name": "pressure",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 2,
            "RegisterCount": 2,
            "DataType": "Float",
            "Batch": 1
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "Unit": "pascal",
            "DspPipeline": [
              {
                "Type": "kalman",
                "Enabled": true,
                
                "Parameters": {
                  "ProcessNoise": 0.01,
                  "MeasurementNoise": 0.5
                }
              }
            ]
          }
        }
      ]
    }
  }
}
```

---

## 最佳實踐

### 1. 不要在 appsettings.json 設定的欄位

以下欄位**不應**出現在 appsettings.json 中:

- ❌ `DeviceId` - 由雲端分配或從 localStorage 取得
- ❌ `SubNodeTypeName` - 自動從 DeviceConfigs 的 key 指派

### 2. DtdlPath 路徑

- ✅ 使用相對路徑: `"assets/dtdl/dtmi/advantech/edgesync/wise-4012.json"`
- ❌ 避免絕對路徑: `"/Users/user/project/assets/..."`
- SDK 會自動從專案根目錄或 `DTDL_BASE_PATH` 環境變數解析路徑

### 3. Batch 最佳化

對於連續位址的感測器,使用相同的 `Batch` ID 可大幅減少 Modbus 請求:

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0, "Batch": 1 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1, "Batch": 1 } },
    { "Name": "ch2", "Parameters": { "RegisterAddress": 2, "Batch": 1 } },
    { "Name": "ch10", "Parameters": { "RegisterAddress": 10, "Batch": 2 } }
  ]
}
```

### 4. Transform vs DSP Pipeline

- **Transform Pipeline**: 用於資料校正和單位轉換（確定性轉換）
- **DSP Pipeline**: 用於降噪和訊號處理（統計性濾波）
- 執行順序: `Raw Data → Transform → DSP → Output`

### 5. 日誌等級

根據環境調整日誌等級:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Weda.SubNode": "Debug"     // 開發環境
        // "Weda.SubNode": "Information"  // 生產環境
      }
    }
  }
}
```

---

## 相關文檔

- [DTDL 規範](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md)
- [Transformation Pipeline 使用案例](../02_use_cases/02_transformation.md)
- [DSP 濾波器使用案例](../02_use_cases/03_dsp_filters.md)
- [連接真實裝置](../01_quick_start/05_connect_real_device.md)

---

**版本**: 1.0.0
**最後更新**: 2025-11-17
**維護者**: Rain Hu
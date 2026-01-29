---
title: "WISE-4012 Builder 範例"
description: "使用 Builder pattern 的生產就緒 WISE-4012 整合"
version: "0.0.1"
author: "Rain Hu"
date: "2025-11-18"
lang: "zh"
---

# WISE-4012 Builder 範例

展示使用 **Builder pattern** (wedabuilder 風格) 整合 Advantech WISE-4012 工業 I/O 模組的生產就緒範例。

## 概述

本範例展示如何使用 `WedaApplication.CreateBuilder()` pattern 整合 WISE-4012 (4AI + 2AO) 模組，建議用於有多個裝置的生產環境。

- **硬體**: Advantech WISE-4012
- **Pattern**: Builder pattern (類似 ASP.NET Core)
- **協定**: Modbus TCP
- **雲端**: Weda EdgeSync Cloud
- **模板**: `wedabuilder`

## 硬體規格

**WISE-4012**:
- 4x 類比輸入通道 (16-bit)
- 2x 類比輸出通道 (12-bit)
- Modbus TCP/RTU 協定
- 工業級 (-25°C 至 +75°C)

## 先決條件

1. 已安裝 .NET 10.0 SDK
2. WISE-4012 硬體模組
3. 與 WISE-4012 的網路連線
4. Weda EdgeSync Cloud 存取權限 (或使用 Mock Cloud)

## 快速開始

### 步驟 1: 配置連線

編輯 `appsettings.json` 以符合您的 WISE-4012 配置：

```json
{
  "WedaNode": {
    "Url": "nats://your-cloud-server:4224"
  },
  "DeviceConfigs": {
    "MyFirstDeviceConfig": {
      "DeviceName": "FirstWiseDevice4012",
      "Communication": {
        "Host": "192.168.1.100",  // 您的 WISE-4012 IP 位址
        "Port": 502,
        "SlaveId": 1
      }
    }
  }
}
```

### 步驟 2: 執行範例

```bash
dotnet run

# 輸出:
# [16:45:12 INF] Device started successfully
# [16:45:13 INF] channel.0: 1024
# [16:45:13 INF] channel.1: 2048
# [16:45:13 INF] channel.2: 512
# [16:45:13 INF] channel.3: 3072
```

## 程式碼結構

### Program.cs - Builder Pattern

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args);

// 從 appsettings.json 載入裝置配置
builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");

var app = builder.Build();
await app.RunAsync();
```

### appsettings.json - 配置

配置檔案定義：
- **Serilog**: 日誌配置
- **Nats**: 雲端連線設定
- **DeviceConfigs**: 裝置和感測器配置
  - 裝置中繼資料 (名稱、型號、DTDL 路徑)
  - 通訊設定 (IP、連接埠、slave ID)
  - 感測器定義 (4個類比輸入通道)

### DTDL 中繼資料

本範例使用 DTDL (Digital Twin Definition Language) 作為中繼資料：
- 位於 `assets/dtdl/dtmi/advantech/edgesync/wise-4012.json`
- 定義裝置功能和遙測架構
- 啟用數位孿生整合

## 展示的功能

### 1. Builder Pattern
- Fluent API 配置
- 依賴注入
- 自動生命週期管理
- Hosted service pattern

### 2. 真實硬體整合
- Modbus TCP 通訊
- 多通道類比輸入讀取
- 1 秒輪詢間隔
- 生產就緒的錯誤處理

### 3. 雲端整合
- 基於 NATS 的雲端連線
- 遙測上傳 (uplink)
- 健康報告 (uplink)
- 命令接收 (downlink)
- 配置更新 (downlink)

### 4. DTDL 支援
- 數位孿生中繼資料
- 結構化遙測架構
- 裝置功能定義

## 配置選項

### Modbus 通訊

```json
"Communication": {
  "Host": "192.168.1.100",   // WISE-4012 IP 位址
  "Port": 502,               // Modbus TCP 連接埠 (預設: 502)
  "SlaveId": 1               // Modbus slave ID (預設: 1)
}
```

### 感測器配置

每個感測器 (類比輸入通道) 都可以配置：

```json
{
  "Name": "channel.0",
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
    "Interval": 1000  // 輪詢間隔（毫秒）
  }
}
```

## Pattern 對比

### wise-4012 vs wise-4012-builder

| 功能 | wise-4012 | wise-4012-builder |
|------|-----------|-------------------|
| Pattern | Manual Context | Builder Pattern |
| DI 容器 | 否 | 是 |
| Hosted Services | 手動 | 自動 |
| Configuration | 手動載入 | 自動從 appsettings.json |
| 最適合 | 學習、調試 | 生產、多裝置 |

## 疑難排解

### 無法連線到 WISE-4012

```bash
# 檢查網路連線
ping 192.168.1.100

# 檢查 Modbus TCP 連接埠是否開放
telnet 192.168.1.100 502
```

### 沒有遙測資料

1. 驗證 `appsettings.json` 中的感測器配置
2. 檢查暫存器位址是否符合 WISE-4012 文檔
3. 確保感測器 `Enabled: true`
4. 檢查日誌中的通訊錯誤

### 雲端連線問題

1. 驗證 NATS 伺服器 URL 是否正確
2. 檢查到雲端的網路連線
3. 如果啟用身份驗證，請檢查憑證
4. 檢查日誌中的連線錯誤

## 相關範例

- **[wise-4012](../wise-4012/)** - Manual Context pattern 版本
- **[wise-4012-isensing](../wise-4012-isensing/)** - iSensing 診斷功能
- **[tutorials/](../../tutorials/)** - 使用 Mock Cloud 的學習範例

## 文檔

- [Weda SubNode SDK Wiki](../../docs/wiki/zh/README.md)
- [Builder Pattern 指南](../../docs/wiki/zh/02_core_concepts/wedaapplication_builder.md)
- [DTDL 整合](../../docs/wiki/zh/03_advanced/dtdl_integration.md)

## 下一步

1. **新增 Transform Pipeline**: 參考 [tutorials/01-transform-dsp-config](../../tutorials/01-transform-dsp-config/)
2. **新增 DSP Filters**: 參考 [tutorials/02-transform-dsp-programmatic](../../tutorials/02-transform-dsp-programmatic/)
3. **多裝置**: 使用 `builder.AddDevice<T>()` 新增更多裝置
4. **自訂處理**: 在 `MyFirstDevice.cs` 中覆寫 `OnDataReceived`

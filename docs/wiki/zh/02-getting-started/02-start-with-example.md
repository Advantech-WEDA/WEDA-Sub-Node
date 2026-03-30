---
sidebar_position: 2
sidebar_label: '使用範例開始'
hide_title: true
title: '使用範例開始 | SubNode SDK'
keywords: ['SubNode', 'Example', 'WISE-4012', 'Modbus', 'Quick Start']
description: '透過執行和探索預建範例來學習 SubNode SDK。'
---

# 使用範例開始

> 透過執行和探索預建範例來學習 SubNode。

## Overview

SubNode SDK 提供多個可直接執行的範例專案，涵蓋不同協定和使用場景。本文以 WISE-4012 範例為主軸，帶你從執行、設定到理解程式碼，快速掌握 SubNode 的核心用法。

## What You'll Learn

閱讀本文後，你將能夠：

- 執行預建的 SubNode 範例專案
- 理解 `devicecfg.json` 設定與 `Program.cs` 進入點的關係
- 理解自訂裝置類別的基本結構
- 在沒有實體裝置的情況下使用模擬器進行開發

## Prerequisites

- 完成[環境準備](./01-prerequisites.md)

---

## 可用範例

`examples/` 目錄包含可直接使用的範例：

| 範例 | 說明 | 資料類型 | 協定 | 適用場景 |
|------|------|------|----------|-----|
| **wise-4012** | 工業 I/O 模組 | double + boolean | Modbus TCP | 學習基礎 |
| **wise-4012-isensing** | 研華自研協議 | double + boolean | ISensing MQTT | 學習基礎 |
| **stock-monitor** | HTTP API 整合 | double | HTTP | 自訂協定 |
| **image-sensor** | 影像串流 | image/png | MQTT | 二進位資料 |
| **air-quality-monitor** | 環境感測 | application/json | HTTP | 感測器融合 |
| **power-aggregation** | 多裝置聚合 | double | 多來源 | 資料聚合 |

---

## 執行 WISE-4012 範例

`wise-4012` 範例適合初學者。它示範了：

- 基本裝置設定
- Modbus TCP 通訊
- 感測器遙測資料收集
- 事件處理

### 步驟 1：複製儲存庫

```bash
git clone https://github.com/Advantech-Containers/WEDA-Sub-Node
cd WEDA-Sub-Node
```

### 步驟 2：導覽至範例

```bash
cd examples/wise-4012
```

### 步驟 3：檢視專案結構

```text
wise-4012/
├── .weda/                    # Runtime data (registration cache, local storage)
├── payloads/                 # Sample command payloads for testing
├── Program.cs                # Application entry point (*)
├── MyFirstDevice.cs          # Custom device implementation (*)
├── devicecfg.json            # Device and sensor configuration (*)
├── systemcfg.json            # WedaNode connection settings
├── customcfg.json            # Custom application settings (reserved)
├── appsettings.json          # Logging (Serilog) configuration
├── Dockerfile                # Container image build
├── docker-compose.yml        # One-command container deployment
└── Wise4012Example.csproj    # Project file and NuGet references
```

> **Quick-start 重點**：標示 `(*)` 的三個檔案是核心。`Program.cs` 負責啟動、`MyFirstDevice.cs` 定義裝置行為、`devicecfg.json` 定義連線與感測器。其餘檔案在進階場景才需要調整。

### 步驟 4：設定裝置

編輯 `devicecfg.json` 中的 `Host` 欄位，改為你的裝置 IP：

```json
{
  "SubNode": {
    "Name": "Wise4012",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": {
        "Host": "172.16.8.122",
        "Port": 502
      },
      "Properties": {
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "channel_0",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 1,
            "DataType": "UInt16"
          },
          "Report": {
            "Enabled": true,
            "Interval": 3000
          }
        }
      ]
    }
  }
}
```

> **Note**: 將 `"Host": "172.16.8.122"` 修改為你的裝置實際 IP 位址。

### 步驟 5：執行範例

```bash
dotnet run
```

**預期輸出：**

```text
[12:34:56 INF] SubNode started. Press Ctrl+C to stop...
[12:34:57 INF] channel_0: 1234
[12:34:57 INF] channel_1: 5678
[12:34:57 INF] channel_2: 9012
[12:34:57 INF] channel_3: 3456
[12:35:00 INF] channel_0: 1235
...
```

按 `Ctrl+C` 停止。

---

## 理解程式碼

### Program.cs

```csharp
using Weda.SubNode.Host;
using Wise4012Example;

// Create application builder with defaults
var builder = WedaApplication.CreateDefaultBuilder(args);

// Register device with configuration key
builder.AddDevice<MyFirstDevice>("MyFirstDevice");

// Build and run
var app = builder.Build();
await app.RunAsync();
```

關鍵點：

- `CreateDefaultBuilder` 從 `devicecfg.json` 和 `appsettings.json` 載入設定
- `AddDevice<T>("key")` 使用設定金鑰註冊裝置類型
- 設定金鑰（`"MyFirstDevice"`）對應到 JSON 中的 `DeviceConfigs.MyFirstDevice`

### MyFirstDevice.cs

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        // Enable event tracking
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value}",
                    sensor.Name, measure.Value);
            }
        }
    }
}
```

關鍵點：

- 繼承 `TcpModbusDevice` 以獲得 Modbus TCP 支援
- 建構函式接收 context 和設定金鑰
- 收集遙測時觸發 `DataReceived` 事件
- 透過 `Configuration.Sensors` 存取感測器設定

---

## 無硬體執行

如果你沒有實體裝置，請使用內建模擬器：

1. 導覽至包含模擬器的範本：

   ```bash
   cd templates/wedabuilder
   ```

2. 執行範本（包含 Modbus Simulator）：

   ```bash
   dotnet run
   ```

Simulator 會在 `127.0.0.1:5020` 建立虛擬 Modbus 裝置。

---

## 疑難排解

### 連線被拒絕

```text
Error: Connection refused to 172.16.8.122:502
```

**解決方案：**

- 驗證裝置 IP 位址是否正確
- 檢查網路連線（`ping 172.16.8.122`）
- 確保 Modbus TCP 連接埠（502）已開啟
- 驗證裝置已開機

### 未收到資料

**解決方案：**

- 檢查 `SlaveId` 是否符合你的裝置設定
- 驗證暫存器位址對你的裝置是否正確
- 在 `appsettings.json` 中啟用除錯日誌：

  ```json
  {
    "Serilog": {
      "MinimumLevel": {
        "Default": "Debug"
      }
    }
  }
  ```

---

## Summary

- `examples/` 目錄提供多個可直接執行的範例專案
- `devicecfg.json` 定義裝置連線和感測器設定，`Program.cs` 透過 `AddDevice<T>("key")` 將兩者串接
- 自訂裝置類別繼承 `TcpModbusDevice`，透過 `DataReceived` 事件處理遙測資料
- 沒有實體裝置時，可使用 `templates/wedabuilder` 的內建 Simulator

## See Also

- [使用範本開始](./03-start-with-template.md) - 從範本建立你自己的專案
- [感測器設定](../04-configuration/02-configuration-via-json.md) - 詳細設定感測器
- [專案結構](../03-hierarchy/01-project-structure.md) - 了解程式碼庫

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-06 | Rain Hu | Doc created. |
| 1.1.0 | 2026-03-30 | Rain Hu | Added Overview, What You'll Learn, Summary. Fixed links and formatting. |

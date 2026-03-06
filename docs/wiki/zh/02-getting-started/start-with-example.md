---
sidebar_position: 2
sidebar_label: '使用範例開始'
hide_title: true
title: '使用範例開始 - 執行預建的 SubNode 範例'
keywords: ['SubNode', 'Example', 'WISE-4012', 'Modbus', 'Quick Start']
description: '透過執行和探索預建範例來學習 SubNode'
---

# 使用範例開始

> 透過執行和探索預建範例來學習 SubNode。

## 可用範例

`examples/` 目錄包含可直接使用的範例：

| 範例 | 說明 | 協定 | 適用場景 |
|------|------|------|----------|
| **wise-4012** | 工業 I/O 模組 | Modbus TCP | 學習基礎 |
| **power-aggregation** | 多裝置聚合 | 多來源 | 資料聚合 |
| **stock-monitor** | HTTP API 整合 | HTTP | 自訂協定 |
| **image-sensor** | 影像串流 | MQTT | 二進位資料 |
| **air-quality-monitor** | 環境感測 | 混合 | 感測器融合 |

## 執行 WISE-4012 範例

`wise-4012` 範例適合初學者。它示範了：
- 基本裝置設定
- Modbus TCP 通訊
- 感測器遙測資料收集
- 事件處理

### 步驟 1：複製儲存庫

```bash
git clone https://your-repo/edge_subnode.git
cd edge_subnode
```

### 步驟 2：導覽至範例

```bash
cd examples/wise-4012
```

### 步驟 3：檢視專案結構

```
wise-4012/
├── Program.cs           # 應用程式進入點
├── MyFirstDevice.cs     # 自訂裝置實作
├── devicecfg.json       # 裝置和感測器設定
├── appsettings.json     # 日誌設定
└── wise-4012.csproj     # 專案檔
```

### 步驟 4：設定裝置

編輯 `devicecfg.json` 以符合您的裝置：

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
        "Host": "172.16.8.122",   // <-- 修改為您的裝置 IP
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
        // ... 更多感測器
      ]
    }
  }
}
```

### 步驟 5：執行範例

```bash
dotnet run
```

**預期輸出：**

```
[12:34:56 INF] SubNode started. Press Ctrl+C to stop...
[12:34:57 INF] channel_0: 1234
[12:34:57 INF] channel_1: 5678
[12:34:57 INF] channel_2: 9012
[12:34:57 INF] channel_3: 3456
[12:35:00 INF] channel_0: 1235
...
```

按 `Ctrl+C` 停止。

## 理解程式碼

### Program.cs

```csharp
using Weda.SubNode.Host;
using Wise4012Example;

// 使用預設值建立應用程式 builder
var builder = WedaApplication.CreateDefaultBuilder(args);

// 使用設定金鑰註冊裝置
builder.AddDevice<MyFirstDevice>("MyFirstDevice");

// 建置並執行
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
        // 啟用事件追蹤
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

## 無硬體執行

如果您沒有實體裝置，請使用內建模擬器：

1. 導覽至包含模擬器的範本：
   ```bash
   cd templates/wedabuilder
   ```

2. 執行範本（包含 Modbus 模擬器）：
   ```bash
   dotnet run
   ```

模擬器會在 `127.0.0.1:5020` 建立虛擬 Modbus 裝置。

## 下一步

- [使用範本開始](./start-with-template.md) - 建立您自己的專案
- [感測器設定](../04-sensor-configuration/configuration-via-json.md) - 詳細設定感測器
- [專案結構](../03-project-structure.md) - 了解程式碼庫

## 疑難排解

### 連線被拒絕

```
Error: Connection refused to 172.16.8.122:502
```

**解決方案：**
- 驗證裝置 IP 位址是否正確
- 檢查網路連線（`ping 172.16.8.122`）
- 確保 Modbus TCP 連接埠（502）已開啟
- 驗證裝置已開機

### 未收到資料

**解決方案：**
- 檢查 `SlaveId` 是否符合您的裝置設定
- 驗證暫存器位址對您的裝置是否正確
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

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

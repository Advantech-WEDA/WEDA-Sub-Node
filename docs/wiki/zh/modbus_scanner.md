---
title: Modbus 暫存器掃描器
category: modbus
order: 1
parent: null
related:
  - path: how_to_start.md
    title: 如何開始
  - path: architecture_overview.md
    title: 架構概述
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Modbus 暫存器掃描器使用指南 - 自動探測 Modbus 設備的暫存器配置
tags: [Modbus, Scanner, Protocol, Device Discovery]
---

# Modbus 暫存器掃描器

Modbus 掃描器是一個實用工具,可以幫助您在**撰寫配置之前**發現 Modbus 裝置上可用的感測器。當您使用不知道暫存器位址或資料類型的新裝置時,這特別有用。

## 功能特色

- **自動發現**: 掃描一系列 Modbus 保持暫存器
- **多類型解析**: 自動嘗試解析為 UInt16、Int16、UInt32、Int32、Float32、Float64
- **配置建議**: 生成即用型感測器配置 JSON
- **可配置**: 控制掃描範圍、延遲和要測試的資料類型
- **詳細日誌**: 查看原始十六進制/十進制值和解析結果

## 快速開始

### 1. 基本用法

```csharp
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Modbus;

// 建立最小裝置配置(掃描不需要感測器)
var config = new DeviceConfiguration
{
    DeviceName = "Scanner Test",
    DeviceType = "ModbusTCP",
    Communication = new Dictionary<string, object>
    {
        ["Host"] = "192.168.1.100",
        ["Port"] = 502,
        ["SlaveId"] = 1
    },
    Sensors = []  // 空的 - 我們將透過掃描來發現
};

// 建立裝置並連接
var communication = WedaFactory.Communication.Tcp.Create("192.168.1.100", 502);
var device = new ModbusDevice(config, communication, WedaFactory.Cloud.Null);
await device.InitializeAsync();

// 掃描暫存器
var results = await device.ScanRegistersAsync();

// 生成配置建議
var suggestions = device.GenerateSensorSuggestions(results);
```

### 2. 執行範例

我們在 `ProgramWithScan.cs` 中提供了完整的範例:

```bash
cd examples/Wise4012Example

# 首先更新 ProgramWithScan.cs 中的 IP 位址
dotnet run --project Wise4012Example.csproj -- scan
```

或直接使用 Program.cs:
```bash
# 執行掃描器版本
dotnet run -c Release
```

## 配置選項

### 掃描配置

```csharp
var scanConfig = new ModbusScanConfig
{
    StartAddress = 0,           // 起始暫存器位址
    EndAddress = 99,            // 結束暫存器位址
    RegistersPerScan = 4,       // 每次掃描讀取的暫存器數量(4 用於支援 Float64)
    DelayBetweenScans = 100,    // 掃描之間的延遲(毫秒)
    DataTypesToTest = [         // 要嘗試解析的資料類型
        ModbusDataType.UInt16,
        ModbusDataType.Int16,
        ModbusDataType.UInt32,
        ModbusDataType.Int32,
        ModbusDataType.Float32,
        ModbusDataType.Float64
    ]
};

var results = await device.ScanRegistersAsync(scanConfig);
```

### 掃描策略

**快速掃描 (0-9)**: 快速初步發現
```csharp
var quickScan = new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 9,
    DelayBetweenScans = 50
};
```

**完整掃描 (0-99)**: 全面發現
```csharp
var fullScan = new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 99,
    DelayBetweenScans = 100
};
```

**目標掃描**: 掃描特定範圍
```csharp
var targetedScan = new ModbusScanConfig
{
    StartAddress = 40000,
    EndAddress = 40010,
    RegistersPerScan = 2  // 如果您知道它們是 16/32 位元,只讀取 2 個暫存器
};
```

## 輸出格式

### 控制台輸出範例

```
================================================================================
Modbus Register Scan Results
================================================================================

Address 000 (0x0000):
  Raw Hex:  [0x4248, 0x0000, 0x0000, 0x0000]
  Raw Dec:  [16968, 0, 0, 0]
  UInt16    : 16968
  Int16     : 16968
  Float32   : 50.0

Address 002 (0x0002):
  Raw Hex:  [0x4248, 0x0000, 0x0000, 0x0000]
  Raw Dec:  [16968, 0, 0, 0]
  UInt16    : 16968
  Int16     : 16968
  Float32   : 75.5

================================================================================
Summary: 2 registers with non-zero data
================================================================================

Suggested Sensor Configurations:
Copy the following to your appsettings.json:

"Sensors": [
  {
    "Name": "sensor.0",
    "Dtmi": "dtmi:advantech:EdgeSync:Sensor;1",
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
  },
  {
    "Name": "sensor.2",
    "Dtmi": "dtmi:advantech:EdgeSync:Sensor;1",
    "Parameters": {
      "RegisterType": "HoldingRegister",
      "RegisterAddress": 2,
      "RegisterCount": 2,
      "DataType": "Float32"
    },
    "Config": {
      "Enabled": true,
      "Interval": 1000
    }
  }
]
```

### 程式化存取

```csharp
var results = await device.ScanRegistersAsync();

foreach (var result in results.Where(r => r.Success))
{
    Console.WriteLine($"Address: {result.Address}");
    Console.WriteLine($"Raw: {string.Join(", ", result.RawValues)}");

    foreach (var (dataType, value) in result.ParsedValues)
    {
        Console.WriteLine($"  {dataType}: {value}");
    }
}
```

## 使用案例

### 1. 新裝置發現

使用新的 Modbus 裝置時,掃描以發現可用的暫存器:

```csharp
// 進行完整掃描
var results = await device.ScanRegistersAsync(new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 99
});

// 取得配置建議
var suggestions = device.GenerateSensorSuggestions(results);

// 複製輸出到 appsettings.json
```

### 2. 驗證裝置配置

檢查您的裝置是否在預期的位址回應:

```csharp
// 掃描已知位址範圍
var results = await device.ScanRegistersAsync(new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 10
});

// 檢查特定位址
var address0 = results.FirstOrDefault(r => r.Address == 0);
if (address0?.Success == true)
{
    Console.WriteLine($"Address 0 has data: {address0.RawValues[0]}");
}
```

### 3. 資料類型檢測

不確定您的感測器是 Float32 還是 UInt16?掃描並比較:

```csharp
var results = await device.ScanRegistersAsync();
var address0 = results.First(r => r.Address == 0);

Console.WriteLine($"As Float32: {address0.ParsedValues[ModbusDataType.Float32]}");
Console.WriteLine($"As UInt16: {address0.ParsedValues[ModbusDataType.UInt16]}");
Console.WriteLine($"As Int16: {address0.ParsedValues[ModbusDataType.Int16]}");
```

## 疑難排解

### 找不到資料

如果掃描顯示沒有非零暫存器:

1. **檢查連接**: 驗證 IP 位址、埠號和 SlaveId
2. **裝置狀態**: 確保裝置已開機並發送資料
3. **擴大範圍**: 嘗試掃描 0-99 而不是 0-9
4. **檢查功能碼**: 掃描器使用 0x03 (讀取保持暫存器)
5. **防火牆**: 檢查網路連接性

### 逾時錯誤

如果掃描逾時:

1. **增加延遲**: 使用 `DelayBetweenScans = 200` 或更高
2. **減少範圍**: 掃描較小的範圍(例如,一次 10 個位址)
3. **檢查網路**: 確保裝置連接穩定

### 資料類型錯誤

如果解析值看起來不正確:

1. **驗證位元組順序**: 某些裝置使用不同的位元組序
2. **查看文件**: 參考裝置手冊中的暫存器映射
3. **測試不同類型**: 嘗試所有資料類型,看哪個有意義
4. **暫存器數量**: 確保您為資料類型讀取了足夠的暫存器

## 進階用法

### 自訂掃描器

如需更多控制,直接使用 `ModbusScanner`:

```csharp
using Weda.SubNode.Core.Protocols.Modbus;

var communication = WedaFactory.Communication.Tcp.Create("192.168.1.100", 502);
await communication.ConnectAsync();

var scanner = new ModbusScanner(communication, slaveId: 1, logger);
var results = await scanner.ScanHoldingRegistersAsync(scanConfig);

scanner.PrintScanResults(results, logger);
```

### 匯出為 JSON

以程式方式生成感測器配置 JSON:

```csharp
var suggestions = device.GenerateSensorSuggestions(results);
var json = JsonSerializer.Serialize(suggestions, new JsonSerializerOptions
{
    WriteIndented = true
});

File.WriteAllText("sensor-config.json", json);
```

## 資料類型參考

| 資料類型 | 暫存器數 | 大小 | 使用案例範例 |
|-----------|-----------|------|------------------|
| UInt16    | 1         | 16 位元無符號 | 狀態旗標、小型計數器 |
| Int16     | 1         | 16 位元有符號 | 溫度 (×10)、小數值 |
| UInt32    | 2         | 32 位元無符號 | 大型計數器、時間戳記 |
| Int32     | 2         | 32 位元有符號 | 大型有符號數值 |
| Float32   | 2         | IEEE 754 單精度 | 溫度、電壓、電流 |
| Float64   | 4         | IEEE 754 雙精度 | 高精度測量 |

## 最佳實務

1. **從小開始**: 在進行完整掃描之前,先進行快速掃描 (0-9)
2. **添加延遲**: 使用至少 100ms 的掃描延遲,以避免壓倒裝置
3. **驗證數值**: 將掃描結果與裝置文件交叉參考
4. **測試讀取**: 生成配置後,測試讀取以確保值正確
5. **文件記錄**: 記錄哪些位址對應哪些實體感測器

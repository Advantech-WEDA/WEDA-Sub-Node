# Power Aggregation Example

本範例展示如何使用 Weda SubNode SDK 建立一個資料聚合裝置，從多個來源裝置收集資料並計算衍生值。

## 概述

此範例實作了一個功率計算器，從電壓感測器和電流感測器收集資料，並使用公式 **P = V × I** 計算功率。

## 架構

### 類別繼承結構

```
PowerAggregatorDevice → AggregatorDevice → PubSubDeviceBase → DeviceBase
```

### 分層職責

遵循 SDK 的 Device → Parser → Communication 架構模式：

| 層級 | 類別 | 職責 |
|------|------|------|
| Device | `AggregatorDevice` | 建立 Parser (協定層) |
| Device | `PowerAggregatorDevice` | 建立 Communication (傳輸層) + `IAggregatorDefinition` (聚合邏輯) |
| Parser | `AggregatorProtocolParser` | 實作 `IPubSubProtocolParser`，轉發遙測事件 |
| Communication | `AggregatorCommunication` | 訂閱來源裝置的 `DataProcessed` 事件 |

### 資料流程

```
來源裝置 (VoltageSensor, CurrentSensor)
    ↓ DataProcessed 事件
AggregatorCommunication
    ↓ 轉發至 IAggregatorDefinition
PowerAggregatorDefinition (快取 + 同步)
    ↓ 當 V 和 I 都準備好時計算 P = V × I
AggregatorProtocolParser
    ↓ OnTelemetryReceived 事件
PubSubDeviceBase.SensorCache
    ↓ 定時取樣
EnqueueTelemetryAsync
    ↓ 批次發送
NATS
```

## 專案結構

```
power-aggregation/
├── Aggregation/                      # 聚合邏輯 (中間層)
│   ├── IAggregatorDefinition.cs      # 聚合邏輯介面
│   ├── PowerAggregatorDefinition.cs  # P = V × I 實作
│   └── ExternalDataSource.cs         # 外部資料來源設定
├── Communication/                    # 通訊層 (中間層)
│   └── AggregatorCommunication.cs    # 訂閱來源裝置事件
├── Protocols/                        # 協議層 (中間層)
│   └── AggregatorProtocolParser.cs   # PubSub 協定解析器
├── Devices/                          # 裝置基類 (中間層)
│   └── AggregatorDevice.cs           # 聚合裝置基類
├── Simulators/
│   └── SimulatorFactory.cs           # Modbus 模擬器工廠
├── assets/dtdl/                      # DTDL 定義檔案
├── CurrentSensorDevice.cs            # 電流感測器裝置 (應用層)
├── VoltageSensorDevice.cs            # 電壓感測器裝置 (應用層)
├── PowerAggregatorDevice.cs          # 功率計算裝置 (應用層)
├── Program.cs                        # 主程式進入點
├── appsettings.json                  # 設定檔
└── README.md                         # 本文件
```

### 分層說明

| 層級 | 位置 | 說明 |
|------|------|------|
| **應用層** | 專案根目錄 | 實際使用的裝置類別，如 `PowerAggregatorDevice`, `CurrentSensorDevice` |
| **中間層** | `Devices/`, `Communication/`, `Aggregation/` | 可重用的基類和元件 |

## 核心元件

### IAggregatorDefinition

定義聚合邏輯的介面，允許實作不同的計算公式：

```csharp
public interface IAggregatorDefinition
{
    IReadOnlyList<ExternalDataSource> ExternalSources { get; }
    TimeSpan SyncTimeWindow { get; }

    void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp);
    bool CanAggregate();
    IReadOnlyList<TelemetryMeasure>? Aggregate();
    void Reset();
}
```

### PowerAggregatorDefinition

實作 P = V × I 計算，具備以下特性：

- **時間同步**：確保電壓和電流資料的時間戳在 `SyncTimeWindow` (預設 5 秒) 內
- **防重複計算**：使用 `IsConsumed` 標記避免產生中間值 (IV, IV', I'V')
- **SourceKey 識別**：使用 "voltage" 和 "current" 字串識別來源，而非固定的 enum

### AggregatorDevice

聚合裝置的基類，負責建立 Parser 層：

```csharp
public class AggregatorDevice : PubSubDeviceBase
{
    public AggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        AggregatorCommunication communication)
        : base(context, configuration, CreateParser(context, communication))
    { }
}
```

### PowerAggregatorDevice

功率計算裝置，繼承自 `AggregatorDevice`，負責建立 Communication 層和聚合定義：

```csharp
public class PowerAggregatorDevice : AggregatorDevice
{
    public PowerAggregatorDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration)
    { }

    public PowerAggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateAggregatorCommunication(context, configuration))
    { }
}
```

## 設定

### appsettings.json

```json
{
  "DeviceConfigs": {
    "PowerAggregator": {
      "Enabled": true,
      "DeviceName": "PowerAggregator",
      "DeviceTypeName": "PowerAggregatorDevice",
      "DeviceType": "CustomDevice",
      "Communication": {
        "ExternalSources": [
          {
            "DeviceName": "VoltageSensor",
            "SourceKey": "voltage",
            "ResourceIds": []
          },
          {
            "DeviceName": "CurrentSensor",
            "SourceKey": "current",
            "ResourceIds": []
          }
        ]
      },
      "Sensors": [
        {
          "Name": "voltage",
          "Dtmi": "dtmi:custom:voltage;1",
          "Config": { "Enabled": true, "Interval": 5000 }
        },
        {
          "Name": "current",
          "Dtmi": "dtmi:custom:current;1",
          "Config": { "Enabled": true, "Interval": 5000 }
        },
        {
          "Name": "power",
          "Dtmi": "dtmi:custom:power;1",
          "Config": { "Enabled": true, "Interval": 5000 }
        }
      ]
    }
  }
}
```

### ExternalDataSource 設定

| 屬性 | 說明 |
|------|------|
| `DeviceName` | 來源裝置名稱，必須與 DeviceConfigs 中的裝置名稱一致 |
| `SourceKey` | 來源識別字串，用於 `IAggregatorDefinition` 識別資料來源 |
| `ResourceIds` | 選用，指定要訂閱的特定 ResourceId 列表 |
| `FriendlyName` | 選用，顯示用的友善名稱 |

## 執行

```bash
# 建置專案
dotnet build

# 執行範例
dotnet run
```

## 擴展

### 建立自訂聚合器

1. 實作 `IAggregatorDefinition` 介面
2. 建立繼承自 `AggregatorDevice` 的裝置類別
3. 在設定檔中定義 `ExternalSources`

範例：平均值聚合器

```csharp
public class AverageAggregatorDefinition : IAggregatorDefinition
{
    public IReadOnlyList<ExternalDataSource> ExternalSources { get; }
    public TimeSpan SyncTimeWindow => TimeSpan.FromSeconds(10);

    private readonly List<AggregatorDataEntry> _entries = new();

    public void OnDataReceived(string sourceName, IReadOnlyList<TelemetryMeasure> data, DateTimeOffset timestamp)
    {
        // 快取資料
    }

    public bool CanAggregate()
    {
        // 檢查是否有足夠的資料
        return _entries.Count >= ExternalSources.Count;
    }

    public IReadOnlyList<TelemetryMeasure>? Aggregate()
    {
        // 計算平均值
        var average = _entries.Average(e => e.Value);
        return new List<TelemetryMeasure>
        {
            new() { ResourceId = "average", Value = average, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        };
    }

    public void Reset() => _entries.Clear();
}
```

## 相關文件

- [Weda SubNode SDK 文件](../../docs/)
- [Stock Monitor 範例](../stock-monitor/) - 類似的架構模式
- [Custom Device 開發指南](../../docs/custom-device-development.md)

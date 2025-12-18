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
| Device | `AggregatorDevice` | 基類，負責建立 Parser、Communication、註冊 Sensors |
| Device | `PowerAggregatorDevice` | 子類，只需提供 `DefinitionFactory` |
| Parser | `AggregatorProtocolParser` | 實作 `IPubSubProtocolParser`，管理 Definitions，路由資料 |
| Communication | `AggregatorCommunication` | 訂閱來源裝置的 `DataProcessed` 事件，處理 ResourceId → SourceKey 映射 |
| Definition | `AggregatorDefinitionBase` | 基類，處理參數解析、快取、時間同步 |
| Definition | `PowerAggregatorDefinition` | 子類，只需實作 `Calculate()` 方法 |

### 資料流程

```
來源裝置 (VoltageSensor, CurrentSensor)
    ↓ DataProcessed 事件 (ResourceId = UUID 格式)
AggregatorCommunication
    ↓ ResourceId → SourceKey 映射轉換
    ↓ OnDataReceived 事件
AggregatorProtocolParser
    ↓ 路由資料到對應的 IAggregatorDefinition
PowerAggregatorDefinition (快取 + 時間同步)
    ↓ 當 V 和 I 都準備好時計算 P = V × I
    ↓ OnTelemetryReceived 事件
PubSubDeviceBase.SensorCache
    ↓ 定時取樣
EnqueueTelemetryAsync
    ↓ 批次發送
NATS
```

### ResourceId vs SourceKey

| 概念 | 格式 | 說明 |
|------|------|------|
| ResourceId | UUID (`bea9c0bc-6e47-58b2-...`) | 系統產生的識別碼，裝置註冊雲端後才有 |
| SourceKey | `DeviceName/SensorName` (`VoltageSensor/voltage001`) | 設定檔定義的識別碼，用於資料匹配 |

`AggregatorCommunication` 負責在 `ConnectAsync` 時建立 ResourceId → SourceKey 映射，並在收到資料時自動轉換。

## 專案結構

```
power-aggregation/
├── Aggregation/                       # 聚合邏輯層 (中間層)
│   ├── IAggregatorDefinition.cs       # 聚合邏輯介面
│   ├── AggregatorDefinitionBase.cs    # 聚合定義基類 (處理快取、時間同步)
│   ├── PowerAggregatorDefinition.cs   # P = V × I 實作 (繼承基類)
│   ├── AggregatorDataEntry.cs         # 快取資料項目
│   └── ExternalDataSource.cs          # 外部資料來源設定 (SourceKey)
├── Communication/                     # 通訊層 (中間層)
│   └── AggregatorCommunication.cs     # 訂閱來源裝置，處理 ResourceId 映射
├── Protocols/                         # 協議層 (中間層)
│   └── AggregatorProtocolParser.cs    # PubSub 協定解析器，管理 Definitions
├── Devices/                           # 裝置基類 (中間層)
│   └── AggregatorDevice.cs            # 聚合裝置基類
├── Simulators/
│   └── SimulatorFactory.cs            # Modbus 模擬器工廠
├── assets/dtdl/                       # DTDL 定義檔案
├── CurrentSensorDevice.cs             # 電流感測器裝置 (應用層)
├── VoltageSensorDevice.cs             # 電壓感測器裝置 (應用層)
├── PowerAggregatorDevice.cs           # 功率計算裝置 (應用層)
├── Program.cs                         # 主程式進入點
├── appsettings.json                   # 設定檔
└── README.md                          # 本文件
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

### AggregatorDefinitionBase

聚合定義的抽象基類，封裝了所有共用邏輯：

- **參數解析**：自動從 `Sensor.Parameters` 讀取來源設定
- **ExternalSources 建立**：自動解析 `"DeviceName/SensorName"` 格式
- **資料快取**：執行緒安全的快取機制
- **時間同步**：確保所有來源資料的時間戳在 `SyncTimeWindow` (預設 5 秒) 內
- **防重複計算**：使用 `IsConsumed` 標記避免產生中間值

子類只需：
1. 在建構子中指定所需的參數 keys
2. 實作 `Calculate()` 方法

### PowerAggregatorDefinition

繼承 `AggregatorDefinitionBase`，實作 P = V × I 計算：

```csharp
public class PowerAggregatorDefinition : AggregatorDefinitionBase
{
    private const string VoltageSourceKey = "VoltageSource";
    private const string CurrentSourceKey = "CurrentSource";

    public PowerAggregatorDefinition(Sensor sensor, ILogger<PowerAggregatorDefinition>? logger = null)
        : base(sensor, logger, VoltageSourceKey, CurrentSourceKey)
    {
    }

    protected override double Calculate(IReadOnlyDictionary<string, double> values)
    {
        var voltage = values[VoltageSourceKey];
        var current = values[CurrentSourceKey];
        return voltage * current;  // P = V × I
    }
}
```

### AggregatorDevice

聚合裝置的基類，負責：
- 建立 `AggregatorProtocolParser` 並註冊所有 Sensors
- 建立 `AggregatorCommunication` 處理來源裝置訂閱
- 設定 external sources 並連接各層元件

```csharp
public class AggregatorDevice : PubSubDeviceBase
{
    /// <summary>
    /// Factory delegate for creating IAggregatorDefinition from a Sensor.
    /// </summary>
    public delegate IAggregatorDefinition DefinitionFactory(Sensor sensor);

    /// <summary>
    /// 建構子遵循 WedaApplicationBuilder 慣例：
    /// (IWedaApplicationContext context, DeviceConfiguration configuration)
    /// </summary>
    public AggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        DefinitionFactory definitionFactory)
        : base(context, configuration, CreateParser(context, configuration, definitionFactory))
    { }
}
```

### PowerAggregatorDevice

功率計算裝置，**只需提供 `DefinitionFactory`**：

```csharp
public class PowerAggregatorDevice : AggregatorDevice
{
    /// <summary>
    /// 建構子遵循 WedaApplicationBuilder 慣例。
    /// 必須為 public (IWedaApplicationContext, DeviceConfiguration) 簽名。
    /// </summary>
    public PowerAggregatorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, CreateDefinitionFactory(context))
    {
    }

    /// <summary>
    /// 建立 PowerAggregatorDefinition 的 Factory。
    /// </summary>
    private static DefinitionFactory CreateDefinitionFactory(IWedaApplicationContext context)
        => sensor => new PowerAggregatorDefinition(
            sensor,
            context.GetLogger<PowerAggregatorDefinition>());
}
```

**重點**：子類只需實作 `CreateDefinitionFactory`，所有 Parser/Communication 邏輯由 `AggregatorDevice` 處理。

## 設定

### appsettings.json

```json
{
  "DeviceConfigs": {
    "PowerAggregator": {
      "Enabled": true,
      "DeviceName": "PowerAggregator",
      "SubNodeTypeName": "PowerAggregatorDevice",
      "SubNodeType": "CustomDevice",
      "Sensors": [
        {
          "Name": "power001",
          "Dtmi": "dtmi:custom:power;1",
          "SensorGroup": "PWR",
          "Parameters": {
            "DataType": "Float64",
            "VoltageSource": "VoltageSensor/voltage001",
            "CurrentSource": "CurrentSensor/current001"
          },
          "Config": { "Enabled": true, "Interval": 3000 }
        }
      ]
    }
  }
}
```

### Sensor.Parameters 設定

外部來源在 Sensor 的 `Parameters` 中定義，格式為 `"DeviceName/SensorName"`：

| 參數 | 說明 |
|------|------|
| `VoltageSource` | 電壓來源，格式 `"DeviceName/SensorName"` (e.g., `"VoltageSensor/voltage001"`) |
| `CurrentSource` | 電流來源，格式 `"DeviceName/SensorName"` (e.g., `"CurrentSensor/current001"`) |
| `DataType` | 輸出資料類型 (選用) |

`IAggregatorDefinition` 從 `Sensor.Parameters` 讀取來源設定，並建立 `ExternalDataSource` 列表。

## 執行

```bash
# 建置專案
dotnet build

# 執行範例
dotnet run
```

## 擴展

### 建立自訂聚合器

只需 **三個步驟**：

#### 步驟 1：繼承 `AggregatorDefinitionBase`

只需定義參數 keys 並實作 `Calculate()` 方法：

```csharp
public class MyAggregatorDefinition : AggregatorDefinitionBase
{
    // 定義參數 keys (對應 Sensor.Parameters 中的 key)
    private const string SourceAKey = "SourceA";
    private const string SourceBKey = "SourceB";

    public MyAggregatorDefinition(Sensor sensor, ILogger? logger = null)
        : base(sensor, logger, SourceAKey, SourceBKey)  // 傳入所需的參數 keys
    {
    }

    // 實作計算邏輯
    protected override double Calculate(IReadOnlyDictionary<string, double> values)
    {
        var a = values[SourceAKey];
        var b = values[SourceBKey];
        return a + b;  // 你的公式
    }

    // 可選：自訂日誌輸出
    protected override void LogCalculation(IReadOnlyDictionary<string, double> values, double result)
    {
        Logger.LogInformation("[MyAggregator] {A} + {B} = {Result}",
            values[SourceAKey], values[SourceBKey], result);
    }
}
```

基類自動處理：
- 從 `Sensor.Parameters` 讀取來源設定
- 建立 `ExternalSources` 列表
- 執行緒安全的資料快取
- 時間同步檢查
- 重複計算防護

#### 步驟 2：建立 Device 子類

只需提供 `DefinitionFactory`：

```csharp
public class MyAggregatorDevice : AggregatorDevice
{
    public MyAggregatorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, CreateDefinitionFactory(context))
    {
    }

    private static DefinitionFactory CreateDefinitionFactory(IWedaApplicationContext context)
        => sensor => new MyAggregatorDefinition(
            sensor,
            context.GetLogger<MyAggregatorDefinition>());
}
```

#### 步驟 3：設定 appsettings.json

```json
{
  "DeviceConfigs": {
    "MyAggregator": {
      "DeviceName": "MyAggregator",
      "SubNodeTypeName": "MyAggregatorDevice",
      "SubNodeType": "CustomDevice",
      "Sensors": [
        {
          "Name": "output001",
          "Parameters": {
            "SourceA": "DeviceA/sensorA",
            "SourceB": "DeviceB/sensorB"
          }
        }
      ]
    }
  }
}
```

### 進階用法

如果需要更複雜的邏輯（例如三個以上的來源、動態來源數量），可以直接實作 `IAggregatorDefinition` 介面。

## 相關文件

- [Weda SubNode SDK 文件](../../docs/)
- [Stock Monitor 範例](../stock-monitor/) - 類似的架構模式
- [Custom Device 開發指南](../../docs/custom-device-development.md)

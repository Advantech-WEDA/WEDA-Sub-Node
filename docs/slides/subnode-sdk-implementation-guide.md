# SubNode SDK 實作指南

## 概述

SubNode SDK 提供三層式架構，讓開發者可以根據設備類型選擇最適合的實作方式。

---

## 架構總覽

```
┌─────────────────────────────────────────────────────────────────────┐
│                        你的應用程式 (Your Application)                │
├─────────────────────────────────────────────────────────────────────┤
│   Level 1: 研華設備        │  Level 2: 自定義協議   │  Level 3: 自定義通訊  │
│   ─────────────────        │  ─────────────────    │  ─────────────────   │
│   繼承 TcpModbusDevice     │  實作 IProtocolParser │  實作 ICommunication │
│   只需修改 Config          │  繼承 DeviceBase      │  + IProtocolParser   │
│                            │                       │  + DeviceBase        │
│   工作量: ⭐               │  工作量: ⭐⭐         │  工作量: ⭐⭐⭐       │
└─────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────┐
│                         SDK 提供的基礎設施                            │
├──────────────────────┬──────────────────────┬───────────────────────┤
│    Device Layer      │   Protocol Layer     │  Communication Layer  │
│    設備層            │   協議層             │  通訊層               │
├──────────────────────┼──────────────────────┼───────────────────────┤
│  • DeviceBase        │  • IProtocolParser   │  • ICommunication     │
│  • RequestResponse-  │  • ModbusParser      │  • TcpCommunication   │
│    DeviceBase        │  • OpcUaParser       │  • SerialCommunication│
│  • PubSubDeviceBase  │  • MqttParser        │  • MqttCommunication  │
│  • StreamingDevice-  │                      │  • WebSocket-         │
│    Base              │                      │    Communication      │
└──────────────────────┴──────────────────────┴───────────────────────┘
```

---

## 三種實作情境

| 情境 | 適用條件 | 需實作 | 複雜度 |
|------|----------|--------|--------|
| **Level 1** | 研華設備 (如 Modbus TCP) | 僅修改 `devicecfg.json` | ⭐ 低 |
| **Level 2** | 非研華設備，但使用 SDK 支援的通訊層 | `IProtocolParser` + 繼承 `DeviceBase` | ⭐⭐ 中 |
| **Level 3** | 通訊協議也不在 SDK 支援範圍 | `ICommunication` + `IProtocolParser` + `DeviceBase` | ⭐⭐⭐ 高 |

---

## DeviceBase 的三種通訊模式

SDK 根據不同的通訊模式，提供三種 DeviceBase 子類別：

### 1. RequestResponseDeviceBase (請求-回應模式)

**適用協議:** Modbus TCP/RTU, OPC-UA, REST API, BACnet

**資料流程:**
```
Device 主動輪詢 → 請求設備 → 收到回應 → EnqueueTelemetry → 批次發送
```

**特點:**
- 主動輪詢 (Polling)
- Sensors 依 Interval 分組
- 內建自動重連機制 (Polly)

**繼承範例:**
```
MyFirstDevice → TcpModbusDevice → ModbusDevice → RequestResponseDeviceBase → DeviceBase
```

---

### 2. PubSubDeviceBase (發佈-訂閱模式)

**適用協議:** MQTT, NATS, AMQP, Kafka

**資料流程:**
```
Message Broker 推送 → SensorCache → 定時採樣 → EnqueueTelemetry → 批次發送
```

**特點:**
- 被動接收 (Event-driven)
- 使用 SensorCache 緩存最新值
- 自動訂閱與取消訂閱

**繼承範例:**
```
MyMqttDevice → MqttDevice → PubSubDeviceBase → DeviceBase
```

---

### 3. StreamingDeviceBase (串流模式)

**適用協議:** WebSocket, gRPC Streaming, SSE (Server-Sent Events)

**資料流程:**
```
Stream 持續推送 → SensorCache → 定時採樣 → EnqueueTelemetry → 批次發送
```

**特點:**
- 雙向串流支援
- 持續連線
- 使用 SensorCache 緩存最新值

**繼承範例:**
```
MyWebSocketDevice → WebSocketStreamingDevice → StreamingDeviceBase → DeviceBase
```

---

## 通訊模式選擇指南

```
                    你的設備通訊方式是？
                           │
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
      主動輪詢          訂閱訊息        持續串流
    (Request/Reply)    (Pub/Sub)      (Streaming)
           │               │               │
           ▼               ▼               ▼
   RequestResponse-   PubSubDevice-   StreamingDevice-
      DeviceBase          Base            Base
           │               │               │
           ▼               ▼               ▼
    Modbus, OPC-UA    MQTT, NATS     WebSocket, gRPC
    REST API, BACnet  AMQP, Kafka         SSE
```

---

## ICommunication 介面架構

```
ICommunication (Base)
    │
    ├── IRequestResponseCommunication<TRequest, TResponse>
    │       ├── RequestAsync(request) → response
    │       └── 適用: TCP, HTTP, Modbus RTU, OPC-UA
    │
    ├── IPubSubCommunication<TMessage>
    │       ├── SubscribeAsync(topic)
    │       ├── PublishAsync(topic, message)
    │       ├── MessageReceived event
    │       └── 適用: MQTT, NATS, RabbitMQ
    │
    └── IStreamingCommunication<TRequest, TResponse>
            ├── StreamAsync(requests) → IAsyncEnumerable<response>
            └── 適用: WebSocket, gRPC
```

### ICommunication 基礎屬性

| 屬性/方法 | 說明 |
|-----------|------|
| `ConnectionSettings Settings` | 連線設定 (Retry, Timeout) |
| `CommunicationState State` | 連線狀態 |
| `bool IsConnected` | 是否已連線 |
| `ConnectAsync()` | 建立連線 |
| `DisconnectAsync()` | 斷開連線 |
| `StateChanged` event | 連線狀態變更事件 |

---

## IProtocolParser 介面架構

```
IProtocolParserCore (Base)
    │
    ├── IRequestResponseProtocolParser
    │       ├── ReadTelemetryAsync()
    │       ├── ExecuteCommandAsync()
    │       └── WriteSensorDataAsync()
    │
    ├── IPubSubProtocolParser
    │       ├── StartAsync() / StopAsync()
    │       └── OnTelemetryReceived event
    │
    └── IStreamingProtocolParser
            ├── StartStreamAsync() / StopStreamAsync()
            ├── OnTelemetryReceived event
            └── StreamState property
```

### IProtocolParser 核心方法

| 方法 | 說明 |
|------|------|
| `Parse(rawData)` | 解析原始資料 |
| `Encode(value)` | 編碼資料 |
| `ParseSensorData(payload)` | 轉換為 TelemetryMeasure |
| `EncodeCommand(command)` | 編碼設備指令 |

---

## 完整資料處理流程 (Data Processing Flow)

這是資料從實體設備到雲端的完整流程：

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                              SubNode Data Processing Pipeline                             │
└──────────────────────────────────────────────────────────────────────────────────────────┘

┌─────────────┐      ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│  Physical   │      │ ICommunication  │      │ IProtocolParser │      │  TelemetryMeasure│
│   Device    │ ───► │                 │ ───► │                 │ ───► │                 │
│             │      │  (Raw Bytes)    │      │   (Parsing)     │      │   (Structured)  │
└─────────────┘      └─────────────────┘      └─────────────────┘      └────────┬────────┘
                                                                                 │
                     ┌───────────────────────────────────────────────────────────┘
                     ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              DeviceBase.EnqueueTelemetryAsync()                          │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   ┌─────────────────────┐      ┌─────────────────────┐      ┌─────────────────────┐     │
│   │  ITelemetryTransform │      │  ITelemetryTransform │      │     IDspFilter      │     │
│   │                     │      │                     │      │                     │     │
│   │  • UnitConversion   │ ───► │  • Calibration      │ ───► │  • MovingAverage    │     │
│   │    (單位轉換)        │      │    (校正補償)        │      │  • KalmanFilter     │     │
│   │                     │      │                     │      │  • ReluFilter       │     │
│   └─────────────────────┘      └─────────────────────┘      └──────────┬──────────┘     │
│                                                                         │               │
│                                    Transform Pipeline                   │  DSP Pipeline │
└─────────────────────────────────────────────────────────────────────────┼───────────────┘
                                                                          │
                     ┌────────────────────────────────────────────────────┘
                     ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                  Batch Queue & Send                                      │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                          │
│   ┌─────────────────────┐      ┌─────────────────────┐      ┌─────────────────────┐     │
│   │   Telemetry Batch   │      │     WedaNode        │      │     WedaCore        │     │
│   │      Queue          │ ───► │   (Edge Gateway)    │ ───► │    (Cloud)          │     │
│   │                     │      │                     │      │                     │     │
│   └─────────────────────┘      └─────────────────────┘      └─────────────────────┘     │
│                                                                                          │
│              CalculatedSendTelemetryPeriod (最小 sensor interval)                        │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 資料流程圖 (簡化版)

```
Physical Device
      │
      │ Raw Bytes (TCP/MQTT/WebSocket...)
      ▼
┌─────────────────┐
│ ICommunication  │  ← 通訊層：連線管理、收發資料
└────────┬────────┘
         │ byte[]
         ▼
┌─────────────────┐
│ IProtocolParser │  ← 協議層：解析協議、對應 Sensor
└────────┬────────┘
         │ List<TelemetryMeasure>
         ▼
┌─────────────────┐
│ ITelemetryTrans-│  ← 轉換層：單位轉換、校正補償
│ form Pipeline   │
└────────┬────────┘
         │ List<TelemetryMeasure>
         ▼
┌─────────────────┐
│ IDspFilter      │  ← 濾波層：平滑、降噪、異常過濾
│ Pipeline        │
└────────┬────────┘
         │ List<TelemetryMeasure>
         ▼
┌─────────────────┐
│ Batch Queue     │  ← 批次層：累積、定時發送
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ WedaNode        │  ← Edge Gateway
└────────┬────────┘
         │ NATS
         ▼
┌─────────────────┐
│ WedaCore        │  ← Cloud Platform
└─────────────────┘
```

---

## 各階段介面說明

### 1. ICommunication - 通訊層

負責與實體設備的底層通訊。

```csharp
// 三種通訊模式
IRequestResponseCommunication<byte[], byte[]>  // Modbus, OPC-UA
IPubSubCommunication<byte[]>                   // MQTT, NATS
IStreamingCommunication<byte[], byte[]>        // WebSocket, gRPC
```

**輸入:** 無 (主動輪詢) 或 Event (被動接收)
**輸出:** `byte[]` (Raw Data)

---

### 2. IProtocolParser - 協議層

解析通訊層的原始資料，轉換為結構化的 TelemetryMeasure。

```csharp
public interface IProtocolParser<TRawData, TValue>
{
    TValue Parse(TRawData rawData);
    List<TelemetryMeasure> ParseSensorData(byte[] payload, SensorMapping? mapping);
}
```

**輸入:** `byte[]` (Raw Data)
**輸出:** `List<TelemetryMeasure>`

---

### 3. ITelemetryTransform - 轉換層

對 TelemetryMeasure 進行數值轉換 (Pipeline 模式)。

```csharp
public interface ITelemetryTransform
{
    string Name { get; }
    bool Enabled { get; set; }

    Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken ct);
}
```

**SDK 內建 Transform:**
| Transform | 說明 | 參數範例 |
|-----------|------|----------|
| `UnitConversion` | 單位轉換 | `{ "FromUnit": "C", "ToUnit": "F" }` |
| `Calibration` | 校正補償 | `{ "Offset": 0.5, "Scale": 1.02 }` |

**輸入:** `List<TelemetryMeasure>`
**輸出:** `List<TelemetryMeasure>` (轉換後)

---

### 4. IDspFilter - 數位訊號處理層

對 TelemetryMeasure 進行訊號處理 (Pipeline 模式)。

```csharp
public interface IDspFilter
{
    bool Enabled { get; set; }

    IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        CancellationToken ct);
}
```

**SDK 內建 Filter:**
| Filter | 說明 | 參數範例 |
|--------|------|----------|
| `MovingAverage` | 移動平均 | `{ "WindowSize": 5 }` |
| `KalmanFilter` | 卡爾曼濾波 | `{ "ProcessNoise": 0.01, "MeasurementNoise": 0.1 }` |
| `ReluFilter` | ReLU 過濾 (去負值) | `{ "Threshold": 0 }` |

**輸入:** `IAsyncEnumerable<TelemetryMeasure>`
**輸出:** `IAsyncEnumerable<TelemetryMeasure>` (濾波後)

---

## 資料處理設定範例

在 `devicecfg.json` 中設定 Transform 和 Filter：

```json
{
  "Name": "TemperatureSensor",
  "Sensors": [
    {
      "Name": "Temperature",
      "ResourceId": "temp-01",
      "Config": {
        "RegisterAddress": 100,
        "Interval": 1000
      },
      "Transforms": [
        {
          "Type": "Calibration",
          "Parameters": { "Offset": -0.5, "Scale": 1.0 }
        },
        {
          "Type": "UnitConversion",
          "Parameters": { "FromUnit": "C", "ToUnit": "F" }
        }
      ],
      "DspFilters": [
        {
          "Type": "MovingAverage",
          "Parameters": { "WindowSize": 5 }
        }
      ]
    }
  ]
}
```

---

## 自定義 Transform 範例

```csharp
public class MyCustomTransform : ITelemetryTransform, IConfigurableTransform<MyCustomTransform>
{
    // Factory 用
    public static string TypeName => "mycustom";

    public static MyCustomTransform Create(Dictionary<string, object> parameters)
    {
        var multiplier = Convert.ToDouble(parameters["Multiplier"]);
        return new MyCustomTransform(multiplier);
    }

    // Instance 屬性
    public string Name => "MyCustomTransform";
    public bool Enabled { get; set; } = true;

    private double _multiplier;

    public MyCustomTransform(double multiplier) => _multiplier = multiplier;

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken ct)
    {
        foreach (var m in measures)
        {
            if (m.Value is double val)
                m.Value = val * _multiplier;
        }
        return Task.FromResult(measures);
    }

    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> p) => Result.Success;
    public void UpdateParameters(Dictionary<string, object> p)
        => _multiplier = Convert.ToDouble(p["Multiplier"]);
}
```

---

## 自定義 DSP Filter 範例

```csharp
public class MyCustomFilter : IDspFilter, IConfigurableDspFilter<MyCustomFilter>
{
    public static string TypeName => "mycustomfilter";

    public static MyCustomFilter Create(Dictionary<string, object> parameters)
    {
        var threshold = Convert.ToDouble(parameters["Threshold"]);
        return new MyCustomFilter(threshold);
    }

    public bool Enabled { get; set; } = true;
    private double _threshold;

    public MyCustomFilter(double threshold) => _threshold = threshold;

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var measure in input.WithCancellation(ct))
        {
            // 只輸出超過 threshold 的值
            if (measure.Value is double val && val > _threshold)
                yield return measure;
        }
    }

    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> p) => Result.Success;
    public void UpdateParameters(Dictionary<string, object> p)
        => _threshold = Convert.ToDouble(p["Threshold"]);
}
```

---

## 洋蔥圖 - 分層架構

```
┌───────────────────────────────────────────────────────────┐
│                  Your Application                         │
│                                                           │
│   ┌───────────────────────────────────────────────────┐   │
│   │              DeviceBase                           │   │
│   │   (Lifecycle, Config, Telemetry, Health, Events)  │   │
│   │                                                   │   │
│   │   ┌───────────────────────────────────────────┐   │   │
│   │   │          IProtocolParser                  │   │   │
│   │   │    (Parse, Encode, SensorMapping)         │   │   │
│   │   │                                           │   │   │
│   │   │   ┌───────────────────────────────────┐   │   │   │
│   │   │   │       ICommunication              │   │   │   │
│   │   │   │  (Connect, Send, Receive, State)  │   │   │   │
│   │   │   └───────────────────────────────────┘   │   │   │
│   │   │                                           │   │   │
│   │   └───────────────────────────────────────────┘   │   │
│   │                                                   │   │
│   └───────────────────────────────────────────────────┘   │
│                                                           │
└───────────────────────────────────────────────────────────┘
                            │
                            ▼
                    Physical Device
```

---

## 金字塔圖 - 實作複雜度

```
                    ╱╲
                   ╱  ╲
                  ╱ L1 ╲      ← Config Only (研華設備)
                 ╱──────╲        繼承 TcpModbusDevice
                ╱   L2   ╲    ← Implement IProtocolParser
               ╱──────────╲      繼承 XXXDeviceBase
              ╱    L3      ╲  ← Implement ICommunication
             ╱──────────────╲    + IProtocolParser + DeviceBase
            ╱________________╲
```

---

## Level 1 範例: Config Only

```json
{
  "Name": "MyModbusDevice",
  "DeviceCommunication": {
    "Type": "TcpModbus",
    "Host": "192.168.1.100",
    "Port": 502,
    "SlaveId": 1
  },
  "Sensors": [
    {
      "Name": "Temperature",
      "ResourceId": "temp-01",
      "Config": {
        "RegisterAddress": 100,
        "RegisterLength": 2,
        "DataType": "Float32",
        "Interval": 1000
      }
    }
  ]
}
```

```csharp
// 只需一行程式碼
public class MyDevice : TcpModbusDevice
{
    public MyDevice(IWedaApplicationContext context)
        : base(context, "devicecfg") { }
}
```

---

## Level 2 範例: 自定義協議

```csharp
// 1. 實作 Protocol Parser
public class MyProtocolParser : IRequestResponseProtocolParser
{
    public ICommunication Communication { get; }

    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        CancellationToken ct)
    {
        // 使用 SDK 提供的 Communication
        var response = await _communication.RequestAsync(
            BuildRequest(), ct);
        return ParseResponse(response);
    }
}

// 2. 繼承 RequestResponseDeviceBase
public class MyDevice : RequestResponseDeviceBase
{
    public MyDevice(
        IWedaApplicationContext context,
        DeviceConfiguration config)
        : base(context, config,
               new MyProtocolParser(CreateTcpCommunication(config)))
    { }
}
```

---

## Level 3 範例: 自定義通訊層

```csharp
// 1. 實作 ICommunication
public class MySerialCommunication
    : IRequestResponseCommunication<byte[], byte[]>
{
    public CommunicationState State { get; private set; }

    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        // 自定義連線邏輯
    }

    public async Task<byte[]> RequestAsync(
        byte[] request, CancellationToken ct)
    {
        // 自定義請求-回應邏輯
    }
}

// 2. 實作 IProtocolParser
public class MyProtocolParser : IRequestResponseProtocolParser
{
    public MyProtocolParser(MySerialCommunication comm)
    {
        Communication = comm;
    }
    // ... 實作解析邏輯
}

// 3. 繼承 DeviceBase
public class MyDevice : RequestResponseDeviceBase
{
    public MyDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config,
               new MyProtocolParser(new MySerialCommunication(config)))
    { }
}
```

---

## DeviceBase 提供的功能

### 生命週期管理
- `InitializeAsync()` - 初始化設備
- `StartAsync()` - 啟動設備
- `StopAsync()` - 停止設備

### Telemetry 管道
- `ReadTelemetryAsync()` - 讀取感測器數據
- `EnqueueTelemetryAsync()` - 加入批次佇列 (含 Transform/Filter)
- `SendTelemetryAsync()` - 發送至雲端

### 組態管理
- `ValidateConfigurationUpdateAsync()` - 驗證組態更新
- `ApplyValidatedConfigurationAsync()` - 套用組態
- `RollbackConfigurationAsync()` - 回滾組態

### 健康狀態
- `GetHealthAsync()` - 取得設備健康狀態
- `ReportHealthAsync()` - 回報健康狀態

### 生命週期 Hook (可覆寫)
- `OnBeforeInitializeAsync()`
- `OnAfterInitializeAsync()`
- `OnBeforeConfigUpdateAsync()`
- `OnAfterConfigUpdateAsync()`
- `OnBeforeCommandAsync()`
- `OnAfterCommandAsync()`

---

## 總結

| 你的情境 | 選擇 | 需要做的事 |
|----------|------|-----------|
| 研華 Modbus 設備 | `TcpModbusDevice` | 修改 `devicecfg.json` |
| 非研華，用 TCP | `RequestResponseDeviceBase` | 實作 `IProtocolParser` |
| 非研華，用 MQTT | `PubSubDeviceBase` | 實作 `IPubSubProtocolParser` |
| 非研華，用 WebSocket | `StreamingDeviceBase` | 實作 `IStreamingProtocolParser` |
| 通訊層也要自己寫 | 任一 DeviceBase | 實作 `ICommunication` + Parser |

---

## 檔案位置參考

| 元件 | 路徑 |
|------|------|
| ICommunication | `src/Weda.SubNode.Abstractions/Communication/ICommunication.cs` |
| IProtocolParser | `src/Weda.SubNode.Abstractions/Protocols/IProtocolParser.cs` |
| DeviceBase | `src/Weda.SubNode.Core/Devices/DeviceBase.cs` |
| RequestResponseDeviceBase | `src/Weda.SubNode.Core/Devices/RequestResponseDeviceBase.cs` |
| PubSubDeviceBase | `src/Weda.SubNode.Core/Devices/PubSubDeviceBase.cs` |
| StreamingDeviceBase | `src/Weda.SubNode.Core/Devices/StreamingDeviceBase.cs` |
| TcpModbusDevice | `src/Weda.SubNode.Devices/Generic/TcpModbusDevice.cs` |

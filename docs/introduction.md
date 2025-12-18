# Weda SubNode SDK - Developer Guide

## 什麼是 SubNode？

**Weda SubNode SDK** 是一個 .NET 9.0 框架，用於建立 IoT 邊緣節點，將邊緣設備連接到雲端管理平台 (Weda.Core)。

SubNode 扮演 **Edge 與 Cloud 之間的橋樑**，負責：

- **Uplink (資料收集)**：從邊緣設備收集資料 → 處理 → 上傳至雲端
- **Downlink (設備控制)**：接收雲端指令 → 執行於邊緣設備

```
┌────────────────────┐          ┌────────────────────────┐          ┌─────────────────────┐        ┌─────────────────────┐
│    Edge Devices    │          │      Weda SubNode      │          │     Weda.Node       │        │     Weda.Core       │
│                    │          │                        │          │                     │        │                     │
│  ┌──────────────┐  │  Modbus  │  ┌──────────────────┐  │   NATS   │                     │        │    Transceiver      │
│  │  PLC Device  │──┼──────────┼─▶│   Custom Device  │──┼──────────┼─▶    Device         │────────┼─▶                   │
│  └──────────────┘  │          │  └──────────────────┘  │          │       Mgmt          │        │                     │
│  ┌──────────────┐  │  MQTT    │  ┌──────────────────┐  │          │      Agent          │        │   Digital Twin      │
│  │ MQTT Sensor  │──┼──────────┼─▶│   MQTT Device    │  │          │      (DMA)          │        │   Shadow Agent      │
│  └──────────────┘  │          │  └──────────────────┘  │          │                     │        │                     │
│  ┌──────────────┐  │  HTTP    │  ┌──────────────────┐  │          │                     │        │   Container Mgmt    │
│  │  REST API    │──┼──────────┼─▶│   HTTP Device    │  │          │                     │        │                     │
│  └──────────────┘  │          │  └──────────────────┘  │          │                     │        │         etc         │
└────────────────────┘          └────────────────────────┘          └─────────────────────┘        └─────────────────────┘
```

---

## 架構設計

### 分層架構 (OSI 7-Layer Model)

SubNode 遵循 OSI 模型，具有清晰的關注點分離：

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ Your Application Code (Business Logic)                                      │
│ - Event Handlers (DataReceived, CommandReceived)                            │
│ - Custom business rules                                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│ IDevice (DeviceBase)                                              Layer 7   │
│ - Lifecycle: Initialize → Start → Stop → Dispose                           │
│ - Cloud: Register, SendTelemetry, ReportHealth                              │
│ - Events: DataReceived, CommandReceived, ConnectionStateChanged             │
├─────────────────────────────────────────────────────────────────────────────┤
│ Device Base Classes                                               Layer 7   │
│ ┌─────────────────────────┬─────────────────────────┬─────────────────────┐ │
│ │ RequestResponseDeviceBase│ MessageBrokerDeviceBase │ StreamingDeviceBase │ │
│ │ (Modbus, OPC-UA, REST)  │ (MQTT, ISensing, NATS)  │ (WebSocket, gRPC)   │ │
│ └─────────────────────────┴─────────────────────────┴─────────────────────┘ │
├─────────────────────────────────────────────────────────────────────────────┤
│ IProtocolParser                                                   Layer 7   │
│ - Parse raw data → TelemetryMeasure                                         │
│ - Execute commands                                                          │
│ - **重要：Parser 擁有 Communication 實例**                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│ ITelemetryTransform & IDspFilter                                  Layer 7   │
│ - Formula: (x * 1.8) + 32                                                   │
│ - Moving Average, Low-Pass Filter                                           │
│ - Anomaly Detection                                                         │
├─────────────────────────────────────────────────────────────────────────────┤
│ ICommunication                                                  Layer 4-7   │
│ - Connection lifecycle (Connect, Disconnect, Reconnect)                     │
│ - Auto-reconnect with Polly resilience                                      │
│ - TCP/UDP/WebSocket transport                                               │
├─────────────────────────────────────────────────────────────────────────────┤
│ OS Network Stack                                                Layer 1-4   │
│ - TCP/IP, Ethernet, WiFi, Serial                                            │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 核心設計原則：Parser 擁有 Communication

這是 SubNode 最重要的設計模式：

```csharp
public interface IRequestResponseProtocolParser
{
    // Parser 擁有並管理 Communication 實例
    ICommunication Communication { get; }

    Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken ct);
    Task<ErrorOr<object>> ExecuteCommandAsync(DeviceCommand cmd, CancellationToken ct);
}
```

**為什麼這樣設計？**
- Parser 負責所有協定邏輯
- Device 類別保持簡潔，專注於業務邏輯
- 容易替換不同的 Communication 實作

---

## 三種通訊模式

SubNode 支援三種主要的通訊模式，對應不同的 Device Base Class：

| 模式 | Base Class | 適用協定 | 特點 |
|------|-----------|---------|------|
| **Request-Response** | `RequestResponseDeviceBase` | Modbus, OPC-UA, REST | 週期性輪詢 |
| **Publish-Subscribe** | `MessageBrokerDeviceBase` | MQTT, ISensing, NATS | 事件驅動 |
| **Streaming** | `StreamingDeviceBase` | WebSocket, gRPC | 持續串流 |

---

## 如何開發 Custom Device

### 方法一：繼承現有 Device 類別（推薦）

如果你的協定已被支援（如 Modbus），直接繼承即可：

```csharp
using Weda.SubNode.Core.Devices;

public class MyTemperatureController : TcpModbusDevice
{
    public MyTemperatureController(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // 訂閱資料接收事件
        EnableDataReceivedTracking = true;
        DataReceived += OnTemperatureReceived;
    }

    private void OnTemperatureReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            if (measure.Name == "temperature" && measure.Value is double temp)
            {
                if (temp > 30.0)
                {
                    _logger.LogWarning("High temperature: {Temp}°C", temp);
                    // 觸發警報、控制冷卻系統等
                }
            }
        }
    }
}
```

### 方法二：自訂協定（完整實作）

如果你需要支援新的協定，需要實作三個元件：

#### Step 1: 建立 Communication 類別

```csharp
using Weda.SubNode.Abstractions.Communication;

public class MyCustomCommunication : IRequestResponseCommunication<byte[], byte[]>
{
    public ConnectionSettings Settings { get; }
    public CommunicationState State { get; private set; }
    public bool IsConnected => State == CommunicationState.Connected;

    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        // 實作連線邏輯
        State = CommunicationState.Connected;
        return true;
    }

    public async Task<byte[]> RequestAsync(byte[] request, CancellationToken ct)
    {
        // 發送請求，接收回應
        return responseBytes;
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        State = CommunicationState.Disconnected;
    }

    public void Dispose() { }
}
```

#### Step 2: 建立 Protocol Parser 類別

```csharp
using Weda.SubNode.Abstractions.Protocols;

public class MyCustomParser : IRequestResponseProtocolParser
{
    private readonly MyCustomCommunication _communication;
    private readonly DeviceConfiguration _configuration;
    private readonly ILogger<MyCustomParser> _logger;

    public MyCustomParser(
        DeviceConfiguration configuration,
        MyCustomCommunication communication,
        ILogger<MyCustomParser> logger)
    {
        _configuration = configuration;
        _communication = communication;
        _logger = logger;
    }

    // 重要：Parser 擁有 Communication
    public ICommunication Communication => _communication;

    public string ProtocolName => "MyCustomProtocol";
    public IReadOnlyList<string> SupportedDataTypes => ["temperature", "humidity"];
    public bool SupportsBidirectional => false;

    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken ct)
    {
        // 1. 透過 Communication 發送請求
        var request = BuildRequest();
        var response = await _communication.RequestAsync(request, ct);

        // 2. 解析回應為 TelemetryMeasure
        return ParseResponse(response);
    }

    private List<TelemetryMeasure> ParseResponse(byte[] data)
    {
        var measures = new List<TelemetryMeasure>();

        // 根據 DeviceConfiguration 中的 Sensor 設定進行解析
        foreach (var sensor in _configuration.Sensors.Where(s => s.Config.Enabled))
        {
            var value = ExtractValue(data, sensor);
            measures.Add(new TelemetryMeasure
            {
                Name = sensor.Name,
                ResourceId = sensor.ResourceId,
                Value = value,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        return measures;
    }

    public Task<ErrorOr<object>> ExecuteCommandAsync(DeviceCommand cmd, CancellationToken ct)
    {
        // 實作指令執行邏輯
        return Task.FromResult<ErrorOr<object>>(new object());
    }

    public Task<bool> WriteSensorDataAsync(IEnumerable<TelemetryMeasure> measures, CancellationToken ct)
    {
        return Task.FromResult(false); // 如果不支援寫入
    }
}
```

#### Step 3: 建立 Device 類別

```csharp
using Weda.SubNode.Core.Devices;

public class MyCustomDevice : RequestResponseDeviceBase
{
    public MyCustomDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateParser(context, configuration))
    {
        _logger.LogDebug("MyCustomDevice initialized");
    }

    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var loggerFactory = context.LoggerFactory;

        // 建立 Communication
        var communication = new MyCustomCommunication(
            configuration.ConnectionSettings,
            loggerFactory.CreateLogger<MyCustomCommunication>());

        // 建立 Parser（Parser 擁有 Communication）
        return new MyCustomParser(
            configuration,
            communication,
            loggerFactory.CreateLogger<MyCustomParser>());
    }
}
```

---

## 完整使用範例

參考 `examples/system-monitor/` 專案：

### Program.cs

```csharp
using Microsoft.Extensions.Configuration;
using Weda.SubNode.Host.Context;

// 1. 載入設定
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// 2. 建立 Application Context
using var context = new WedaApplicationContext(
    configuration,
    deviceConfigKey: "SystemMonitorDeviceConfig");

var config = context.DeviceConfiguration
    ?? throw new InvalidOperationException("Device configuration not found");

// 3. 建立 Device
var device = new SystemMonitorDevice(context, config);

// 4. 初始化（建立連線、向雲端註冊）
if (!await device.InitializeAsync())
{
    Console.WriteLine("Failed to initialize");
    return;
}

// 5. 啟動（開始背景任務：遙測讀取、健康回報）
await device.StartAsync();
Console.WriteLine("Device started. Press Ctrl+C to stop...");

// 6. 等待中斷訊號
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException) { }

// 7. 停止並清理
await device.StopAsync();
device.Dispose();
```

### appsettings.json

```json
{
  "SystemMonitorDeviceConfig": {
    "DeviceName": "my-system-monitor",
    "SubNodeType": "systemMonitor",
    "Periods": {
      "ReadTelemetry": 5000,
      "ReportHealth": 30000
    },
    "Sensors": [
      {
        "Name": "cpu.cpuLoad1",
        "ResourceId": "dtmi:example:cpu:load1;1",
        "Config": { "Enabled": true }
      },
      {
        "Name": "ram.ramMemAvailableBytes",
        "ResourceId": "dtmi:example:ram:available;1",
        "Config": { "Enabled": true }
      }
    ]
  }
}
```

---

## 元件關係圖

```
DeviceConfiguration
├── DeviceInfo (name, type)
├── Sensors[]
│   ├── Name, ResourceId
│   ├── Transforms[] (Formula, UnitConversion)
│   └── DspFilters[] (MovingAverage, LowPass)
├── Communication{} (protocol settings)
└── ConnectionSettings (retry, timeout)

Device (Your Custom Class)
├── Inherits: RequestResponseDeviceBase | MessageBrokerDeviceBase | StreamingDeviceBase
├── Implements: IDevice interface
├── Creates: IProtocolParser in constructor
└── Subscribes: DataReceived, CommandReceived events

IProtocolParser
├── Owns: ICommunication (核心設計模式!)
├── Reads: DeviceConfiguration for sensor mappings
├── Parses: Raw data → TelemetryMeasure
└── Applies: Transforms & Filters

ICommunication
├── Manages: Connection lifecycle
├── Handles: Request/Response or Pub/Sub
└── Supports: Auto-reconnect with Polly
```

---

## 資料處理流程

```
Raw Device Data (bytes/JSON/etc.)
         ↓
┌─────────────────────────────┐
│ ICommunication              │  ← 負責傳輸
└─────────────────────────────┘
         ↓
┌─────────────────────────────┐
│ IProtocolParser             │  ← 負責解析
│ - Parse to TelemetryMeasure │
└─────────────────────────────┘
         ↓
┌─────────────────────────────┐
│ Sensor-Level Transforms     │  ← 資料轉換
│ - Formula: (x * 1.8) + 32   │
│ - Unit Conversion           │
└─────────────────────────────┘
         ↓
┌─────────────────────────────┐
│ Sensor-Level DSP Filters    │  ← 訊號處理
│ - Moving Average            │
│ - Low-Pass Filter           │
└─────────────────────────────┘
         ↓
┌─────────────────────────────┐
│ Device-Level Processing     │  ← 設備層處理
└─────────────────────────────┘
         ↓
┌─────────────────────────────┐
│ Cloud Service (NATS)        │  ← 上傳雲端
└─────────────────────────────┘
```

---

## Device 生命週期

```
┌──────────────┐
│   Created    │
└──────┬───────┘
       │ new Device()
       ▼
┌──────────────┐
│ Uninitialized│
└──────┬───────┘
       │ InitializeAsync()
       │ - Connect to device
       │ - Register with cloud
       ▼
┌──────────────┐
│ Initialized  │
└──────┬───────┘
       │ StartAsync()
       │ - Start telemetry polling
       │ - Start health reporting
       ▼
┌──────────────┐
│   Running    │ ◄─── Normal operation
└──────┬───────┘
       │ StopAsync()
       │ - Stop background tasks
       │ - Disconnect
       ▼
┌──────────────┐
│   Stopped    │
└──────┬───────┘
       │ Dispose()
       ▼
┌──────────────┐
│   Disposed   │
└──────────────┘
```

---

## 快速參考

### 選擇正確的 Base Class

| 你的需求 | 使用的 Base Class | Parser Interface |
|---------|------------------|------------------|
| 週期性輪詢 (Modbus, REST) | `RequestResponseDeviceBase` | `IRequestResponseProtocolParser` |
| 訂閱訊息 (MQTT, ISensing) | `MessageBrokerDeviceBase` | `IPublishSubscribeProtocolParser` |
| 持續串流 (WebSocket) | `StreamingDeviceBase` | `IStreamingProtocolParser` |

### 常用事件

```csharp
// 訂閱資料接收事件
device.EnableDataReceivedTracking = true;
device.DataReceived += (s, e) => {
    foreach (var measure in e.Data)
    {
        Console.WriteLine($"{measure.Name}: {measure.Value}");
    }
};

// 訂閱連線狀態變更
device.EnableConnectionStateTracking = true;
device.ConnectionStateChanged += (s, e) => {
    Console.WriteLine($"Connection: {e.OldState} → {e.NewState}");
};

// 訂閱指令接收事件
device.EnableCommandReceivedTracking = true;
device.CommandReceived += (s, e) => {
    Console.WriteLine($"Command received: {e.Command.DeviceCmd}");
};
```

### 重要檔案位置

| 檔案 | 說明 |
|-----|------|
| `src/Weda.SubNode.Abstractions/Devices/IDevice.cs` | Device 核心介面 |
| `src/Weda.SubNode.Abstractions/Protocols/IRequestResponseProtocolParser.cs` | Request-Response Parser 介面 |
| `src/Weda.SubNode.Core/Devices/RequestResponseDeviceBase.cs` | Request-Response Base Class |
| `examples/system-monitor/` | 完整範例 |

---

## 下一步

1. **快速開始**：參考 [Quick Start Guide](wiki/en/01_quick_start/00_overview.md)
2. **安裝範本**：參考 [Install Templates](wiki/en/01_quick_start/01_install_templates.md)
3. **進階設定**：參考 [Configuration Guide](wiki/en/03_advanced/appsettings_configuration.md)

---

**Version**: 1.0.0
**Last Updated**: 2025-12-04
**Maintainer**: Rain Hu

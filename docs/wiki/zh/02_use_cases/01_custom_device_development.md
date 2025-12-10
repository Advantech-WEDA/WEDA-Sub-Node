---
title: "客製化裝置開發指南"
description: "從零開始開發自訂 IoT 裝置，以 system-monitor 與 stock-monitor 為實作範例"
author: "Rain Hu"
date: "2025-12-10"
lang: "zh"
parent: "README"
examples:
  - "examples/system-monitor"
  - "examples/stock-monitor"
---

# 客製化裝置開發指南

本指南說明如何開發客製化的 IoT 裝置，適用於非 Modbus 協定的場景。我們將以 `system-monitor`（本機系統監控）和 `stock-monitor`（台灣證交所股價監控）作為實作範例。

**所需時間**: 30-45 分鐘

---

## 學習目標

完成本教學後，您將學會:

1. 理解 **Device → Parser → Communication** 三層架構
2. 使用 `WedaApplication.CreateBuilder()` 建立應用程式
3. 透過 `appsettings.json` 配置裝置與感測器
4. 實作自訂 Communication、Parser 和 Device 類別

---

## 架構概覽

### 三層架構

客製化裝置遵循 **Device → Parser → Communication** 的分層架構：

```
┌───────────────────────────────────────────────────────────────────┐ 
│                         Program.cs                                │
│  WedaApplication.CreateBuilder() -> AddDevice<T>() -> Build()     │
└───────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────┐ 
│                      Device Layer                                 │
│  - Manages device lifecycle                                       │
│  - Subscribes to DataReceived event for telemetry processing      │
│  - Creates Parser and Communication instances                     │
│  Examples: TwseStockMonitorDevice, LocalSystemMonitorDevice       │
└───────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────┐ 
│                     Parser Layer                                  │
│  - Implements IRequestResponseProtocolParser interface            │
│  - Handles protocol parsing and data transformation               │
│  - Uses sensor.Parameters for sensor-specific parameters          │
│  - Returns List<TelemetryMeasure>                                 │
│  Examples: TwseStockParser, SystemMetricsParser                   │
└───────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌───────────────────────────────────────────────────────────────────┐ 
│                 Communication Layer                               │
│  - Inherits RequestResponseCommunicationBase<TRequest, TResponse> │
│  - Handles low-level data access (HTTP, file, OS API, etc.)       │
│  - Implements RequestAsync() method                               │
│  Examples: HttpCommunication, LocalSystemCommunication            │
└───────────────────────────────────────────────────────────────────┘
```

### DeviceBase 類型選擇

SDK 提供三種 DeviceBase 基類，對應不同的通訊模式：

| DeviceBase 類型 | 通訊模式 | 適用協定 | 資料流向 |
|----------------|---------|---------|---------|
| `RequestResponseDeviceBase` | Request-Response | Modbus, REST API, OPC-UA | Device 主動輪詢 |
| `PubSubDeviceBase` | Pub/Sub | MQTT, NATS, AMQP, Kafka | 外部推送 → SensorCache → 採樣 |
| `StreamingDeviceBase` | Streaming | WebSocket, gRPC streaming, SSE | 串流推送 → SensorCache → 採樣 |

**選擇指南：**

- **RequestResponseDeviceBase** - 裝置由 SubNode 主動輪詢資料
  - 適用於傳統工業協定 (Modbus TCP/RTU)
  - 適用於 REST API (如本指南的 stock-monitor)
  - 適用於 OPC-UA Read 操作

- **PubSubDeviceBase** - 裝置透過訊息代理推送資料
  - 適用於 MQTT 裝置 (如 iSensing)
  - 資料由外部推送到 SensorCache，再依 Interval 採樣上傳

- **StreamingDeviceBase** - 裝置透過持續連線串流資料
  - 適用於 WebSocket 連線
  - 適用於 gRPC 串流
  - 資料由串流推送到 SensorCache，再依 Interval 採樣上傳

### 類別繼承關係

```
RequestResponseDeviceBase (SDK)
    │
    ├── StockMonitorDevice (Intermediate Device)
    │       └── TwseStockMonitorDevice (Concrete Implementation)
    │
    └── SystemMonitorDevice (Intermediate Device)
            └── LocalSystemMonitorDevice (Concrete Implementation)
```

---

## 專案結構

以 `stock-monitor` 為例的建議專案結構：

```
examples/stock-monitor/
├── appsettings.json              # Config (device, sensors, NATS connection)
├── Program.cs                    # Application entry point
├── TwseStockMonitorDevice.cs     # Concrete device (creates Communication)
├── Devices/
│   └── StockMonitorDevice.cs     # Intermediate device (creates Parser)
├── Protocols/
│   ├── TwseStockParser.cs        # Protocol parser
│   ├── StockSensorParameters.cs  # Sensor parameters class
│   └── SensorMetricsConfig.cs    # Sensor config helper class
├── Communication/
│   ├── HttpCommunication.cs      # Communication layer (HTTP requests)
│   └── TwseStockClient.cs        # TWSE API client
├── Models/
│   └── TwseModels.cs             # API response models
└── dtdl/
    └── stock-price.json          # DTDL definition file
```

---

## 步驟 1: 配置 appsettings.json

### 基本結構

```json
{
  "Serilog": { },
  "Nats": { },
  "DeviceConfigs": {
    "YourDeviceConfigKey": {
      "Enabled": true,
      "DeviceName": "YourDevice-001",
      "DeviceType": "CustomDevice",
      "DeviceTypeName": "YourDeviceType",
      "DtdlPath": "path/to/dtdl/file.json",
      "DeviceCapabilities": { },
      "Communication": { },
      "Periods": { },
      "Sensors": [ ]
    }
  }
}
```

### 感測器配置範例

**stock-monitor (多指標感測器)**:

```json
{
  "Sensors": [
    {
      "Name": "2395",
      "SensorGroup": "AI",
      "Dtmi": "dtmi:advantech:EdgeSync:StockPrice;1",
      "Parameters": {
        "StockCode": "2395",
        "Metrics": "Current,Volume,Open,High,Low,Change,ChangePercent"
      },
      "Config": {
        "Enabled": true,
        "Interval": 10000
      }
    }
  ]
}
```

**system-monitor (單指標感測器)**:

```json
{
  "Sensors": [
    {
      "Name": "cpu.usage",
      "SensorGroup": "SYS",
      "Dtmi": "dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1",
      "Parameters": {
        "MetricType": "cpu",
        "MetricName": "usage"
      },
      "Config": {
        "Enabled": true,
        "Interval": 5000
      }
    },
    {
      "Name": "memory.total",
      "SensorGroup": "SYS",
      "Dtmi": "dtmi:advantech:EdgeSync:SystemInfo:MemoryTotal;1",
      "Parameters": {
        "MetricType": "memory",
        "MetricName": "total"
      },
      "Config": {
        "Enabled": true,
        "Interval": 10000
      }
    }
  ]
}
```

### 感測器參數說明

| 欄位 | 說明 | 範例 |
|------|------|------|
| `Name` | 感測器名稱 (唯一識別) | `"cpu.usage"`, `"2395"` |
| `SensorGroup` | 感測器群組 | `"AI"`, `"SYS"`, `"DO"`, `"DI"` |
| `Dtmi` | Digital Twin Model ID | `"dtmi:advantech:EdgeSync:Temperature;1"` |
| `Parameters` | 協定特定參數 (由 Parser 解析) | `{"StockCode": "2395"}` |
| `Config.Enabled` | 是否啟用 | `true` / `false` |
| `Config.Interval` | 採樣間隔 (毫秒) | `5000`, `10000` |

**重要**: `Parameters` 是 `Dictionary<string, object>`，Parser 需要自行解析其內容。

---

## 步驟 2: 實作 Communication Layer

Communication 層負責底層資料存取，繼承 `RequestResponseCommunicationBase<TRequest, TResponse>`。

### HTTP Communication 範例 (stock-monitor)

```csharp
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication;

namespace StockMonitor.Communication;

/// <summary>
/// HTTP request object
/// </summary>
public record StockQuoteRequest(IEnumerable<string> StockCodes);

/// <summary>
/// HTTP communication implementation
/// </summary>
public class HttpCommunication : RequestResponseCommunicationBase<StockQuoteRequest, TwseStockResponse?>
{
    private readonly TwseStockClient _stockClient;

    public HttpCommunication(
        TwseStockClient stockClient,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        _stockClient = stockClient;
    }

    /// <summary>
    /// HTTP is stateless - ConnectCoreAsync just marks as connected
    /// </summary>
    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("HTTP communication connected (stateless)");
        State = CommunicationState.Connected;
        return Task.FromResult(true);
    }

    /// <summary>
    /// HTTP is stateless - DisconnectAsync just marks as disconnected
    /// </summary>
    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = CommunicationState.Disconnected;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Implements Request-Response pattern
    /// </summary>
    public override async Task<TwseStockResponse?> RequestAsync(
        StockQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _stockClient.GetStockQuotesAsync(request.StockCodes, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            State = CommunicationState.Error;
            throw;
        }
    }
}
```

### Local System Communication 範例 (system-monitor)

```csharp
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication;

namespace SystemMonitorExample.Communication;

/// <summary>
/// System metrics request object
/// </summary>
public record SystemMetricsRequest(HashSet<string> MetricTypes);

/// <summary>
/// Local system communication implementation
/// </summary>
public class LocalSystemCommunication : RequestResponseCommunicationBase<SystemMetricsRequest, SystemMetricsRawData>
{
    private readonly SystemResourceCollector _collector;

    public LocalSystemCommunication(
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        _collector = new SystemResourceCollector(_logger);
    }

    /// <summary>
    /// Verifies access to system APIs
    /// </summary>
    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var processorCount = Environment.ProcessorCount;
            _logger.LogDebug("Local system communication connected. Processors: {Count}", processorCount);
            State = CommunicationState.Connected;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to local system APIs");
            State = CommunicationState.Error;
            return Task.FromResult(false);
        }
    }

    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = CommunicationState.Disconnected;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Collects system metrics
    /// </summary>
    public override async Task<SystemMetricsRawData> RequestAsync(
        SystemMetricsRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _collector.CollectMetricsAsync(request.MetricTypes, cancellationToken);
    }
}
```

---

## 步驟 3: 實作 Parser Layer

Parser 層實作 `IRequestResponseProtocolParser` 介面，負責:
1. 從 `DeviceConfiguration.Sensors` 讀取感測器配置
2. 解析 `sensor.Parameters` 中的協定特定參數
3. 呼叫 Communication 取得原始資料
4. 轉換為 `List<TelemetryMeasure>` 回傳

### 重要介面方法

```csharp
public interface IRequestResponseProtocolParser : IProtocolParserCore
{
    /// <summary>
    /// Reads telemetry data for all enabled sensors
    /// </summary>
    Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads telemetry data for specific sensors (supports different sampling intervals)
    /// </summary>
    Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes command (if protocol supports)
    /// </summary>
    Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default);
}
```

### Parser 實作範例 (SystemMetricsParser)

```csharp
public class SystemMetricsParser : IRequestResponseProtocolParser
{
    private readonly DeviceConfiguration _configuration;
    private readonly LocalSystemCommunication _communication;
    private readonly ILogger<SystemMetricsParser> _logger;

    public ICommunication Communication => _communication;
    public string ProtocolName => "LocalSystem";
    public IReadOnlyList<string> SupportedDataTypes => ["cpu", "memory", "disk", "network"];
    public bool SupportsBidirectional => false;

    public SystemMetricsParser(
        DeviceConfiguration configuration,
        LocalSystemCommunication communication,
        ILogger<SystemMetricsParser> logger)
    {
        _configuration = configuration;
        _communication = communication;
        _logger = logger;
    }

    /// <summary>
    /// Reads all enabled sensors
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var enabledSensors = _configuration.Sensors.Where(s => s.Config.Enabled).ToList();
        return await ReadTelemetryForSensorsAsync(enabledSensors, cancellationToken);
    }

    /// <summary>
    /// Reads specified sensors (supports different sampling intervals)
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = sensorResourceIds.ToHashSet();
        var sensors = _configuration.Sensors
            .Where(s => s.Config.Enabled && requestedIds.Contains(s.ResourceId))
            .ToList();

        return await ReadTelemetryForSensorsAsync(sensors, cancellationToken);
    }

    private async Task<List<TelemetryMeasure>> ReadTelemetryForSensorsAsync(
        List<Sensor> sensors,
        CancellationToken cancellationToken)
    {
        if (!_communication.IsConnected || sensors.Count == 0)
            return [];

        // 1. Get MetricTypes to collect from sensor.Parameters
        var metricTypes = sensors
            .Select(s => GetParameterValue(s, "MetricType"))
            .Where(t => t != null)
            .Distinct()
            .ToHashSet();

        // 2. Collect raw data via Communication
        var request = new SystemMetricsRequest(metricTypes!);
        var rawData = await _communication.RequestAsync(request, cancellationToken);

        // 3. Convert to TelemetryMeasure
        return ConvertToTelemetryMeasures(sensors, rawData);
    }

    private List<TelemetryMeasure> ConvertToTelemetryMeasures(
        List<Sensor> sensors,
        SystemMetricsRawData rawData)
    {
        var measures = new List<TelemetryMeasure>();

        foreach (var sensor in sensors)
        {
            // Get protocol-specific parameters from Parameters
            var metricType = GetParameterValue(sensor, "MetricType");
            var metricName = GetParameterValue(sensor, "MetricName");

            if (metricType == null || metricName == null)
            {
                _logger.LogWarning("Sensor {Name} missing parameters", sensor.Name);
                continue;
            }

            var value = GetMetricValue(rawData, metricType, metricName, sensor);
            if (value != null)
            {
                measures.Add(new TelemetryMeasure
                {
                    ResourceId = sensor.ResourceId,  // Must use ResourceId
                    Value = value
                });
            }
        }

        return measures;
    }

    /// <summary>
    /// Gets parameter value from sensor.Parameters
    /// </summary>
    private static string? GetParameterValue(Sensor sensor, string key)
    {
        if (sensor.Parameters == null) return null;
        return sensor.Parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    // ... other GetMetricValue implementation omitted
}
```

### 解析 Parameters 的注意事項

`sensor.Parameters` 是 `Dictionary<string, object>`，值可能是:
- `string` - 直接使用
- `JsonElement` - 需要轉換

建議使用輔助方法處理：

```csharp
private static StockSensorParameters DeserializeParameters(Sensor sensor)
{
    string stockCode = string.Empty;
    List<string> metrics = new();

    if (sensor.Parameters?.TryGetValue("StockCode", out var stockCodeObj) == true)
    {
        if (stockCodeObj is string s)
            stockCode = s;
        else if (stockCodeObj is JsonElement json && json.ValueKind == JsonValueKind.String)
            stockCode = json.GetString() ?? "";
    }

    if (sensor.Parameters?.TryGetValue("Metrics", out var metricsObj) == true)
    {
        // Supports comma-separated string format: "Current,Volume,Open"
        if (metricsObj is string s)
            metrics = s.Split(",").Select(x => x.Trim()).ToList();
        else if (metricsObj is JsonElement json && json.ValueKind == JsonValueKind.String)
            metrics = json.GetString()?.Split(",").Select(x => x.Trim()).ToList() ?? [];
    }

    return new StockSensorParameters(stockCode, metrics);
}
```

---

## 步驟 4: 實作 Device Layer

Device 層負責組裝 Parser 和 Communication，並處理裝置生命週期。

### 中繼 Device (建立 Parser)

```csharp
public class SystemMonitorDevice : RequestResponseDeviceBase
{
    public SystemMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
        : base(context, configuration, CreateParser(context, configuration, communication))
    {
        _logger.LogDebug("SystemMonitorDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);
    }

    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
    {
        return new SystemMetricsParser(
            configuration,
            communication,
            context.LoggerFactory.CreateLogger<SystemMetricsParser>());
    }
}
```

### 具體 Device (建立 Communication)

```csharp
public class LocalSystemMonitorDevice : SystemMonitorDevice
{
    public LocalSystemMonitorDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found"))
    {
    }

    public LocalSystemMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateLocalCommunication(context, configuration))
    {
        // Subscribe to DataReceived event for data processing
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private static LocalSystemCommunication CreateLocalCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();
        var logger = context.LoggerFactory.CreateLogger<CommunicationBase>();
        return new LocalSystemCommunication(connectionSettings, logger);
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("Data received, Count={Count}", e.Data.Count);

        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value}", sensor.Name, measure.Value);
            }
        }
    }
}
```

---

## 步驟 5: 建立應用程式進入點

使用 `WedaApplication.CreateBuilder()` 建立應用程式：

```csharp
using SystemMonitorExample;
using Weda.SubNode.Host;

Console.WriteLine("===========================================");
Console.WriteLine("    System Monitor");
Console.WriteLine("===========================================");

var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()           // Enable logging
    .AddTelemetry()         // Enable telemetry upload
    .AddHealthReporting()   // Enable health reporting
    .UseMockCloud();        // Use mock cloud (for dev/test)

// Register device, "SystemMonitorDeviceConfig" maps to key in appsettings.json
builder.AddDevice<LocalSystemMonitorDevice>("SystemMonitorDeviceConfig");

var app = builder.Build();

Console.WriteLine("Starting system monitor...");
Console.WriteLine("Press Ctrl+C to stop.");

await app.RunAsync();
```

### Builder 方法說明

| 方法 | 說明 |
|------|------|
| `.AddLogging()` | 啟用 Serilog 日誌 |
| `.AddTelemetry()` | 啟用遙測資料上傳到雲端 |
| `.AddHealthReporting()` | 啟用裝置健康狀態回報 |
| `.AddCommands()` | 啟用雲端命令接收 (若需要) |
| `.AddConfigUpdates()` | 啟用配置更新接收 (若需要) |
| `.UseMockCloud()` | 使用 Mock 雲端服務 (開發/測試用) |
| `.AddDevice<T>(configKey)` | 註冊裝置，configKey 對應 appsettings.json |

---

## 感測器採樣間隔

SDK 會自動根據 `Config.Interval` 將感測器分組，不同間隔的感測器會在各自的週期被輪詢：

```json
{
  "Sensors": [
    { "Name": "cpu.usage", "Config": { "Interval": 5000 } },
    { "Name": "memory.total", "Config": { "Interval": 10000 } },
    { "Name": "disk.usage", "Config": { "Interval": 30000 } }
  ]
}
```

Parser 的 `ReadTelemetryAsync(IEnumerable<string> sensorResourceIds)` 方法會被呼叫，傳入當次需要讀取的感測器 ResourceId 列表。

---

## 最佳實踐

### 1. 預處理感測器配置

在 Parser 建構時預處理配置，避免每次讀取時重複解析：

```csharp
public class TwseStockParser : IRequestResponseProtocolParser
{
    private readonly Dictionary<string, List<SensorMetricsConfig>> _sensorConfigByStockCode;

    public TwseStockParser(DeviceConfiguration configuration, ...)
    {
        // Preprocess at construction time, group by StockCode
        _sensorConfigByStockCode = PreprocessSensorConfiguration(configuration.Sensors);
    }
}
```

### 2. 在 TelemetryMeasure 中使用 ResourceId

SDK 使用 `ResourceId` 識別感測器，而非 `Name`：

```csharp
measures.Add(new TelemetryMeasure
{
    ResourceId = sensor.ResourceId,  // Correct
    // Name = sensor.Name,           // Do not use
    Value = value
});
```

### 3. 處理唯讀協定

對於唯讀協定 (如 REST API)，命令和寫入操作應回傳錯誤：

```csharp
public Task<ErrorOr<object>> ExecuteCommandAsync(DeviceCommand command, ...)
{
    return Task.FromResult<ErrorOr<object>>(
        Error.Failure("NotSupported", "This protocol is read-only"));
}

public Task<bool> WriteSensorDataAsync(IEnumerable<TelemetryMeasure> measures, ...)
{
    return Task.FromResult(false);
}
```

### 4. 使用 Metadata 傳遞額外資訊

```csharp
measures.Add(new TelemetryMeasure
{
    ResourceId = sensor.ResourceId,
    Value = quote.CurrentPrice,
    Timestamp = timestamp,
    Metadata = new Dictionary<string, object>
    {
        ["StockCode"] = stockCode,
        ["StockName"] = quote.Name ?? "",
        ["MetricName"] = "Current"
    }
});
```

---

## 完整範例參考

- **system-monitor**: [examples/system-monitor/](../../examples/system-monitor/)
- **stock-monitor**: [examples/stock-monitor/](../../examples/stock-monitor/)

---

## 下一步

- [DSP 濾波器配置](../tutorials/01-transform-dsp-config/README_zh.md)
- [程式化 DSP 配置](../tutorials/02-transform-dsp-programmatic/README_zh.md)

---

**版本**: 1.0.0
**最後更新**: 2025-12-10
**維護者**: Rain Hu

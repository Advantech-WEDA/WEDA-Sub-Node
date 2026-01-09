---
marp: false
theme: default
paginate: true
backgroundColor: #fff
style: |
  section {
    font-family: 'Noto Sans TC', 'Microsoft JhengHei', sans-serif;
  }
  h1 {
    color: #1e3a5f;
  }
  h2 {
    color: #2563eb;
  }
  code {
    background: #f1f5f9;
  }
  .columns {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
  }
---

# SubNode SDK

**Edge Device 開發框架**

讓 IoT 裝置開發變得簡單

---

# Agenda

1. **SubNode SDK 介紹** - 什麼是 SubNode？解決什麼問題？ 1. multi-device and open design for protocols   2. pipeline: including tranformation and dsp filter
2. **範例與實作教學** - 快速上手指南 1. start with existing sample(demo -> tcpmodbus), 2. start with template(just intro), 3. how to integration sensor configuration.
3. **未來展望** - Roadmap 與規劃

---

<!-- _class: lead -->

# Part 1
## SubNode SDK 介紹

---

# 什麼是 SubNode SDK？

**一套專為 Edge Device 設計的 .NET SDK**

- 簡化 IoT 裝置與雲端的連接
- 統一管理多個裝置的生命週期
- 內建 Modbus、REST API 等協定支援
- 自動處理 Telemetry 上傳、Config 下發、Command 執行

---

# 核心概念

```
┌─────────────────────────────────────────────────────┐
│                     SubNode                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │  Device A   │  │  Device B   │  │  Device C   │  │
│  │  (Modbus)   │  │  (REST API) │  │  (Custom)   │  │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────┘
                          │
                          ▼
                   ┌─────────────┐
                   │  Weda.Node  │
                   └─────────────┘
                          │
                          ▼
                   ┌─────────────┐
                   │  Weda.Core  │
                   │   (Cloud)   │
                   └─────────────┘
```

## Data Flow
```
┌─────────────┐    ┌─────────────┐   ┌───────────────────┐   ┌────────────────┐   ┌─────────────┐
│   Raw Data  │ -> │   Protocol  │-> │   Transformation  │-> │   DspFilter    │-> │    Cloud    │
└─────────────┘    └─────────────┘   └───────────────────┘   └────────────────┘   └─────────────┘
```

**SubNode = 聚合根 (Aggregate Root)**
- 統一管理所有 Device
- 一次性上傳所有 Device Configuration

---

# 解決的問題

| 傳統做法 | SubNode SDK |
|---------|-------------|
| 手動管理每個裝置連線 | 自動連線管理 + 重試機制 |
| 各裝置分別上傳 Config | 聚合後一次上傳 |
| 手動處理 Telemetry 格式 | 自動序列化 + DTDL 支援 |
| 複雜的生命週期管理 | `await using` 自動處理 |

---

# 兩種使用模式

<div class="columns">

<div>

### SubNode 模式
```csharp
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyDevice(...));

await subNode.InitializeAsync();
await subNode.StartAsync();
```

**適用**: 開發、調試、完整控制

</div>

<div>

### WedaBuilder 模式
```csharp
var builder = WedaApplication.CreateBuilder()
    .AddTelemetry()
    .AddHealthReporting();

builder.AddDevice<MyDevice>("config");
await builder.Build().RunAsync();
```

**適用**: Production、快速部署

</div>

</div>

---

# 架構設計

```
┌──────────────────────────────────────────────────────────┐
│                    Application Layer                     │
│  ┌────────────┐  ┌────────────┐  ┌────────────────────┐  │
│  │  SubNode   │  │ WedaBuilder│  │ DeviceHostedService│  │
│  └────────────┘  └────────────┘  └────────────────────┘  │
├──────────────────────────────────────────────────────────┤
│                      Core Layer                          │
│  ┌────────────┐  ┌────────────┐  ┌────────────────────┐  │
│  │SubNodeMgr  │  │ DeviceBase │  │ TcpModbusDevice    │  │
│  └────────────┘  └────────────┘  └────────────────────┘  │
├──────────────────────────────────────────────────────────┤
│                   Cloud Service Layer                    │
│  ┌────────────┐  ┌────────────┐  ┌────────────────────┐  │
│  │ NATS Client│  │  Mock Cloud│  │ Telemetry Upload   │  │
│  └────────────┘  └────────────┘  └────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

---

<!-- _class: lead -->

# Part 2
## 範例與實作教學

---

# Quick Start - 安裝模板

```bash
# 安裝 SubNode 模板
dotnet new install Weda.SubNode.Templates

# 建立新專案
mkdir devices && cd devices
dotnet new subnode -n MyFirstSubnode

# 執行
cd MyFirstSubnode
dotnet run
```

---

# Program.cs - 最簡範例

```csharp
try
{
    // 1. 建立 Context
    using var context = new WedaApplicationContext(options =>
        options.CloudService = WedaFactory.Cloud.Mock);

    // 2. 建立 SubNode 並加入裝置
    await using var subNode = new SubNode(context);
    subNode.AddDevice(new MyFirstDevice(context, config));

    // 3. 初始化並啟動
    await subNode.InitializeAsync();
    await subNode.StartAsync();

    // 4. 等待停止 (Ctrl+C 自動優雅關閉)
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException) { }
```

---

# MyFirstDevice.cs - 自訂裝置

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(IWedaApplicationContext context,
                         DeviceConfiguration config)
        : base(context, config)
    {
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("  {Name}: {Value}",
                measure.Name, measure.Value);
        }
    }
}
```

---

# 生命週期管理

```
┌─────────────────────────────────────────────────────────┐
│                  SubNode Lifecycle                      │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  new SubNode()          建立容器                         │
│       │                                                 │
│       ▼                                                 │
│  AddDevice()            註冊裝置                         │
│       │                                                 │
│       ▼                                                 │
│  InitializeAsync()      連接雲端 → 註冊 SubNode          │
│       │                 → 初始化各裝置 → 聚合上傳 Config   │
│       ▼                                                 │
│  StartAsync()           啟動所有裝置 (開始讀取資料)         │  
│       │                                                 │
│       ▼                                                 │
│  [Running...]           自動上傳 Telemetry               │
│       │                                                 │
│       ▼                                                 │
│  DisposeAsync()         自動停止並釋放資源                 │
│  (await using)                                          │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

# 實際案例: Power Aggregation

**情境**: 聚合電流與電壓感測器，計算功率

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddTelemetry()
    .AddHealthReporting()
    .UseMockCloud();

// 註冊多個裝置
builder.AddDevice<CurrentSensorDevice>("CurrentSensor");
builder.AddDevice<VoltageSensorDevice>("VoltageSensor");
builder.AddDevice<PowerAggregatorDevice>("PowerAggregator");

await builder.Build().RunAsync();
```

**特點**: 多裝置協作、資料聚合

---

# 連接真實雲端

```csharp
// 開發環境 - Mock
using var context = new WedaApplicationContext(options =>
    options.CloudService = WedaFactory.Cloud.Mock);

// 正式環境 - 移除 Mock，設定 NATS
using var context = new WedaApplicationContext(options =>
{
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://your-server:4222",
        CredFile = "/path/to/nats.creds"
    };
});
```

或透過 `appsettings.json`:
```json
{
  "WedaNode": {
    "Url": "nats://your-server:4222",
    "CredFile": "/path/to/nats.creds"
  }
}
```

---

# 支援的協定

| 協定 | Base Class | 使用情境 |
|-----|-----------|---------|
| **Modbus TCP** | `TcpModbusDevice` | 工業感測器、PLC |
| **REST API** | `RestApiDevice` | 股票 API、天氣 API |
| **iSensing** | `ISensingDevice` | Advantech iSensing |
| **Custom** | `DeviceBase` | 任意自訂協定 |

---

<!-- _class: lead -->

# Part 3
## 未來展望

---

# Roadmap

### 近期 (Q1 2025)
- [ ] OPC UA 協定支援
- [ ] MQTT 協定支援
- [ ] 更完善的 Retry Policy 配置

### 中期 (Q2-Q3 2025)
- [ ] Device Twin 雙向同步
- [ ] Offline 模式 (本地快取)
- [ ] Dashboard UI

### 長期
- [ ] Multi-tenant 支援
- [ ] Edge Computing 框架整合

---

# 技術演進方向

```
                    現在                        未來
              ┌─────────────┐            ┌─────────────┐
              │  SubNode    │            │  SubNode    │
              │    SDK      │     →      │  Platform   │
              └─────────────┘            └─────────────┘
                    │                          │
          ┌─────────┴─────────┐      ┌─────────┴─────────┐
          │                   │      │                   │
     ┌────┴────┐        ┌─────┴───┐  │    ┌─────────┐    │
     │ Modbus  │        │  REST   │  │    │ OPC UA  │    │
     └─────────┘        └─────────┘  │    └─────────┘    │
                                     │    ┌────┴────┐    │
                                     │    │  MQTT   │    │
                                     │    └─────────┘    │
                                     │    ┌────┴────┐    │
                                     │    │ BACnet  │    │
                                     │    └─────────┘    │
                                     └───────────────────┘
```

---

# 資源與連結

### 文件
- Wiki: `docs/wiki/zh/01_quick_start/`
- Examples: `examples/`

### 模板
```bash
dotnet new subnode -n MyProject      # Console 風格
dotnet new wedabuilder -n MyProject  # Production 風格
```

### 聯絡
- GitHub Issues
- Teams Channel

---

<!-- _class: lead -->

# Q & A

**感謝聆聽**


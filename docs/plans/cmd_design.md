# Command Handler Architecture Design

## Overview

本文件描述 SubNode SDK 的 Command 處理架構設計。

**設計原則**：
- **SubNode-Centric**：所有 Command 都從 SubNode 出發
- **Developer 決定**：Handler 中自己決定要不要操作 Device
- **自動掃描**：Handler 自動從 Assembly 掃描，不需手動註冊
- **兩種模式一致**：WedaBuilder 與 SubNode 模式的 Handler 寫法完全相同

---

## 目錄

1. [架構總覽](#架構總覽)
2. [Command 處理的三種模式](#command-處理的三種模式)
3. [SDK 使用模式](#sdk-使用模式)
4. [Core Abstractions](#core-abstractions)
5. [Implementation Plan](#implementation-plan)
6. [SDK User 快速參考](#sdk-user-快速參考)

---

## 架構總覽

### 整體流程

```
Cloud Command (NATS)
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  SubNode                                                        │
│    → Auto Scan Assembly to find all ICommandHandler<T>          │
│    → CommandDispatcher perform Pipeline → Handler               │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  Pipeline Behaviors (MediatR-like)                              │
│    ├─ LoggingBehavior (entry/exit logging)                      │
│    ├─ ValidatorBehavior (command-specific validation)           │
│    └─ next() → actual handler                                   │
└─────────────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  Custom Handler                                                 │
│                                                                 │
│  public class SetDoCommandHandler : ICommandHandler<SetDoCmd>   │
│  {                                                              │
│      public async Task<CommandResult> HandleAsync(              │
│          SetDoCmd command,                                      │
│          IWedaApplicationContext context,  // 直接用現有 Context │
│          CancellationToken ct)                                  │
│      {                                                          │
│          var device = context.GetDevice<MyModbusDevice>("plc"); │
│          await device.SetDO("do0", true);                       │
│          return CommandResult.Success();                        │
│      }                                                          │
│  }                                                              │
└─────────────────────────────────────────────────────────────────┘
    │ (if you call device methods)
    ▼
┌─────────────────────────────────────────────────────────────────┐
│  CustomDevice Implementation                                    │
│                                                                 │
│  public class MyModbusDevice : BaseDevice                       │
│  {                                                              │
│      public async Task<ErrorOr<T>> SetDO(string name, bool val) │
│      {                                                          │
│          await _modbusClient.WriteSingleCoilAsync(...);         │
│      }                                                          │
│  }                                                              │
└─────────────────────────────────────────────────────────────────┘
```

### Command Envelope (Payload 格式)

```json
{
  "cmd": "deviceCmd",
  "seqId": 100,
  "reqSeqId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": 1737004691020,
  "data": {
    "deviceCmd": "report",
    "reportType": "historicalTelemetry",
    "timeRange": {
      "startTime": 1737000000000,
      "endTime": 1737004000000
    },
    "sensorFilter": {
      "include": ["sensor1", "sensor2"],
      "exclude": []
    },
    "respTopic": "eco1p.advantech.xxx.subnode.cmd.rsp",
    "dataTopic": "eco1p.advantech.xxx.subnode.telemetry.batch",
    "maxBatchesPerMessage": 10,
    "transmissionRateLimit": 100,
    "timeout": 300
  }
}
```

---

## Command 處理的三種模式

### 模式對照圖

```
                             Cloud Command
                                  │
                                  ▼
                        ┌───────────────────┐
                        │ CommandDispatcher │
                        │                   │
                        │  Registry Lookup  │
                        └─────────┬─────────┘
                                  │
         ┌────────────────────────┼────────────────────────┐
         │                        │                        │
         ▼                        ▼                        ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    Mode A       │     │    Mode B       │     │    Mode C       │
│  SubNode Only   │     │  Device Only    │     │    Hybrid       │
├─────────────────┤     ├─────────────────┤     ├─────────────────┤
│                 │     │                 │     │                 │ 
│  ┌───────────┐  │     │  ┌───────────┐  │     │  ┌───────────┐  │ 
│  │  Handler  │  │     │  │  Handler  │  │     │  │  Handler  │  │ 
│  │           │  │     │  │           │  │     │  │           │  │ 
│  │ ┌───────┐ │  │     │  │           │  │     │  │ ┌───────┐ │  │ 
│  │ │Service│ │  │     │  │           │  │     │  │ │Service│ │  │ 
│  │ └───────┘ │  │     │  │           │  │     │  │ └───────┘ │  │ 
│  └───────────┘  │     │  │           │  │     │  │           │  │ 
│                 │     │  │ ┌───────┐ │  │     │  │ ┌───────┐ │  │ 
│                 │     │  │ │Router │ │  │     │  │ │Router │ │  │ 
│                 │     │  │ └───┬───┘ │  │     │  │ └───┬───┘ │  │ 
│                 │     │  └─────│─────┘  │     │  └─────│─────┘  │ 
│                 │     │        ▼        │     │        ▼        │ 
│                 │     │  ┌───────────┐  │     │  ┌───────────┐  │ 
│                 │     │  │  Device   │  │     │  │  Device   │  │ 
│                 │     │  │ ┌───────┐ │  │     │  │ ┌───────┐ │  │ 
│                 │     │  │ | Parse │ │  │     │  │ | Parse │ │  │ 
│                 │     │  │ └───┬───┘ │  │     │  │ └───┬───┘ │  │ 
│                 │     │  └─────│─────┘  │     │  └─────│─────┘  │ 
│                 │     │        ▼        │     │        ▼        │ 
│                 │     │  ┌───────────┐  │     │  ┌───────────┐  │ 
│                 │     │  │ Physical  │  │     │  │ Physical  │  │ 
│                 │     │  │  Device   │  │     │  │  Device   │  │ 
│                 │     │  └───────────┘  │     │  └───────────┘  │ 
│                 │     │                 │     │                 │ 
└─────────────────┘     └─────────────────┘     └─────────────────┘ 
        │                        │                       │
        ▼                        ▼                       ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│ Examples:       │     │ Examples:       │     │ Examples:       │
│ - report        │     │ - setDo         │     │ - calibrateAll  │
│ - getLogs       │     │ - setAo         │     │ - firmwareUpdate│
│ - getStatus     │     │ - readRegister  │     │ - batchConfig   │
│ - clearCache    │     │ - writeConfig   │     │ - syncTime      │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

---

### Mode A: SubNode Only (控制 SubNode 本身)

**場景**：查詢歷史資料、讀取設定、清除快取

```
    Cloud
      │
      │  { "deviceCmd": "report", "timeRange": {...} }
      ▼
┌──────────────────┐
│  SubNode         │
│  ┌────────────┐  │
│  │ Recording  │  │     ← SubNode 直接處理
│  │ Service    │  │       不需要轉發給 Device
│  └────────────┘  │
└──────────────────┘
      │
      ▼
    Response: historical data
```

**SDK User 實作**:

```csharp
// 1. 定義 Command
public record GetLogsCommand : ICommand
{
    public string DeviceCmd => "getLogs";
    public int MaxLines { get; init; } = 100;
}

// 2. 實作 Handler (只操作 SubNode 資源)
public class GetLogsCommandHandler : ICommandHandler<GetLogsCommand>
{
    public async Task<CommandResult> HandleAsync(
        GetLogsCommand command,
        IWedaApplicationContext context,
        CancellationToken ct)
    {
        // 透過 context 取得所需服務
        var logger = context.GetLogger<GetLogsCommandHandler>();
        logger.LogInformation("Getting logs, max lines: {MaxLines}", command.MaxLines);

        // 實際邏輯...
        var logs = new[] { "log1", "log2" };
        return CommandResult.Success(logs);
    }
}

// 3. 註冊 (WedaBuilder 模式)
builder.ConfigureCommands(cmd => {
    cmd.Register<GetLogsCommand, GetLogsCommandHandler>();
});
```

---

### Mode B: Device Only (控制 Physical Device)

**場景**：控制數位輸出、讀取暫存器

```
    Cloud
      │
      │  { "deviceCmd": "deviceControl", "deviceName": "plc-1",
      │    "command": "SetDO", "parameters": { "do": "do0", "state": true } }
      ▼
┌──────────────────┐
│  SubNode         │
│  ┌────────────┐  │
│  │ Dispatcher │──┼──► DeviceControlCommandHandler 轉發給 Device
│  └────────────┘  │
└──────────────────┘
      │
      ▼
┌──────────────────┐
│  Device (plc-1)  │
│  ┌────────────┐  │
│  │ Execute    │  │     ← Device 負責執行
│  │ CommandAsync│  │       (Modbus FC05 Write Coil)
│  └────────────┘  │
└──────────────────┘
      │
      ▼
┌──────────────────┐
│  Physical PLC    │     ← 實際硬體
│  [DO0] = ON      │
└──────────────────┘
```

**內建 Handler** (SDK 提供，不需自己實作):

```csharp
// DeviceControlCommandHandler 是 SDK 內建的
// 它會根據 deviceName 找到對應的 Device，並呼叫 ExecuteCommandAsync
public class DeviceControlCommandHandler : ICommandHandler<DeviceControlCommand>
{
    public async Task<CommandResult> HandleAsync(
        DeviceControlCommand command,
        IWedaApplicationContext context,
        CancellationToken ct)
    {
        var device = context.GetDevice(command.DeviceName);
        var result = await device.ExecuteCommandAsync(command.ToDeviceCommand(), ct);
        return result.IsError
            ? CommandResult.Failure(result.FirstError)
            : CommandResult.Success(result.Value);
    }
}
```

**Cloud Payload**:

```json
{
  "deviceCmd": "deviceControl",
  "deviceName": "plc-1",
  "command": "SetDO",
  "parameters": { "do": "do0", "state": true }
}
```

---

### Mode C: Hybrid (SubNode 處理 + 轉發給 Device)

**場景**：校正所有感測器、批次更新設定、韌體更新

```
    Cloud
      │
      │  { "deviceCmd": "calibrateAll", "referenceValue": 25.0 }
      ▼
┌────────────────────────────────────────────────────────────────┐
│  SubNode                                                       │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │ CalibrateAllCommandHandler                               │  │
│  │                                                          │  │
│  │  1. Record calibration start (SubNode Operation)         │  │
│  │  2. Traverse all devices, forward calibration command    │  │
│  │  3. Collect results                                      │  │
│  │  4. Record calibration results to DB (SubNode Operation) │  │
│  │  5. Return aggregated result                             │  │
│  │                                                          │  │
│  └──────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────┘
           │                    │                    │
           ▼                    ▼                    ▼
     ┌───────────┐        ┌───────────┐        ┌───────────┐
     │ Device 1  │        │ Device 2  │        │ Device 3  │
     │ Calibrate │        │ Calibrate │        │ Calibrate │
     └───────────┘        └───────────┘        └───────────┘
```

**SDK User 實作**:

```csharp
// 1. 定義 Command
public record CalibrateAllCommand : ICommand
{
    public string DeviceCmd => "calibrateAll";
    public double ReferenceValue { get; init; }
}

// 2. 實作 Hybrid Handler
public class CalibrateAllCommandHandler : ICommandHandler<CalibrateAllCommand>
{
    public async Task<CommandResult> HandleAsync(
        CalibrateAllCommand command,
        IWedaApplicationContext context,
        CancellationToken ct)
    {
        var logger = context.GetLogger<CalibrateAllCommandHandler>();

        // ===== 轉發給所有 Devices =====
        var results = new List<DeviceCalibrationResult>();
        var devices = context.DeviceRegistry.GetAllDevices();

        logger.LogInformation("Calibrating {Count} devices with reference {Value}",
            devices.Count, command.ReferenceValue);

        foreach (var device in devices)
        {
            var deviceCmd = new DeviceCommand
            {
                DeviceCmd = "Calibrate",
                Parameters = new Dictionary<string, object>
                {
                    ["referenceValue"] = command.ReferenceValue
                }
            };

            var result = await device.ExecuteCommandAsync(deviceCmd, ct);

            results.Add(new DeviceCalibrationResult
            {
                DeviceName = device.Configuration.DeviceName,
                Success = !result.IsError,
                Error = result.IsError ? result.FirstError.Description : null
            });
        }

        return CommandResult.Success(new
        {
            totalDevices = results.Count,
            successCount = results.Count(r => r.Success),
            results
        });
    }
}
```

---

## SDK 使用模式

### 模式 1: WedaBuilder 模式 (Declarative)

適合 Production 環境，透過 DI 注入。

```csharp
var builder = WedaApplication.CreateBuilder(args);
builder.LoadConfiguration("devicecfg.json");

builder.ConfigureCommands(commands =>
{
    // Type-based registration (DI friendly)
    commands.Register<CalibrateAllCommand, CalibrateAllCommandHandler>();
    commands.RegisterValidator<CalibrateAllCommand, CalibrateAllValidator>();
});

var app = builder.Build();
await app.RunAsync();
```

### 模式 2: SubNode 模式 (Programmatic)

適合快速開發、測試、完全程式化控制。

```csharp
// 1. 建立 registry (不需要 DI)
var registry = new CommandRegistry();

// 2. 用 delegate 註冊 (簡單場景)
// context 是 IWedaApplicationContext，提供所有必要服務
registry.Register("getLogs", async (command, context, ct) =>
{
    var logger = context.GetLogger<Program>();
    logger.LogInformation("Getting logs...");
    return CommandResult.Success(new[] { "log1", "log2" });
});

// 3. 用 delegate 存取 devices
registry.Register("setDo", async (command, context, ct) =>
{
    var device = context.GetDevice<MyModbusDevice>("plc-1");
    await device.SetDO("do0", true);
    return CommandResult.Success();
});

// 4. 用 instance 註冊 (複雜場景)
registry.Register("report", new ReportCommandHandler());

// 5. 建立 SubNodeManager 並啟動
var dispatcher = new CommandDispatcher(registry, context);
var manager = context.SubNodeManager;
await manager.StartAsync();
```

### 兩種模式對照

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         CommandRegistry                                     │
│                                                                             │
│   支援兩種註冊方式:                                                          │
│   1. Type-based: Register<TCommand, THandler>()     (DI 模式)              │
│   2. Delegate-based: Register("cmd", handler)       (Programmatic 模式)    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────┬───────────────────────────────────────┐
│  WedaBuilder 模式                   │  SubNode 模式                         │
├─────────────────────────────────────┼───────────────────────────────────────┤
│                                     │                                       │
│  builder.ConfigureCommands(cmd => { │  var registry = new CommandRegistry();│
│    cmd.Register<T, THandler>();     │  registry.Register("cmd", handler);   │
│  });                                │                                       │
│                                     │  var manager = new SubNodeManager(    │
│  // DI 自動注入                     │      options, registry);              │
│                                     │                                       │
└─────────────────────────────────────┴───────────────────────────────────────┘
```

| 模式 | 註冊方式 | 適用場景 |
|------|----------|----------|
| **WedaBuilder** | `Register<T, THandler>()` | Production、需要 DI、Pipeline |
| **SubNode** | `Register("cmd", handler)` | 快速開發、測試、Programmatic 控制 |

---

## Core Abstractions

### File Structure

```
src/Weda.SubNode.Abstractions/Commands/
├── ICommand.cs
├── ICommandHandler.cs
├── CommandEnvelope.cs
├── CommandResult.cs
├── Pipeline/
│   └── IPipelineBehavior.cs
├── Validation/
│   ├── ICommandValidator.cs
│   └── ValidationResult.cs
└── Definitions/
    ├── ReportCommand.cs           # SubNode-level (Mode A)
    └── DeviceControlCommand.cs    # Device-level (Mode B)

src/Weda.SubNode.Core/Commands/
├── CommandRegistry.cs
├── CommandDispatcher.cs
├── CommandParser.cs
├── Behaviors/
│   ├── LoggingBehavior.cs
│   └── ValidatorBehavior.cs
├── Handlers/
│   ├── ReportCommandHandler.cs
│   └── DeviceControlCommandHandler.cs
└── Validators/
    └── ReportCommandValidator.cs
```

> **Note**: Handler 使用 `IWedaApplicationContext` 作為 context 參數，不需要額外的 CommandContext 類別。
> `IWedaApplicationContext` 已提供：DeviceRegistry、RecordingService、CloudService、Logger 等所有必要服務。

### Key Interfaces

```csharp
// ICommand - 所有 command 的 marker interface
public interface ICommand
{
    string DeviceCmd { get; }
}

// ICommandHandler - command handler 介面
// 使用 IWedaApplicationContext 作為 context，提供 DeviceRegistry、RecordingService 等服務
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        CancellationToken ct);
}

// IPipelineBehavior - pipeline middleware
public interface IPipelineBehavior<TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        CommandHandlerDelegate<TCommand> next,
        CancellationToken ct);
}

// ICommandValidator - command 驗證器
public interface ICommandValidator<TCommand> where TCommand : ICommand
{
    ValidationResult Validate(TCommand command, IWedaApplicationContext context);
}
```

### CommandRegistry API

```csharp
public class CommandRegistry
{
    // ===== Type-based registration (for DI) =====
    public void Register<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : ICommandHandler<TCommand>;

    // ===== Delegate-based registration (for programmatic) =====
    public void Register(string deviceCmd, CommandHandlerDelegate handler);
    public void Register(string deviceCmd, ICommandHandler handler);

    // ===== Lookup =====
    public CommandRegistration? GetRegistration(string deviceCmd);
}

public delegate Task<CommandResult> CommandHandlerDelegate(
    ICommand command,
    IWedaApplicationContext context,
    CancellationToken ct);
```

---

## Implementation Plan

### Phase 1: Core Abstractions

- [ ] Step 1.1: Create ICommand marker interface
- [ ] Step 1.2: Create ICommandHandler interface (uses IWedaApplicationContext)
- [ ] Step 1.3: Create CommandEnvelope (NATS payload wrapper)
- [ ] Step 1.4: Create CommandResult

### Phase 2: Pipeline Infrastructure

- [ ] Step 2.1: Create IPipelineBehavior interface
- [ ] Step 2.2: Create LoggingBehavior
- [ ] Step 2.3: Create ICommandValidator interface
- [ ] Step 2.4: Create ValidatorBehavior

### Phase 3: Registry & Dispatcher

- [ ] Step 3.1: Create CommandRegistry (supports delegate & type registration)
- [ ] Step 3.2: Create CommandDispatcher (executes pipeline → handler)
- [ ] Step 3.3: Create CommandParser (JSON → ICommand)

### Phase 4: Built-in Commands

- [ ] Step 4.1: Create ReportCommand & ReportCommandHandler
- [ ] Step 4.2: Create DeviceControlCommand & DeviceControlCommandHandler

### Phase 5: Integration

- [ ] Step 5.1: Integrate CommandDispatcher into SubNodeManager
- [ ] Step 5.2: Add ConfigureCommands() to WedaApplicationBuilder
- [ ] Step 5.3: Write unit tests
- [ ] Step 5.4: Integration test with NATS

---

## SDK User 快速參考

### 決策流程圖

```
                    ┌─────────────────────────┐
                    │ 我要實作一個新 Command   │
                    └───────────┬─────────────┘
                                │
                                ▼
                    ┌─────────────────────────┐
                    │ 需要操作 Physical Device │
                    │ 嗎？                     │
                    └───────────┬─────────────┘
                                │
              ┌─────────────────┴─────────────────┐
              │ No                                │ Yes
              ▼                                   ▼
    ┌─────────────────┐               ┌─────────────────────────┐
    │    Mode A       │               │ 需要 SubNode 也做事嗎？ │
    │  SubNode Only   │               │ (記錄、統整、協調)      │
    │                 │               └───────────┬─────────────┘
    │ 實作:           │                           │
    │ ICommandHandler │             ┌─────────────┴─────────────┐
    └─────────────────┘             │ No                        │ Yes
                                    ▼                           ▼
                          ┌─────────────────┐       ┌─────────────────┐
                          │    Mode B       │       │    Mode C       │
                          │  Device Only    │       │    Hybrid       │
                          │                 │       │                 │
                          │ 使用內建        │       │ 實作:           │
                          │ DeviceControl   │       │ ICommandHandler │
                          │ Command         │       │ + 呼叫 devices  │
                          └─────────────────┘       └─────────────────┘
```

### 快速參考表

| 我想要... | 用哪個 Mode | 我需要實作 |
|-----------|-------------|-----------|
| 查詢 SubNode 的歷史資料 | Mode A | `ICommandHandler<T>` |
| 讀取 SubNode 的設定 | Mode A | `ICommandHandler<T>` |
| 控制單一 Device 的 DO | Mode B | 使用內建 `DeviceControlCommand`（不需實作） |
| 讀取單一 Device 的 Register | Mode B | 使用內建 `DeviceControlCommand`（不需實作） |
| 校正所有 Devices 並記錄 | Mode C | `ICommandHandler<T>` + 呼叫 `device.ExecuteCommandAsync` |
| 批次更新多個 Devices 設定 | Mode C | `ICommandHandler<T>` + 遍歷 devices |
| 複雜工作流程 (有先後順序) | Mode C | `ICommandHandler<T>` + orchestration logic |

---

## Suggested Commits

```
feat(commands): add ICommand, ICommandHandler and CommandResult (Phase 1)
feat(commands): add pipeline behavior infrastructure (Phase 2)
feat(commands): add CommandRegistry and CommandDispatcher (Phase 3)
feat(commands): add ReportCommand and DeviceControlCommand (Phase 4)
refactor(commands): integrate CommandDispatcher into SubNodeManager (Phase 5)
```

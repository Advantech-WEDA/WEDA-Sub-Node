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
│          IWedaApplicationContext context,                       │
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
    "deviceCmd": "report.historical",
    "respTopic": "eco1p.advantech.xxx.subnode.cmd.rsp",
    "timeout": 300,
    "parameters": {
      "timeRange": {
        "startTime": 1737000000000,
        "endTime": 1737004000000
      },
      "sensorFilter": {
        "include": ["sensor1", "sensor2"],
        "exclude": []
      },
      "maxBatchesPerMessage": 10,
      "transmissionRateLimit": 100
    }
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
[DeviceCmd("get.logs")]
public class GetLogsCommand : CommandData<GetLogsParameter>;

public class GetLogsParameter
{
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
        logger.LogInformation("Getting logs, max lines: {MaxLines}", command.Parameters.MaxLines);

        // 實際邏輯...
        var logs = new[] { "log1", "log2" };
        return CommandResult.Success(logs);
    }
}
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
│  │ SetDO()    │  │     ← Device 負責執行 (Modbus FC05 Write Coil)
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
public class DeviceControlCommandHandler : ICommandHandler<DeviceControlCommand>
{
    public async Task<CommandResult> HandleAsync(
        DeviceControlCommand command,
        IWedaApplicationContext context,
        CancellationToken ct)
    {
        var device = context.GetDevice(command.Parameters.DeviceName);
        if (device == null) return Error.NotFound("Device.NotFound", $"Device {command.Parameters.DeviceName} not found.")
        var result = await device.SetDO(command.Parameters.Do, command.Parameters.State, ct);
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
  
  "cmd": "deviceCmd",
  "seqId": 100,
  "reqSeqId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": 1737004691020,
  "data": {
    "deviceCmd": "set.do",
    "respTopic": "eco1p.advantech.xxx.subnode.cmd.rsp",
    "timeout": 300,
    "parameters": {
      "do": "do0",
      "state": true
    }
  }
}
```

## Command Flow
```
┌──────────────────────────────────────────────────────────────────────────┐
│ CLOUD                                                                    │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │ CommandMessage (JSON)                                                │ │
│ │ {                                                                    │ │
│ │   "cmd": "deviceCmd",                                                │ │
│ │   "seqId": 100,                                                      │ │
│ │   "reqSeqId": "uuid-xxx",                                            │ │
│ │   "timestamp": 1737004691020,                                        │ │
│ │   "data": {                                                          │ │
│ │     "deviceCmd": "report.historical",                                │ │
│ │     "timeout": 300,                                                  │ │
│ │     "respTopic": "eco1j.weda...cmd.rsp",                             │ │
│ │     "parameters": { "timeRange": {...}, "maxBatchSize": 1000 }       │ │
│ │   }                                                                  │ │
│ │ }                                                                    │ │
│ └───────────────────────────────┬──────────────────────────────────────┘ │
└─────────────────────────────────┼────────────────────────────────────────┘
                                  │ NATS
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ WedaCloudService                                                         │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │ SubscribeAsync<CommandMessage>()                                     │ │
│ │   ↓                                                                  │ │
│ │ ExtractDeviceCommand(envelope)                                       │ │
│ │ - Deserialize to DeviceCommand                                       │ │
│ │ - Copy SeqId, ReqSeqId                                               │ │
│ │ - Store RawData (JsonElement)                                        │ │
│ │   ↓                                                                  │ │
│ │ Fire ExecuteCommandEvent                                             │ │
│ └───────────────────────────────┬──────────────────────────────────────┘ │
└─────────────────────────────────┼────────────────────────────────────────┘
                                  │ Event
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ SubNodeManager                                                           │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │ RouteCommandAsync(ExecuteCommandEvent e)                             │ │
│ │   ↓                                                                  │ │
│ │ CreateCommandMessage(e)                                              │ │
│ │ - Create CommandMessage from ExecuteCommandEvent                     │ │
│ │ - Data = e.Command.RawData (JsonElement)                             │ │
│ │   ↓                                                                  │ │
│ │ _commandDispatcher.DispatchAsync(message)                            │ │
│ └───────────────────────────────┬──────────────────────────────────────┘ │
└─────────────────────────────────┼────────────────────────────────────────┘
                                  │
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ CommandDispatcher                                                        │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │ DispatchAsync(CommandMessage message)                                │ │
│ │   ↓                                                                  │ │
│ │ 1. ExtractCommandName(message.Data)                                  │ │
│ │     → "report.historical"                                            │ │
│ │   ↓                                                                  │ │
│ │ 2. registry.GetRegistration("report.historical")                     │ │
│ │     → CommandRegistration { HandlerType: BatchReportCommandHandler } │ │
│ │   ↓                                                                  │ │
│ │ 3. registry.DeserializeCommand(message.Data, BatchReportCommand)     │ │
│ │     → BatchReportCommand instance                                    │ │
│ │   ↓                                                                  │ │
│ │ 4. PopulateCommandMetadata(command, message.SeqId, message.ReqSeqId) │ │
│ │     → Set command.SeqId, command.ReqSeqId via reflection             │ │
│ │   ↓                                                                  │ │
│ │ 5. Send "Received" response (if AutoAck enabled)                     │ │
│ │   ↓                                                                  │ │
│ │ 6. RunDataAnnotationValidation()                                     │ │
│ │   ↓                                                                  │ │
│ │ 7. CreateBehaviors() → [ValidatorBehavior, LoggingBehavior]          │ │
│ │   ↓                                                                  │ │
│ │ 8. ExecutePipelineAsync()                                            │ │
│ └───────────────────────────────┬──────────────────────────────────────┘ │
└─────────────────────────────────┼────────────────────────────────────────┘
                                  │
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ Pipeline Execution                                                       │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │                                                                      │ │
│ │ ┌──────────────────────────────────────────────────────────────────┐ │ │
│ │ │ ValidatorBehavior<BatchReportCommand, BatchReportResult>         │ │ │
│ │ │ → BatchReportCommandValidator.Validate()                         │ │ │
│ │ └─────────────────────────────┬────────────────────────────────────┘ │ │
│ │                               │ next()                               │ │
│ │                               ▼                                      │ │
│ │ ┌──────────────────────────────────────────────────────────────────┐ │ │
│ │ │ LoggingBehavior<BatchReportCommand, BatchReportResult>           │ │ │   
│ │ │ → Log before/after execution                                     │ │ │
│ │ └─────────────────────────────┬────────────────────────────────────┘ │ │
│ │                               │ next()                               │ │
│ │                               ▼                                      │ │
│ │ ┌──────────────────────────────────────────────────────────────────┐ │ │
│ │ │ BatchReportCommandHandler.HandleAsync()                          │ │ │
│ │ │ - Access command.Parameters.TimeRange                            │ │ │
│ │ │ - Access command.Parameters.MaxBatchSize                         │ │ │
│ │ │ - Access command.SeqId, command.ReqSeqId                         │ │ │
│ │ │ - Query recordings, send batch telemetry                         │ │ │
│ │ │ - Return BatchReportResult                                       │ │ │
│ │ └─────────────────────────────┬────────────────────────────────────┘ │ │
│ │                               │                                      │ │
│ └───────────────────────────────┼──────────────────────────────────────┘ │
└─────────────────────────────────┼────────────────────────────────────────┘
                                  │
                                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ CommandDispatcher (Response)                                             │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │ 9. SendResponseAsync()                                               │ │
│ │ → Success/Failed/Rejected based on result                            │ │
│ └──────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
關鍵類別關係

CommandMessage (Envelope) CommandData<T> (Data Payload)
┌──────────────────────┐           ┌───────────────────────────────┐
│ cmd: string          │           │ deviceCmd: string             │
│ seqId: ulong         │           │ timeout: uint                 │
│ reqSeqId: string?    │           │ respTopic: string?            │
│ timestamp: long      │           │ parameters: T                 │
│ data: JsonElement?   │──────────▶│ seqId: ulong [JsonIgnore]     │
└──────────────────────┘           │ reqSeqId: string? [JsonIgnore]│
                                   └───────────────────────────────┘
                                             ▲
                                             │ extends
                       ┌───────────────────────────────────────────┐
                       │ BatchReportCommand                        │
                       │ : CommandData<BatchReportParameters>      │
                       │                                           │
                       │ + GetEffectiveTimeRange()                 │
                       └───────────────────────────────────────────┘
Handler 註冊流程

Application Startup
    │
    ▼
CommandRegistry.ScanAssembly()
│
├── Find ICommandHandler<TCommand, TResult> implementations
│
├── Get [DeviceCmd("report.historical")] from BatchReportCommand
│
├── Get [Validation], [Logging], [AutoAck] from BatchReportCommandHandler
│
└── Register: "report.historical" → CommandRegistration
    ├─ CommandType: BatchReportCommand
    ├─ HandlerType: BatchReportCommandHandler
    ├─ BehaviorConfigs: [Validation, Logging]
    └─ AutoAckEnabled: false
```
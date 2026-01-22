# Command Pipeline Architecture Design

## Overview

本文件描述 SubNode SDK 的 Command Handler 架構，採用 **Attribute-based** 設計，讓開發者可以透過標記 Attribute 來無痛加入 Logging、Validation 等 Pipeline Behaviors。

## Design Goals

1. **零配置體驗** - 使用 Attribute 標記即自動註冊，無需手動配置
2. **可組合的 Pipeline** - 預設提供 Logging + Validation，可自訂順序或加入新 Behavior
3. **DataAnnotation 驗證** - Command DTO 使用標準 `[Required]`, `[Range]` 等 Annotation
4. **可擴展性** - 使用者可繼承 `DefaultCommandPipeline` 調整 Behavior 順序
5. **強型別命令** - Handler 直接接收 `TCommand`，不處理原始 payload
6. **Framework 管理回應** - Response (Received/Success/Failed) 由 framework 自動處理

## Decision Records

### DR-1: Response 由 Framework 自動處理
- **決定**: Handler 只處理業務邏輯，回傳 `ErrorOr<TResult>`；Framework 根據結果自動發送 CommandResponse
- **原因**: 減少 Handler 重複的 response 處理程式碼，讓 Handler 專注於業務邏輯
- **實作**:
  - Framework 從 Command 提取 `RespTopic` (convention-based)
  - `RespTopic` 為空字串時不發送 response
  - 執行前發送 `Received`，成功發送 `Success`，失敗發送 `Failed/Rejected`

### DR-2: Cloud Payload 格式固定
- **決定**: Cloud payload 結構不能改變，`respTopic` 在 `data` 內部
- **原因**: 與 Cloud team 的協議，向後相容
- **實作**: Command 類別必須包含 `RespTopic` 屬性（透過 `[JsonPropertyName("respTopic")]`）

### DR-3: RespTopic 是 Optional
- **決定**: 若 `RespTopic` 為空字串或 null，代表不需要回應
- **原因**: 某些命令可能是 fire-and-forget

## Architecture

```
                    CommandEnvelope (from NATS)
                            │
                            ▼
                    ┌───────────────┐
                    │  Dispatcher   │
                    └───────┬───────┘
                            │
                            ▼
                    ┌───────────────┐
                    │   Registry    │  ← Auto-scan assemblies
                    └───────┬───────┘
                            │
                            ▼
            ┌───────────────────────────────┐
            │      Pipeline Behaviors       │
            │  ┌─────────────────────────┐  │
            │  │   LoggingBehavior       │  │  ← [Logging]
            │  └───────────┬─────────────┘  │
            │              ▼                │
            │  ┌─────────────────────────┐  │
            │  │   ValidatorBehavior     │  │  ← [Validation]
            │  └───────────┬─────────────┘  │
            │              ▼                │
            │  ┌─────────────────────────┐  │
            │  │   Custom Behaviors...   │  │  ← User-defined
            │  └─────────────────────────┘  │
            └───────────────┬───────────────┘
                            │
                            ▼
                    ┌───────────────┐
                    │    Handler    │
                    └───────────────┘
                            │
                            ▼
                    ErrorOr<TResult>
```

## Core Components

### 1. Attributes

#### `[DeviceCmd]` - 標記命令名稱

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class DeviceCmdAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
```

#### `[Logging]` - 啟用 Logging Behavior

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class LoggingAttribute : Attribute
{
    /// <summary>
    /// Log level for command execution start.
    /// </summary>
    public LogLevel BeforeLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Log level for successful command completion.
    /// </summary>
    public LogLevel AfterLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Log level for command execution errors.
    /// </summary>
    public LogLevel ErrorLevel { get; set; } = LogLevel.Warning;
}
```

#### `[Validation]` - 啟用 Validation Behavior

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ValidationAttribute : Attribute
{
    /// <summary>
    /// Custom validator type. If null, uses DataAnnotationValidator.
    /// </summary>
    public Type? CustomValidatorType { get; set; }
}
```

#### `[Pipeline]` - 指定自訂 Pipeline

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class PipelineAttribute(Type pipelineType) : Attribute
{
    public Type PipelineType { get; } = pipelineType;
}
```

### 2. Command Flow (Framework-Managed Response)

```
NATS Message (NatsCommandMessage envelope)
        │
        ▼
┌───────────────────────────────────────┐
│  WedaCloudService                     │
│  1. Deserialize to NatsCommandMessage │
│  2. Extract DeviceCommand             │
│  3. Store RawData (JsonElement)       │
└───────────────────┬───────────────────┘
                    │
                    ▼
┌───────────────────────────────────────┐
│  CommandDispatcher                    │
│  1. Extract RespTopic from Command    │
│  2. Send "Received" response          │◀─ If RespTopic is not empty
│  3. Dispatch to Handler               │
│  4. On success: send "Success"        │
│  5. On error: send "Failed/Rejected"  │
└───────────────────┬───────────────────┘
                    │
                    ▼
┌───────────────────────────────────────┐
│  Handler (only business logic)        │
│  - Receives strongly-typed TCommand   │
│  - Returns ErrorOr<TResult>           │
│  - NO response handling code          │
└───────────────────────────────────────┘
```

### 3. Interfaces

#### `ICommand` - Marker Interface

```csharp
public interface ICommand { }
```

#### `ICommandHandler<TCommand, TResult>`

Handler 只處理業務邏輯，不負責 response。

```csharp
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand
{
    Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default);
}
```

#### `ICommandValidator<TCommand>`

```csharp
public interface ICommandValidator<in TCommand>
    where TCommand : ICommand
{
    ErrorOr<Success> Validate(TCommand command);
}
```

#### `IPipelineBehavior<TCommand, TResult>`

```csharp
public interface IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default);
}
```

### 3. Default Behaviors

#### `LoggingBehavior`

記錄命令執行的開始、結束、耗時、以及錯誤。

```csharp
public class LoggingBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    private readonly LogLevel _beforeLevel;
    private readonly LogLevel _afterLevel;
    private readonly LogLevel _errorLevel;

    public LoggingBehavior(
        LogLevel beforeLevel = LogLevel.Debug,
        LogLevel afterLevel = LogLevel.Debug,
        LogLevel errorLevel = LogLevel.Warning)
    {
        _beforeLevel = beforeLevel;
        _afterLevel = afterLevel;
        _errorLevel = errorLevel;
    }

    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<LoggingBehavior<TCommand, TResult>>();
        var commandName = typeof(TCommand).Name;

        logger.Log(_beforeLevel, "Executing command {CommandName}", commandName);
        var sw = Stopwatch.StartNew();

        var result = await next();

        sw.Stop();

        if (result.IsError)
        {
            logger.Log(_errorLevel,
                "Command {CommandName} failed in {ElapsedMilliseconds}ms with errors: {Errors}",
                commandName, sw.ElapsedMilliseconds,
                string.Join(", ", result.Errors.Select(e => e.Code)));
        }
        else
        {
            logger.Log(_afterLevel,
                "Command {CommandName} completed in {ElapsedMilliseconds}ms",
                commandName, sw.ElapsedMilliseconds);
        }

        return result;
    }
}
```

#### `ValidatorBehavior`

在執行 handler 前驗證命令參數。

```csharp
public class ValidatorBehavior<TCommand, TResult>(ICommandValidator<TCommand>? validator)
    : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            return await next();
        }

        var validationResult = validator.Validate(command);
        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }

        return await next();
    }
}
```

#### `DataAnnotationValidator` - 預設 Validator

使用標準 DataAnnotations 進行驗證。

```csharp
public class DataAnnotationValidator<TCommand> : ICommandValidator<TCommand>
    where TCommand : ICommand
{
    public ErrorOr<Success> Validate(TCommand command)
    {
        var context = new ValidationContext(command);
        var results = new List<ValidationResult>();

        if (Validator.TryValidateObject(command, context, results, validateAllProperties: true))
        {
            return Result.Success;
        }

        var errors = results
            .Where(r => r.ErrorMessage is not null)
            .Select(r => Errors.Command.ValidationFailed(r.ErrorMessage!))
            .ToList();

        return errors;
    }
}
```

### 4. Pipeline Configuration

#### `DefaultCommandPipeline`

預設的 Pipeline 配置，定義 Behavior 執行順序。

```csharp
public class DefaultCommandPipeline<TCommand, TResult> : ICommandPipeline<TCommand, TResult>
    where TCommand : ICommand
{
    private readonly List<Type> _behaviors =
    [
        typeof(LoggingBehavior<,>),
        typeof(ValidatorBehavior<,>)
    ];

    /// <summary>
    /// Gets the pipeline behaviors in execution order.
    /// </summary>
    public virtual IReadOnlyList<Type> Behaviors => _behaviors;

    /// <summary>
    /// Insert a behavior before another behavior type.
    /// </summary>
    protected void InsertBefore<TBefore, TNew>()
        where TBefore : IPipelineBehavior<TCommand, TResult>
        where TNew : IPipelineBehavior<TCommand, TResult>
    {
        var index = _behaviors.FindIndex(t =>
            t == typeof(TBefore) || t.Name == typeof(TBefore).Name);
        if (index >= 0)
        {
            _behaviors.Insert(index, typeof(TNew));
        }
    }

    /// <summary>
    /// Insert a behavior after another behavior type.
    /// </summary>
    protected void InsertAfter<TAfter, TNew>()
        where TAfter : IPipelineBehavior<TCommand, TResult>
        where TNew : IPipelineBehavior<TCommand, TResult>
    {
        var index = _behaviors.FindIndex(t =>
            t == typeof(TAfter) || t.Name == typeof(TAfter).Name);
        if (index >= 0)
        {
            _behaviors.Insert(index + 1, typeof(TNew));
        }
    }

    /// <summary>
    /// Remove a behavior from the pipeline.
    /// </summary>
    protected void Remove<TBehavior>()
        where TBehavior : IPipelineBehavior<TCommand, TResult>
    {
        _behaviors.RemoveAll(t =>
            t == typeof(TBehavior) || t.Name == typeof(TBehavior).Name);
    }
}
```

## Usage Examples

### Example 1: Basic Command with Default Behaviors

```csharp
// Command - 使用 DataAnnotations 驗證
[DeviceCmd("report")]
public record BatchReportCommand(
    [property: Required]
    string ReportType,

    [property: Required]
    TimeRange TimeRange,

    SensorFilter? SensorFilter,

    [property: Required]
    string RespTopic,

    [property: Required]
    string DataTopic,

    [property: Range(1, 100)]
    int MaxBatchesPerMessage = 10,

    [property: Range(1, 1000)]
    int TransmissionRateLimit = 100,

    [property: Range(1, 3600)]
    int Timeout = 300
) : ICommand;

// Handler - 啟用 Logging 和 Validation
[Logging]
[Validation]
public class BatchReportCommandHandler : ICommandHandler<BatchReportCommand, Success>
{
    public async Task<ErrorOr<Success>> HandleAsync(
        BatchReportCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        // Implementation...
        return Result.Success;
    }
}
```

### Example 2: Custom Log Levels

```csharp
[Logging(
    BeforeLevel = LogLevel.Information,
    AfterLevel = LogLevel.Information,
    ErrorLevel = LogLevel.Error)]
[Validation]
public class ImportantCommandHandler : ICommandHandler<ImportantCommand, Success>
{
    // ...
}
```

### Example 3: Custom Validator

```csharp
// Custom validator with complex logic
public class BatchReportCommandValidator : ICommandValidator<BatchReportCommand>
{
    private const long MaxTimeRangeMs = 7 * 24 * 60 * 60 * 1000L; // 7 days

    public ErrorOr<Success> Validate(BatchReportCommand command)
    {
        var errors = new List<Error>();

        // DataAnnotations 無法表達的複雜邏輯
        if (command.TimeRange.EndTime <= command.TimeRange.StartTime)
        {
            errors.Add(Errors.Command.ValidationFailed(
                "TimeRange.EndTime must be greater than StartTime"));
        }

        if (command.TimeRange.EndTime - command.TimeRange.StartTime > MaxTimeRangeMs)
        {
            errors.Add(Errors.Command.ValidationFailed(
                "TimeRange cannot exceed 7 days"));
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}

// Handler 使用自訂 validator
[Logging]
[Validation(CustomValidatorType = typeof(BatchReportCommandValidator))]
public class BatchReportCommandHandler : ICommandHandler<BatchReportCommand, Success>
{
    // ...
}
```

### Example 4: Custom Pipeline

```csharp
// 自訂 Behavior
public class RateLimitBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        // Rate limiting logic...
        if (IsRateLimited())
        {
            return Errors.Command.ExecutionFailed("Rate limit exceeded");
        }

        return await next();
    }
}

// 自訂 Pipeline
public class BatchReportPipeline : DefaultCommandPipeline<BatchReportCommand, Success>
{
    public BatchReportPipeline()
    {
        // 在 Logging 前加入 RateLimiting
        InsertBefore<LoggingBehavior<BatchReportCommand, Success>,
                     RateLimitBehavior<BatchReportCommand, Success>>();
    }
}

// 或完全覆寫 Pipeline
public class CustomPipeline : DefaultCommandPipeline<BatchReportCommand, Success>
{
    public override IReadOnlyList<Type> Behaviors =>
    [
        typeof(RateLimitBehavior<,>),
        typeof(LoggingBehavior<,>),
        typeof(ValidatorBehavior<,>),
        typeof(MetricsBehavior<,>)
    ];
}

// Handler 使用自訂 Pipeline
[Pipeline(typeof(BatchReportPipeline))]
[Logging]
[Validation]
public class BatchReportCommandHandler : ICommandHandler<BatchReportCommand, Success>
{
    // ...
}
```

## File Structure

```
src/
├── Weda.SubNode.Abstractions/
│   └── Commands/
│       ├── ICommand.cs
│       ├── ICommandHandler.cs
│       ├── ICommandValidator.cs
│       ├── IPipelineBehavior.cs
│       ├── ICommandPipeline.cs
│       ├── CommandEnvelope.cs
│       ├── Errors.Command.cs
│       ├── Attributes/
│       │   ├── DeviceCmdAttribute.cs
│       │   ├── LoggingAttribute.cs
│       │   ├── ValidationAttribute.cs
│       │   └── PipelineAttribute.cs
│       └── Behaviors/
│           ├── LoggingBehavior.cs
│           └── ValidatorBehavior.cs
│
└── Weda.SubNode.Core/
    └── Commands/
        ├── CommandRegistry.cs
        ├── CommandDispatcher.cs
        ├── DefaultCommandPipeline.cs
        ├── DataAnnotationValidator.cs
        └── Handlers/
            └── BatchReport/
                ├── Models/
                │   ├── BatchReportCommand.cs
                │   ├── TimeRange.cs
                │   └── SensorFilter.cs
                ├── BatchReportCommandHandler.cs
                └── BatchReportCommandValidator.cs  (optional)
```

## Auto-Scan Registration

`CommandRegistry` 會自動掃描 Assembly 中的 Handler：

```csharp
public class CommandRegistry
{
    public void ScanAssembly(Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.GetInterfaces()
                .Any(i => i.IsGenericType &&
                          i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)));

        foreach (var handlerType in handlerTypes)
        {
            var handlerInterface = handlerType.GetInterfaces()
                .First(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

            var commandType = handlerInterface.GetGenericArguments()[0];
            var resultType = handlerInterface.GetGenericArguments()[1];

            // 從 Command 取得名稱
            var deviceCmdAttr = commandType.GetCustomAttribute<DeviceCmdAttribute>();
            if (deviceCmdAttr is null) continue;

            // 從 Handler 取得 Attributes
            var loggingAttr = handlerType.GetCustomAttribute<LoggingAttribute>();
            var validationAttr = handlerType.GetCustomAttribute<ValidationAttribute>();
            var pipelineAttr = handlerType.GetCustomAttribute<PipelineAttribute>();

            var registration = new CommandRegistration(
                CommandName: deviceCmdAttr.Name,
                CommandType: commandType,
                ResultType: resultType,
                HandlerType: handlerType,
                Logging: loggingAttr is not null,
                LoggingSettings: loggingAttr,
                ValidatorType: validationAttr?.CustomValidatorType,
                UseDataAnnotationValidation: validationAttr is not null &&
                                              validationAttr.CustomValidatorType is null,
                PipelineType: pipelineAttr?.PipelineType);

            _registrations[commandAttr.CommandName] = registration;
        }
    }
}
```

## Current Implementation Status

### Implemented Features

#### 1. Global Pipeline Behaviors

Pipeline Behaviors 透過 `CommandRegistry.AddGlobalBehavior()` 註冊，會對所有命令執行：

```csharp
// 在 WedaApplicationContext 初始化時自動註冊
var commandRegistry = new CommandRegistry(logger);

// 註冊 Global Behaviors（所有命令都會執行）
commandRegistry.AddGlobalBehavior(typeof(LoggingBehavior<,>));

// 掃描 Handlers
commandRegistry.ScanAssembly(typeof(CommandRegistry).Assembly);
```

#### 2. Hybrid Validation Pipeline

驗證採用混合模式：

1. **DataAnnotation Validation** - 永遠執行，使用 `[Required]`, `[Range]` 等標準 Annotations
2. **Custom Validator** - 可選，在 Assembly Scan 時自動發現並關聯

```csharp
// Command 使用 DataAnnotations
[DeviceCmd("report")]
public record BatchReportCommand : ICommand
{
    [Required(ErrorMessage = "ReportType is required")]
    public string ReportType { get; init; } = string.Empty;

    [Range(1, 1000, ErrorMessage = "MaxBatchesPerMessage must be between 1 and 1000")]
    public int MaxBatchesPerMessage { get; init; } = 10;
}

// Custom Validator（自動關聯到同名 Command）
public class BatchReportCommandValidator : ICommandValidator<BatchReportCommand>
{
    public ErrorOr<Success> Validate(BatchReportCommand command)
    {
        var errors = new List<Error>();

        // 複雜驗證邏輯
        if (command.TimeRange?.EndTime <= command.TimeRange?.StartTime)
        {
            errors.Add(Errors.Command.ValidationFailed(
                "EndTime must be greater than StartTime"));
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
```

#### 3. Execution Pipeline Order

```
NATS Message
     │
     ▼
┌─────────────────────────────────────┐
│ CommandDispatcher                   │
│ 1. Deserialize to TCommand          │
│ 2. Extract RespTopic                │
│ 3. Send "Received" response         │
│ 4. Run DataAnnotation validation    │◀─ Always runs
│ 5. Run Custom validator (if any)    │◀─ If registered
│ 6. Execute Pipeline Behaviors       │◀─ Global behaviors in order
│    └─ LoggingBehavior               │
│       └─ [more behaviors...]        │
│          └─ Handler.HandleAsync()   │
│ 7. Send Success/Failed response     │
└─────────────────────────────────────┘
```

#### 4. Response Format

CommandResponse 格式符合雲端規格：

```json
{
  "cmd": "deviceCmd",
  "seqId": 100,
  "reqSeqId": "a1b2c3d4-...",
  "rspSeqId": "b2c3d4e5-...",
  "timestamp": 1737004691500,
  "deviceId": "74fe488d5d54",
  "status": 1,
  "message": "Command 'report' executed successfully",
  "data": { ... }
}
```

Status codes:
- `0` = Received (命令已接收，開始執行)
- `1` = Success (執行成功)
- `-1` = Rejected (驗證失敗)
- `-2` = Failed (執行失敗)

### How to Add a Custom Pipeline Behavior

1. **建立 Behavior Class**

```csharp
public class MetricsBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var result = await next();

        sw.Stop();

        // 記錄 metrics
        RecordMetric(typeof(TCommand).Name, sw.ElapsedMilliseconds, result.IsError);

        return result;
    }
}
```

2. **註冊 Global Behavior**

```csharp
// 在 WedaApplicationContext 或自訂初始化中
commandRegistry.AddGlobalBehavior(typeof(MetricsBehavior<,>));
```

Behaviors 會依註冊順序執行（第一個註冊的最先執行）。

## Summary

| Feature | How |
|---------|-----|
| 標記命令名稱 | `[DeviceCmd("name")]` on Command class |
| 啟用 Logging | 自動（已註冊為 Global Behavior） |
| DataAnnotations 驗證 | `[Required]`, `[Range]` on Command properties（永遠執行） |
| 自訂 Validator | 實作 `ICommandValidator<TCommand>`（自動關聯） |
| 新增 Global Behavior | `commandRegistry.AddGlobalBehavior(typeof(MyBehavior<,>))` |
| Response 處理 | Framework 自動處理（根據 RespTopic） |

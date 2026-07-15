# Command → DTDL Mapping

> 以 `BatchReportCommand` 為例，說明 SubNode 的 command 三件套（`*Command` / `*CommandHandler` / `*CommandValidator`）如何對應到 DTDL v3 Interface。

## 三層分工 — 誰進 DTDL，誰留 C#

| C# 角色 | 內容 | 進 DTDL？ |
|---|---|---|
| `BatchReportCommand` (`CommandData<TParameter>` + `Parameters`) | 參數**形狀** | 變成 Command content 的 `request` |
| `BatchReportResult` + 巢狀 result data 類別 | 回應**形狀** | 變成 Command content 的 `response` |
| `[DeviceCmd("report.historical")]` (class-level) | 識別字串 | 變成 Command content 的 `name` |
| `[Description("...")]` (class-level) | 描述 | 變成 Command content 的 `description` |
| `BatchReportCommandHandler` | **執行邏輯** | 不進 — 純 C# runtime，DTDL 不描述 behavior |
| `BatchReportCommandValidator` | **跨欄位 rule**（`EndTime > StartTime`、`StartTime <= now`） | 不進 — DTDL 表達不了，落 `config-rules` 鏡射 |
| `[Validation(typeof(...))]` / `[Logging]` / `[AutoAck(false)]` | C# runtime metadata | 不進 |

**核心原則**：DTDL 描述 **shape**，不描述 behavior，也不描述需要 runtime 狀態（時間、device state）才能判定的 rule。

---

## DTO 對位表

```
BatchReportCommand          ───▶  contents[] 裡的一個 Command entry
  ├ [DeviceCmd("report.historical")]   ├ name = "report.historical"
  ├ [Description("...")]               ├ description
  └ Parameters (POCO)                  └ request.schema → DTMI

BatchReportParameters       ───▶  schemas[] 裡一個 Object :Parameters;1
  ├ TimeRange? TimeRange               ├ field "timeRange"    → DTMI :TimeRange;1
  ├ SensorFilter? SensorFilter         ├ field "sensorFilter" → DTMI :SensorFilter;1
  ├ [Range(1,1000)] int MaxBatches...  ├ field "maxBatchesPerMessage" → "integer"
  ├ [Range(1,100000)] int MaxBatchSize ├ field "maxBatchSize"         → "integer"
  └ [Range(0,10000)] int Transmission... └ field "transmissionRateLimit" → "integer"

TimeRange (record)          ───▶  Object :TimeRange;1
  ├ long StartTime                     ├ field "startTime" → "long"
  └ long EndTime                       └ field "endTime"   → "long"

SensorFilter (record)       ───▶  Object :SensorFilter;1
  ├ string[]? Include                  ├ field "include" → :IncludeList;1 (Array<string>)
  └ string[]? Exclude                  └ field "exclude" → :ExcludeList;1

BatchReportResult           ───▶  Object :Result;1
  ├ int Status                         ├ field "status"
  ├ string Message                     ├ field "message"
  ├ BatchReportResultData? ResultData  ├ field "resultData"   → :ResultData;1
  ├ BatchReportErrorDetails? Error...  ├ field "errorDetails" → :ErrorDetails;1
  ├ long ExecutedAt                    ├ field "executedAt"
  └ long CompletedAt                   └ field "completedAt"
```

`[Range]` / `[Required]` / `[RegularExpression]` 不進 DTDL schema — 它們 echo 到 field 的 `description` 文字（前端 form 渲染時當 hint）並由 `Validator.TryValidateObject` 在 runtime 真正驗。

---

## 完整 DTDL Interface 輸出

把上面那棵樹 flatten 進 v3 Interface，對齊 `examples/system-agent/docs/Metrics/dtdl/CpuSensorConfig.v2.json` 的 shape：

```json
{
  "@context": "dtmi:dtdl:context;3",
  "@id":      "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport;1",
  "@type":    "Interface",
  "displayName": "Batch Report",
  "description": "Query historical telemetry within a time range and emit batched records.",

  "schemas": [
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:TimeRange;1",
      "@type": "Object", "displayName": "Time Range",
      "fields": [
        { "name": "startTime", "displayName": "Start Time", "schema": "long" },
        { "name": "endTime",   "displayName": "End Time",   "schema": "long" }
      ]
    },
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:IncludeList;1",
      "@type": "Array", "elementSchema": "string"
    },
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:ExcludeList;1",
      "@type": "Array", "elementSchema": "string"
    },
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:SensorFilter;1",
      "@type": "Object", "displayName": "Sensor Filter",
      "fields": [
        { "name": "include", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:IncludeList;1" },
        { "name": "exclude", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:ExcludeList;1" }
      ]
    },
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:Parameters;1",
      "@type": "Object", "displayName": "Parameters",
      "fields": [
        { "name": "timeRange",
          "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:TimeRange;1",
          "description": "Time range for historical data query (Unix milliseconds). Defaults to last 10 minutes when omitted." },
        { "name": "sensorFilter",
          "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:SensorFilter;1",
          "description": "Optional filter selecting which sensors to include / exclude." },
        { "name": "maxBatchesPerMessage", "schema": "integer",
          "description": "Maximum number of batches per outbound message. (range 1..1000, default 1)" },
        { "name": "maxBatchSize", "schema": "integer",
          "description": "Maximum samples per batch. (range 1..100000, default 10000)" },
        { "name": "transmissionRateLimit", "schema": "integer",
          "description": "Transmission rate limit (messages per second). 0 disables rate limiting. (range 0..10000, default 0)" }
      ]
    },

    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:ResultData;1",
      "@type": "Object", "displayName": "Result Data",
      "fields": [
        { "name": "batchesSent",  "schema": "integer" },
        { "name": "totalSamples", "schema": "integer" }
      ]
    },
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:ErrorDetails;1",
      "@type": "Object", "displayName": "Error Details",
      "fields": [
        { "name": "errorCode",      "schema": "string" },
        { "name": "recommendation", "schema": "string" }
      ]
    },
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:Result;1",
      "@type": "Object", "displayName": "Result",
      "fields": [
        { "name": "status",       "schema": "integer" },
        { "name": "message",      "schema": "string" },
        { "name": "resultData",   "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:ResultData;1" },
        { "name": "errorDetails", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:ErrorDetails;1" },
        { "name": "executedAt",   "schema": "long" },
        { "name": "completedAt",  "schema": "long" }
      ]
    }
  ],

  "contents": [
    {
      "@type": "Command",
      "name": "report.historical",
      "displayName": "Batch Report",
      "description": "Query historical telemetry within a time range and emit batched records.",
      "request":  { "name": "parameters", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:Parameters;1" },
      "response": { "name": "result",     "schema": "dtmi:advantech:EdgeSync:SubNode:Command:BatchReport:Result;1" }
    }
  ]
}
```

---

## Command vs Property — `contents[]` 的兩種型態

| Capability 類別 | `contents[]` 內的 `@type` | 額外結構 |
|---|---|---|
| Transform / DspFilter / Device / Sensor | `Property` | 只引用 schemas[] 裡的 Object DTMI |
| Command | `Command` | 多 `request` 和 `response` 兩個 named schema reference |

兩者共用同一份 `schemas[]` 收 Enum / Object / Array — emitter 不需要為 command 寫第二套 type-walking 邏輯。

---

## Handler 與 Validator 的歸宿

DTDL 沒寫的，全部在 binary 自帶的附件上：

```
同一份 BatchReport binary 同時 register：

  1. DTDL Interface（上面那坨 JSON）       → 上傳給 cloud / 前端用
  2. ICommandValidator<BatchReportCommand> → runtime 驗跨欄位 rule
  3. ICommandHandler<...>                  → runtime 執行業務邏輯

CommandDispatcher 解 payload 時的流程：

  incoming JSON
   ├── JsonSerializer.Deserialize<BatchReportParameters>           ← 形狀對齊 DTDL
   ├── Validator.TryValidateObject (DataAnnotations [Range] 等)    ← 形狀對齊 DTDL
   ├── BatchReportCommandValidator.Validate (跨欄位 rule)          ← DTDL 不能表達
   └── BatchReportCommandHandler.HandleAsync                       ← 純 behavior
```

`BatchReportCommandValidator` 的兩條 rule（[BatchReportCommandValidator.cs:25-38](../src/Weda.SubNode.Core/Commands/Handlers/BatchReport/BatchReportCommandValidator.cs#L25-L38)）：

```csharp
if (p.TimeRange.EndTime <= p.TimeRange.StartTime) ...
if (p.TimeRange.StartTime > now) ...
```

要讓 cloud / 前端也能驗（在訊息真的送出去之前先擋掉），鏡射到一份 declarative `config-rules`（對齊 system-agent 的兩層 pattern）：

```javascript
// configrules.js — 與 examples/system-agent 同形
const rules = {
  "report.historical": [
    cmd => cmd.parameters?.timeRange?.endTime > cmd.parameters?.timeRange?.startTime
           || { ok: false, reason: "TimeRange.EndTime must be greater than TimeRange.StartTime" },
    cmd => cmd.parameters?.timeRange?.startTime <= Date.now()
           || { ok: false, reason: "TimeRange.StartTime cannot be in the future" },
  ],
};
```

兩邊（C# binary 內 + JS 前端 / cloud 端）跑同一邏輯，是**故意冗餘** — 不同 trust boundary 各自驗一次。同步靠**共用測試 fixture**：[examples/system-agent/docs/Metrics/samples-config/*.configs.json](../examples/system-agent/docs/Metrics/samples-config/) 已有現成 pattern 可抄。

---

## 三個 takeaway

1. **DTDL = shape only**：Properties / fields / 型別 / enum allow-list / 可選文字 hint。runtime invariant 完全不進 DTDL
2. **Command 與 Property 同住 `contents[]`**：差別在 `@type` (`"Command"` vs `"Property"`)，以及是否有 `request` / `response`
3. **Validator 雙存在**：C# `ICommandValidator` 是 truth source；config-rules 是給 cloud / 前端的 mirror。同步靠共用測試 fixture

---

## 相關文件

- [docs/refactor/capability-schema-upload.md](./refactor/capability-schema-upload.md) — 為什麼用 DTDL、Dictionary 盤點、上傳 payload 結構
- [docs/refactor/typed-poco-dtdl-emit.md](./refactor/typed-poco-dtdl-emit.md) — POCO + DataAnnotation → DTDL auto-generation 規則
- [examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md](../examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md) — DTDL parser + config-rules 兩層驗證 reference 實作
- [src/Weda.SubNode.Core/Commands/Handlers/BatchReport/](../src/Weda.SubNode.Core/Commands/Handlers/BatchReport/) — 本文範例 command 的完整 C# 實作

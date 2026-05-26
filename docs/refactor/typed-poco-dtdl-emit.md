# Typed POCO + DTDL Auto-Generation Guide

> Companion to [`capability-schema-upload.md`](./capability-schema-upload.md) 與 [`../dtdl-design.md`](../dtdl-design.md)
> 撰寫日期：2026-05-22

本文件回答下列實作層問題：

1. CRTP / POCO 是什麼，為什麼用這個模式
2. 支援的 DataAnnotation 清單
3. POCO 的寫法守則
4. 哪些 metadata 可以靠 auto-generate，使用者只要寫 POCO
5. 完整範例：transform / DSP filter / command / device / sensor 各一份
6. Unit test 的長相
7. 複雜跨欄位 rule（例：command validator）怎麼放
8. 前端如何消費上傳的 DTDL Interface

---

## 1. CRTP 與 POCO 是什麼

### POCO (Plain Old CLR Object)

「沒有特殊 base class、沒有框架依賴」的純資料類別。SubNode 用 POCO 表達 transform / DSP filter / command / device / sensor 的參數，例如 [src/Weda.SubNode.Core/Dsp/MovingAverageFilter.cs:14-21](../../src/Weda.SubNode.Core/Dsp/MovingAverageFilter.cs#L14-L21)：

```csharp
public class MovingAverageParameters
{
    [Description("Sliding window size (number of samples).")]
    [JsonPropertyName("window")]
    [Range(1, int.MaxValue)]
    [DefaultValue(5)]
    public int Window { get; init; } = 5;
}
```

`MovingAverageParameters` 沒有繼承任何 SubNode base class、沒有 framework lifecycle hook，純粹是個 `class`。這讓 SubNode 可以對它做兩件事：

- **反射**：`DtdlInterfaceEmitter` 讀屬性 + DataAnnotation 自動產出 DTDL Interface JSON
- **序列化**：`System.Text.Json` 把它跟 `Dictionary<string, object>` 互轉（透過 [`TypedParameterConverter`](../../src/Weda.SubNode.Core/Schema/TypedParameterConverter.cs)）

### CRTP (Curiously Recurring Template Pattern)

子類別「把自己」當泛型參數傳給父介面，常用來宣告 `static abstract` 成員。SubNode 的 `IConfigurableTransform<TSelf, TParameter>` 就是這個模式，看 [src/Weda.SubNode.Abstractions/Transforms/IConfigurableTransform.cs:44-46](../../src/Weda.SubNode.Abstractions/Transforms/IConfigurableTransform.cs#L44-L46)：

```csharp
public interface IConfigurableTransform<TSelf, TParameter>
    where TSelf : ITelemetryTransform, IConfigurableTransform<TSelf, TParameter>
    where TParameter : class, new()
```

實作端的長相：

```csharp
public class MovingAverageFilter
    : IDspFilter,
      IConfigurableDspFilter<MovingAverageFilter, MovingAverageParameters>
                          //  ^^^^^^^^^^^^^^^^^^^ 把自己塞回去
{
    public static string TypeName => "movingaverage";
    public static MovingAverageFilter Create(MovingAverageParameters p) => new(p.Window);
    ...
}
```

CRTP 帶來三個能力：

| 能力 | 為什麼需要 |
|---|---|
| `static abstract TypeName` | factory 不用先 new 一個 instance 就能拿到識別字串 |
| `static abstract Create(TParameter)` | factory 用同一條 `paramType` 反射出來，可以無感註冊任何實作 |
| `TParameter : class, new()` | 確保 JSON deserialize 出來能 instantiate |

**一句話**：CRTP 把「型別自我描述」搬到編譯期，factory 就不必為每個實作寫一條註冊程式碼。

---

## 2. 支援的 DataAnnotation

下表是 `DtdlInterfaceEmitter` 支援的 annotation。**寫在 POCO property 上**，emitter 反射出來放進 DTDL 對應位置。

| Annotation | 用途 | 落到 DTDL | 落到 description hint | 落到 runtime |
|---|---|---|---|---|
| `[Required]` | 必填 | 不 emit | `(required)` | ✅ `Validator.TryValidateObject` |
| `[Range(min, max)]` | 數值區間 | 不 emit | `(range min..max)` | ✅ |
| `[StringLength(max, MinimumLength=…)]` | 字串長度 | 不 emit | `(length min..max)` 或 `(max length max)` | ✅ |
| `[MinLength(n)]` / `[MaxLength(n)]` | 字串 / 陣列長度 | 不 emit | `(min length n)` / `(max length n)` | ✅ |
| `[RegularExpression(pattern)]` | 字串 pattern | 不 emit | `(pattern ...)` | ✅ |
| `[AllowedValues(v1, v2, …)]` | 列舉允許值 | 不 emit（建議改用 C# `enum`） | — | ✅ |
| `[Description("...")]` | 文字說明 | `description` 主體 | — | — |
| `[Display(Name="...")]` | UI label | `displayName` | — | — |
| `[Display(Description="...")]` | UI 說明 fallback | `description` 主體（若無 `[Description]`） | — | — |
| `[DefaultValue(v)]` | 預設值 | 不 emit | `(default v)` | runtime 也吃 |
| `[JsonPropertyName("...")]` | 序列化名 override（預設 PascalCase → camelCase） | DTDL field `name` 直接覆寫 | — | — |
| `[EnumMember(Value="...")]` | enum 值文字 override | enumValue 覆寫 enum name | — | — |

### 不支援 / 故意排除

| 型別 / 用法 | 處理 |
|---|---|
| `DateTime` / `Guid` / `Uri` / `TimeSpan` 等 | emitter throw — 改用 `long` (Unix ms) / `string` (UUID) / `string` |
| abstract class / interface property | throw — 用 concrete class，polymorphism 透過 enum discriminator 表達 |
| 自我引用 POCO (`class A { A? Self }`) | throw `Circular reference detected` |
| 自訂 `ValidationAttribute` | **runtime 仍會跑**，只是不會 emit 到 schema |
| C# nullable reference (`string?`) | **不**等於 `[Required]` 缺一 — emitter 採 Rule A：只看 `[Required]`，nullable annotation 忽略 |

---

## 3. POCO 寫法守則

### 3.1 基本骨架

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class ExampleParameters
{
    [Required]
    [Display(Name = "Name")]
    [Description("一行寫清楚這個欄位做什麼")]
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [Display(Name = "Count")]
    [Range(1, 100)]
    [DefaultValue(10)]
    public int Count { get; init; } = 10;

    [Display(Name = "Mode")]
    [JsonPropertyName("mode")]
    public OperatingMode Mode { get; init; } = OperatingMode.Auto;
}

public enum OperatingMode { Auto, Manual, Test }
```

### 3.2 守則

| 守則 | 理由 |
|---|---|
| 屬性用 `{ get; init; }` 不用 setter | 配置物件是一次性建構，避免 runtime 偷改後跟 schema 漂移 |
| **每個 property 給預設值** | 跟 `[DefaultValue]` 一致；JSON 缺欄位時自動補 |
| 用 `enum`，不要用 `string` + `[AllowedValues]` | DTDL 直接 emit 成 `Enum`，型別安全更強 |
| **想要 UI label 一定要寫 `[Display(Name="…")]`** | emitter 不會 auto-humanize；沒寫就 omit，前端 fallback 到 camelCase `name` |
| 巢狀類別用普通 `class` 或 `record`，不要 abstract | emitter 走 reflection 讀 public property |
| 陣列用 `List<T>` 或 `T[]`，不要 `IEnumerable<T>` 之外的特殊集合 | emitter 只認 `List<>` / `T[]` / `IList<>` / `IReadOnlyList<>` |
| Dictionary 只允許 `Dictionary<string, T>` | 對應 DTDL `Map` schema |
| 避免 BCL 「值狀」型別（`DateTime` / `Guid` / `Uri` / `TimeSpan`） | emitter 會 throw；轉成 long / string |

### 3.3 命名

| 階段 | 大小寫 |
|---|---|
| C# property | PascalCase (`MaxBatchSize`) |
| JSON / DTDL field name | camelCase (`maxBatchSize`) — emitter 自動轉 |
| 想客製 | `[JsonPropertyName("maxBatches")]` |
| `displayName` | **不存在 / 由使用者寫**：`[Display(Name = "Max Batch Size")]` |

### 3.4 何時拆 nested class

如果某組欄位**會一起出現、會被別處引用**，拆成 nested POCO。例如 [BatchReportCommand.cs:42-48](../../src/Weda.SubNode.Core/Commands/Handlers/BatchReport/Models/BatchReportCommand.cs#L42-L48)：

```csharp
public class BatchReportParameters
{
    public TimeRange? TimeRange { get; init; }          // ← nested
    public SensorFilter? SensorFilter { get; init; }    // ← nested
    public int MaxBatchSize { get; init; } = 10000;
}

public record TimeRange(long StartTime, long EndTime);
public class SensorFilter { ... }
```

`TimeRange` 在 DTDL 會自動 emit 成獨立的 `Object` 跟 DTMI；其他 command 引用同一個 `TimeRange` 就會共用 DTMI（emitter 用 `Dictionary<Type, string>` dedup）。

---

## 4. 哪些可以靠 auto-generate

**使用者要寫的 ≈ 1 個 POCO + 1 個實作 class（含 4 個 `static abstract`）**，其餘全部由 framework 反射生出。

| 項目 | 誰負責 | 來源 |
|---|---|---|
| Property 列表 | auto | reflection over POCO |
| Property 型別 | auto | C# type → DTDL primitive |
| Property name (camelCase) | auto | `[JsonPropertyName]` 或 PascalCase fallback |
| `displayName` | **使用者** | `[Display(Name=…)]`；沒寫就 omit |
| `description` | auto | `[Description("…")]` + DataAnnotation hint 自動附加 |
| Enum allow-list | auto | `Enum.GetNames(typeof(E))` + `[EnumMember]` 可覆寫 |
| Nested Object DTMI | auto | `dtmi:…:<Category>:<TypeName>:<NestedType.Name>;1` |
| Array element schema | auto | `T[]` / `List<T>` → `elementSchema` |
| Map value schema | auto | `Dictionary<string, T>` → `mapValue` |
| DTMI identifier | auto / opt-in | 由 `TypeName` + Category prefix 推導 |
| Required 必填驗證 | auto | `Validator.TryValidateObject` |
| Range / pattern / length 驗證 | auto | `Validator.TryValidateObject`（runtime） |
| **Type-name 識別字串** | 使用者 | `static string TypeName => "movingaverage"` |
| **建構函式** | 使用者 | `static TSelf Create(TParameter p)` |
| **執行邏輯** | 使用者 | 對應的 `ApplyAsync` / `Read` / `ExecuteAsync` |
| **跨欄位 rule** | 使用者 | `IValidatableObject` 或外掛 `ICommandValidator`（§7） |

換句話說，**使用者寫 transform / device / sensor 時，不用寫**：
- DTDL JSON
- JSON Schema
- Factory 註冊 code
- 序列化 / 反序列化 code
- 大部分驗證 code（除非跨欄位）

---

## 5. 範例

### 5.1 Transform

```csharp
public class MovingAverageTransformParameters
{
    [Display(Name = "Window")]
    [Description("Sliding window size (number of samples).")]
    [Range(1, int.MaxValue), DefaultValue(5)]
    [JsonPropertyName("window")]
    public int Window { get; init; } = 5;
}

public class MovingAverageTransform
    : ITelemetryTransform,
      IConfigurableTransform<MovingAverageTransform, MovingAverageTransformParameters>
{
    public static string TypeName => "movingaverage";
    public static string? Description => "Sliding window arithmetic mean.";
    public static MovingAverageTransform Create(MovingAverageTransformParameters p) => new(p.Window);
    public void UpdateParameters(MovingAverageTransformParameters p) { /* swap */ }
    public IAsyncEnumerable<TelemetryMeasure> ApplyAsync(...) { ... }
}
```

### 5.2 DSP filter

跟 transform 一模一樣，介面換成 `IConfigurableDspFilter<TSelf, TParameter>`，看 [src/Weda.SubNode.Core/Dsp/MovingAverageFilter.cs](../../src/Weda.SubNode.Core/Dsp/MovingAverageFilter.cs) 整份。

### 5.3 Command — 多一個 response 型別

```csharp
[DeviceCmd("report.historical")]
[Display(Name = "Batch Report")]
[Description("Query historical telemetry within a time range and emit batched records.")]
public class BatchReportCommand : CommandData<BatchReportParameters>
{
    public TimeRange GetEffectiveTimeRange() { ... }
}

public class BatchReportParameters
{
    [JsonPropertyName("timeRange")]
    [Display(Name = "Time Range")]
    [Description("Time range for historical data query (Unix milliseconds).")]
    public TimeRange? TimeRange { get; init; }

    [JsonPropertyName("maxBatchSize")]
    [Display(Name = "Max Batch Size")]
    [Range(1, 100000, ErrorMessage = "MaxBatchSize must be between 1 and 100000")]
    [DefaultValue(10000)]
    [Description("Maximum samples per batch.")]
    public int MaxBatchSize { get; init; } = 10000;
}

public record TimeRange(long StartTime, long EndTime);
```

完整 DTDL 輸出見 [docs/command-dtdl.md](../command-dtdl.md)。

### 5.4 Device — 兩棵 POCO

```csharp
public class TcpModbusCommunication
{
    [Required, Display(Name = "Host")]
    [Description("Modbus TCP slave host or IP")]
    [JsonPropertyName("host")]
    public string Host { get; init; } = "";

    [Range(1, 65535), DefaultValue(502), Display(Name = "Port")]
    [Description("TCP port")]
    [JsonPropertyName("port")]
    public int Port { get; init; } = 502;
}

public class TcpModbusProperties
{
    [Range(1, 247), DefaultValue((byte)1), Display(Name = "Slave ID")]
    [Description("Modbus slave unit ID")]
    [JsonPropertyName("slaveId")]
    public byte SlaveId { get; init; } = 1;

    [DefaultValue(ModbusByteOrder.BigEndian), Display(Name = "Byte Order")]
    [JsonPropertyName("byteOrder")]
    public ModbusByteOrder ByteOrder { get; init; } = ModbusByteOrder.BigEndian;
}

public enum ModbusByteOrder
{
    BigEndian, LittleEndian, BigEndianByteSwap, LittleEndianByteSwap
}

public class TcpModbusDevice
    : DeviceBase,
      IConfigurableDevice<TcpModbusDevice, TcpModbusCommunication, TcpModbusProperties>
{
    public static string DeviceTypeName => "tcp-modbus";
    public static string? Description => "Modbus/TCP master client";

    public static TcpModbusDevice Create(
        TcpModbusCommunication comm, TcpModbusProperties props) => new(comm, props);

    public void UpdateProperties(TcpModbusProperties p) { /* swap byte order etc. */ }
}
```

### 5.5 Sensor — 跟 device 綁定識別

```csharp
public class ModbusSensorParameters
{
    [Required, Range(0, ushort.MaxValue)]
    [Display(Name = "Register Address")]
    [Description("Starting register address")]
    [JsonPropertyName("registerAddress")]
    public ushort RegisterAddress { get; init; }

    [Range(1, 125), DefaultValue((ushort)1)]
    [Display(Name = "Register Count")]
    [Description("Number of registers to read (1..125 per Modbus spec)")]
    [JsonPropertyName("registerCount")]
    public ushort RegisterCount { get; init; } = 1;

    [Display(Name = "Register Type")]
    [JsonPropertyName("registerType")]
    public ModbusRegisterType RegisterType { get; init; } = ModbusRegisterType.HoldingRegister;

    [Display(Name = "Data Type")]
    [JsonPropertyName("dataType")]
    public ModbusDataType DataType { get; init; } = ModbusDataType.UInt16;
}

public class TcpModbusSensor
    : IConfigurableSensor<TcpModbusSensor, ModbusSensorParameters>
{
    public static string DeviceTypeName  => "tcp-modbus";    // 對齊 device
    public static string SensorTypeName  => "modbus-register";
    public static string? Description    => "Single/multi-register Modbus read";

    public static TcpModbusSensor Create(ModbusSensorParameters p) => new(p);
}
```

---

## 6. Unit test

### 6.1 跟既有 `DtdlInterfaceEmitterTests` 同形（Shouldly 風格）

看 [tests/Weda.SubNode.Core.Tests/Schema/DtdlInterfaceEmitterTests.cs](../../tests/Weda.SubNode.Core.Tests/Schema/DtdlInterfaceEmitterTests.cs)：

```csharp
public class DtdlInterfaceEmitterTests
{
    private class SimpleParameters
    {
        [Range(1, 100), DefaultValue(10)]
        public int Count { get; init; } = 10;
    }

    [Fact]
    public void Emits_object_with_integer_field_for_int_property()
    {
        var iface = DtdlInterfaceEmitter.Emit(
            new DtdlInterfaceEmitter.Options(
                Prefix: "dtmi:test:Demo", Category: "Transform", TypeName: "Simple"),
            new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(SimpleParameters)));

        var paramsObj = iface["schemas"]!
            .AsArray()
            .Cast<JsonObject>()
            .FirstOrDefault(s => (string?)s["@id"] == "dtmi:test:Demo:Transform:Simple:Parameters;1");

        paramsObj.ShouldNotBeNull();
        var fields = paramsObj["fields"]!.AsArray();
        fields.Count.ShouldBe(1);
        ((string?)fields[0]!["schema"]).ShouldBe("integer");
    }
}
```

### 6.2 Real-world fixture（用 production POCO 當 smoke test）

```csharp
[Fact]
public void Real_world_BatchReportParameters_emits_expected_envelope()
{
    var iface = DtdlInterfaceEmitter.Emit(
        new DtdlInterfaceEmitter.Options(
            Prefix: "dtmi:advantech:EdgeSync:SubNode",
            Category: "Command",
            TypeName: "BatchReport",
            DisplayName: "Batch Report"),
        new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(BatchReportParameters)));

    ((string?)iface["@id"]).ShouldBe("dtmi:advantech:EdgeSync:SubNode:Command:BatchReport;1");
    ((string?)iface["displayName"]).ShouldBe("Batch Report");
}
```

要更嚴格，可加上跑官方 `@azure/dtdl-parser` / `DTDLParser` 驗解 emit 出來的 JSON — 對齊 system-agent 的 `AllInterfacesParse` xUnit fact（[examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md:154-162](../../examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md#L154-L162)）。

### 6.3 Dictionary round-trip

```csharp
[Fact]
public void Dict_to_poco_to_dict_round_trips()
{
    var dict = new Dictionary<string, object> { ["window"] = 7 };

    var poco = TypedParameterConverter.FromDictionary<MovingAverageTransformParameters>(dict);
    poco.Window.ShouldBe(7);

    var dict2 = TypedParameterConverter.ToDictionary(poco);
    ((JsonElement)dict2["window"]).GetInt32().ShouldBe(7);
}

[Fact]
public void Out_of_range_value_fails_validation()
{
    var dict = new Dictionary<string, object> { ["window"] = 0 };

    Should.Throw<ValidationException>(() =>
        TypedParameterConverter.FromDictionary<MovingAverageTransformParameters>(dict));
}
```

---

## 7. 複雜 rule：跨欄位 / 條件 / runtime 狀態

DTDL **不**能表達：欄位互斥、條件必填、依賴 runtime 時間或外部狀態的 invariant。這些走兩個出口：

### 7.1 純資料層 — `IValidatableObject`

POCO 自己實作 `IValidatableObject.Validate`，`Validator.TryValidateObject` 會自動跑。適合「只用 POCO 自己的欄位」就能判定的 rule：

```csharp
public class NetworkSensorParameters : IValidatableObject
{
    [JsonPropertyName("interface")]  public string? Interface  { get; init; }
    [JsonPropertyName("interfaces")] public List<string>? Interfaces { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        var hasSingle = !string.IsNullOrEmpty(Interface);
        var hasList   = Interfaces is { Count: > 0 };

        if (hasSingle && hasList)
        {
            yield return new ValidationResult(
                "Interface and Interfaces are mutually exclusive",
                new[] { nameof(Interface), nameof(Interfaces) });
        }
    }
}
```

`IConfigurableTransform.ValidateParameters` 已經會吃這個（[IConfigurableTransform.cs:79-93](../../src/Weda.SubNode.Abstractions/Transforms/IConfigurableTransform.cs#L79-L93)），不需要再手動呼叫。

### 7.2 需要 runtime 狀態 — 獨立 validator class

依賴「現在幾點」、device state、其他 sensor 的當前值等，POCO 沒辦法獨立判定 — 走 `ICommandValidator` 那種獨立 class。參考 [BatchReportCommandValidator.cs:17-43](../../src/Weda.SubNode.Core/Commands/Handlers/BatchReport/BatchReportCommandValidator.cs#L17-L43)：

```csharp
public class BatchReportCommandValidator : ICommandValidator<BatchReportCommand>
{
    public ErrorOr<Success> Validate(BatchReportCommand command)
    {
        var errors = new List<Error>();
        var p = command.Parameters;

        if (p.TimeRange is not null)
        {
            if (p.TimeRange.EndTime <= p.TimeRange.StartTime)
                errors.Add(Errors.Command.ValidationFailed(
                    "TimeRange.EndTime must be greater than TimeRange.StartTime"));

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (p.TimeRange.StartTime > now)
                errors.Add(Errors.Command.ValidationFailed(
                    "TimeRange.StartTime cannot be in the future"));
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
```

### 7.3 同步到 `config-rules` 給前端 / cloud 看

這條對齊 system-agent 的兩層 pattern（[HOWTO_EVALUATE_DTDL.md §2](../../examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md)）。要讓 cloud / 前端**也**能驗，把同樣的 rule 表達成 declarative 形式（依賴 runtime 的 rule 只能 client-side 表達為 hint，server 端再跑一次）：

```csharp
public static class TcpModbusConfigRules
{
    public static IReadOnlyList<ConfigRule> Rules => new ConfigRule[]
    {
        new("comm.host.required",
            "Communication.host is required",
            (comm, props) => !string.IsNullOrWhiteSpace(comm.GetProperty("host").GetString())),

        new("comm.port.range",
            "Communication.port must be in [1, 65535]",
            (comm, _) => { var p = comm.GetProperty("port").GetInt32(); return p is >= 1 and <= 65535; }),

        new("props.slaveId.range",
            "Properties.slaveId must be in [1, 247]",
            (_, props) => { var s = props.GetProperty("slaveId").GetByte(); return s is >= 1 and <= 247; }),
    };
}
```

跟 DataAnnotation 重複嗎？是 — **這是故意的**：DTDL 只表達結構，runtime rule 在 binary 內走 DataAnnotation，cloud / 前端走 declarative config-rules（最終可上傳成 capability descriptor 的一部份）。兩邊邏輯同步 = 一個 unit test 對兩邊各跑一遍。

---

## 8. 前端整合

### 8.1 上傳 payload 形狀

`SubNodeCapabilitiesDto` 上來時，前端拿到的每個 capability 包含三段：

```json
{
  "typeName": "movingaverage",
  "description": "Sliding window arithmetic mean.",
  "dtdlInterface": { /* 完整一份 DTDL v3 Interface JSON */ },
  "configRules":  [ { "id": "...", "message": "..." } ]   // 可選
}
```

### 8.2 渲染表單的 3 步

**Step 1：parse Interface，挑出 `contents[]` 裡的 Property / Command。**

```typescript
import { ModelParser } from '@azure/dtdl-parser';

const model = await new ModelParser().parse([JSON.stringify(dtdlInterface)]);
const iface = Object.values(model).find(e => e.entityKind === 'Interface');
const formFields = iface!.contents.filter(c => c.entityKind === 'Property');
const commands   = iface!.contents.filter(c => c.entityKind === 'Command');
```

**Step 2：每個 Property 渲染對應 input。**

| `schema` | 渲染 |
|---|---|
| `"string"` | `<input type="text">` |
| `"integer"` / `"long"` | `<input type="number">` |
| `"double"` / `"float"` | `<input type="number" step="any">` |
| `"boolean"` | `<input type="checkbox">` |
| DTMI → `Enum` | `<select>`，options 從 `enumValues` |
| DTMI → `Object` | 遞迴渲染 `fields[]`（fieldset） |
| DTMI → `Array` | repeater，element 依 `elementSchema` 遞迴 |
| DTMI → `Map` | key-value editor |

UI metadata：
- `displayName` → `<label>`（沒寫就 fallback 到 `name`）
- `description` → tooltip / helper text
- `writable: false` → readonly / disabled

**Step 3：套 config-rules 驗證。**

```typescript
const errors = configRules
  .map(rule => rule.evaluate(formValues) ? null : rule.message)
  .filter(Boolean);
```

### 8.3 為什麼前端容易做

DTDL Interface 結構是樹狀且 reference-by-DTMI，跟 React component composition 同形：

```
Interface
└── Property "Parameters"
    └── Object (DTMI ref)
        ├── Field "registerAddress" → integer    → <NumberInput>
        ├── Field "registerCount"   → integer    → <NumberInput>
        ├── Field "registerType"    → Enum DTMI  → <Select>
        └── Field "dataType"        → Enum DTMI  → <Select>
```

寫一個遞迴 `<DtdlFieldRenderer schema={schema} value={…} onChange={…} />` 就涵蓋所有 capability — 不需要為每個 transform / device type 寫客製 form。

### 8.4 跟既有 system-agent 工具鏈一致

驗證走同一套：
- Schema 層：`@azure/dtdl-parser` —[HOWTO_EVALUATE_DTDL.md §1](../../examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md)
- Config 層：`config-rules.js` —[HOWTO_EVALUATE_DTDL.md §2](../../examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md)

兩邊都已經有 Node / .NET 兩個實作，新的 SubNode capability 直接接上 —不用另外寫前端 validator。

---

## 相關文件

- [docs/dtdl-design.md](../dtdl-design.md) — 6 種 capability 完整 DTDL 對應規則（master design doc）
- [docs/command-dtdl.md](../command-dtdl.md) — Command 三件套（Command + Handler + Validator）的 DTDL 映射
- [docs/refactor/capability-schema-upload.md](./capability-schema-upload.md) — 為什麼用 DTDL、Dictionary 盤點、上傳 payload 結構
- [examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md](../../examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md) — DTDL parser + config-rules 兩層驗證 reference 實作
- [src/Weda.SubNode.Core/Schema/DtdlInterfaceEmitter.cs](../../src/Weda.SubNode.Core/Schema/DtdlInterfaceEmitter.cs) — Emitter 實作
- [src/Weda.SubNode.Core/Schema/TypedParameterConverter.cs](../../src/Weda.SubNode.Core/Schema/TypedParameterConverter.cs) — Dictionary 橋接

# DTDL Design — Capability Schema Mapping

> Master design doc：SubNode 六種 capability（command / transform / DSP filter / sensor parameters / device communication / device properties）的 strongly-typed POCO 如何映射到 DTDL v3 Interface。
>
> 配套讀物：
> - [docs/refactor/capability-schema-upload.md](./refactor/capability-schema-upload.md) — 為什麼用 DTDL、Dictionary 盤點
> - [docs/refactor/typed-poco-dtdl-emit.md](./refactor/typed-poco-dtdl-emit.md) — POCO 寫法守則、annotation 對照
> - [docs/command-dtdl.md](./command-dtdl.md) — Command 特例詳述
> - [examples/system-agent/docs/Metrics/dtdl/](../examples/system-agent/docs/Metrics/dtdl/) — 對齊參考 shape

---

## 1. 範圍

SubNode 將下列六種 capability 改成強型別 POCO，由 [DtdlInterfaceEmitter](../src/Weda.SubNode.Core/Schema/DtdlInterfaceEmitter.cs) 自動產生 DTDL v3 Interface，整包上傳給 cloud / 前端消費。

| Capability | DTDL Category 段 | C# Interface | 描述 |
|---|---|---|---|
| Command | `Command` | `ICommandHandler<TCmd, TResult>` + `CommandData<TParam>` | Cloud → SubNode 的指令（含 request + response） |
| Transform | `Transform` | `IConfigurableTransform<TSelf, TParam>` | Telemetry pipeline 內的純資料轉換 |
| DSP filter | `DspFilter` | `IConfigurableDspFilter<TSelf, TParam>` | Telemetry pipeline 內的訊號處理 |
| Sensor parameters | `Sensor` | `IConfigurableSensor<TSelf, TParam>` *(規劃中)* | 每個 sensor 的 protocol-specific 設定（如 Modbus register address） |
| Device communication | `Device` (Property) | `IConfigurableDevice<TSelf, TComm, TProps>` *(規劃中)* | Transport-layer 設定（Host / Port / BrokerUrl ...） |
| Device properties | `Device` (Property) | 同上 | Protocol-specific 設定（SlaveId / ByteOrder ...） |

`Device` 一個 Category 內含兩個 top-level Property（Communication + Properties），其他都是單一 Property。

---

## 2. SubNodeCapabilitiesDto 上傳結構

```
SubNodeCapabilitiesDto
├── Commands[]      → 每個 element 一份 DTDL Interface (Category="Command")
├── Transforms[]    → 每個 element 一份 DTDL Interface (Category="Transform")
├── DspFilters[]    → 每個 element 一份 DTDL Interface (Category="DspFilter")
├── Sensors[]       → 每個 element 一份 DTDL Interface (Category="Sensor")
└── Devices[]       → 每個 element 一份 DTDL Interface (Category="Device")
```

每個 element 是個 descriptor：

```csharp
public record CapabilityDescriptorDto(
    string TypeName,           // "movingaverage" / "report.historical" / "tcp-modbus"
    string? Description,       // class-level [Description("...")]
    JsonObject DtdlInterface,  // emitter 產出的完整一份 Interface JSON
    JsonArray? ConfigRules     // 對應 runtime invariants (DTDL 不能表達的)
);
```

`DtdlInterface` 欄位裝完整 v3 Interface JSON（含 `@context` / `@id` / `@type` / `displayName` / `description` / `contents[]` / `schemas[]`），cloud / 前端拿到後可以直接餵 `@azure/dtdl-parser` 或 `DTDLParser` (.NET) 驗證並渲染。

---

## 3. DTDL Interface 共通 shape

對齊 system-agent `CpuSensorConfig.v2.json` 的結構：

```json
{
  "@context": "dtmi:dtdl:context;3",
  "@id":      "dtmi:advantech:EdgeSync:SubNode:{Category}:{TypeName};{Version}",
  "@type":    "Interface",
  "displayName": "<optional, from Options.DisplayName>",
  "description": "<optional, from Options.Description>",

  "schemas": [
    { "@id": "...:Foo;1",   "@type": "Enum",   "valueSchema": "string", "enumValues": [...] },
    { "@id": "...:Bar;1",   "@type": "Object", "fields": [...] },
    { "@id": "...:TagsList;1", "@type": "Array", "elementSchema": "string" }
  ],

  "contents": [
    { "@type": "Property", "name": "...", "schema": "...:Parameters;1", "writable": true }
    // 或 Command 特例：
    // { "@type": "Command", "name": "...", "request": {...}, "response": {...} }
  ]
}
```

**DTMI segment 命名規則**：

| 元素 | DTMI segment |
|---|---|
| Interface | `{Category}:{TypeName}` |
| Top-level Object（contents[] 的 Property 對應的） | `{Category}:{TypeName}:{PropertyBinding.Name}` |
| Nested Object（POCO 內部巢狀 class） | `{Category}:{TypeName}:{NestedType.Name}` |
| Enum | `{Category}:{TypeName}:{EnumType.Name}` |
| Array | `{Category}:{TypeName}:{FieldName}List` |
| Map (Dictionary<string, T>) | `{Category}:{TypeName}:{FieldName}Map` |

**Dedup**：相同 CLR `Type` 在 schemas[] 內只會出現一次（emitter 用 `Dictionary<Type, string>` 追蹤）。

---

## 4. 六種 Capability 的對應

### 4.1 Command

[詳細範例見 docs/command-dtdl.md](./command-dtdl.md)。

```csharp
[DeviceCmd("report.historical")]
[Display(Name = "Batch Report")]
[Description("Query historical telemetry within a time range.")]
public class BatchReportCommand : CommandData<BatchReportParameters> { }

public class BatchReportParameters
{
    [Display(Name = "Time Range")]
    public TimeRange? TimeRange { get; init; }

    [Display(Name = "Max Batch Size")]
    [Range(1, 100000), DefaultValue(10000)]
    public int MaxBatchSize { get; init; } = 10000;
}
```

```json
"contents": [
  {
    "@type": "Command",
    "name": "report.historical",
    "displayName": "Batch Report",
    "description": "Query historical telemetry within a time range.",
    "request":  { "name": "parameters", "schema": "...:Parameters;1" },
    "response": { "name": "result",     "schema": "...:Result;1" }
  }
]
```

**特例**：Command 是 `contents[]` 內唯一不是 `Property` 的 entry — `@type` 是 `Command`，多 `request` / `response` 兩個 named schema reference，跟 transform / device / sensor 的 Property entry 不同。

`BatchReportCommandHandler` + `BatchReportCommandValidator` 留在 C# binary 內 — **不**進 DTDL。詳見 command-dtdl.md §「Validator 與 Handler 的歸宿」。

### 4.2 Transform

```csharp
public class MovingAverageTransform
    : ITelemetryTransform,
      IConfigurableTransform<MovingAverageTransform, MovingAverageParameters>
{
    public static string TypeName => "movingaverage";
    public static string? Description => "Sliding window arithmetic mean.";

    public static MovingAverageTransform Create(MovingAverageParameters p) => new(p.Window);
    public void UpdateParameters(MovingAverageParameters p) { /* ... */ }
}

public class MovingAverageParameters
{
    [Display(Name = "Window")]
    [Description("Sliding window size (number of samples).")]
    [Range(1, int.MaxValue), DefaultValue(5)]
    [JsonPropertyName("window")]
    public int Window { get; init; } = 5;
}
```

```json
{
  "@context": "dtmi:dtdl:context;3",
  "@id":      "dtmi:advantech:EdgeSync:SubNode:Transform:movingaverage;1",
  "@type":    "Interface",
  "description": "Sliding window arithmetic mean.",
  "schemas": [
    {
      "@id": "dtmi:advantech:EdgeSync:SubNode:Transform:movingaverage:Parameters;1",
      "@type": "Object",
      "fields": [
        { "name": "window", "displayName": "Window", "schema": "integer",
          "description": "Sliding window size (number of samples). (range 1..2147483647, default 5)" }
      ]
    }
  ],
  "contents": [
    { "@type": "Property", "name": "Parameters", "writable": true,
      "schema": "dtmi:advantech:EdgeSync:SubNode:Transform:movingaverage:Parameters;1" }
  ]
}
```

### 4.3 DSP filter

跟 transform 形狀一樣，差別只在 Category 字串：

```csharp
public class KalmanFilter
    : IDspFilter,
      IConfigurableDspFilter<KalmanFilter, KalmanParameters> { ... }
```

DTMI：`dtmi:...:DspFilter:kalman;1`

### 4.4 Sensor parameters

每個 device 自帶一組 sensor type，sensor 的 `Parameters` POCO 描述 protocol-specific 欄位（如 Modbus 的 `RegisterAddress` / `DataType`）：

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
    public static string DeviceTypeName => "tcp-modbus";       // 跟 device 對齊
    public static string SensorTypeName => "modbus-register";
    public static string? Description => "Single/multi-register Modbus read";
    public static TcpModbusSensor Create(ModbusSensorParameters p) => new(p);
}
```

```json
{
  "@id": "dtmi:advantech:EdgeSync:SubNode:Sensor:TcpModbus.Register;1",
  "@type": "Interface",
  "schemas": [
    { "@id": "...:RegisterType;1", "@type": "Enum", ... },
    { "@id": "...:DataType;1", "@type": "Enum", ... },
    { "@id": "...:Parameters;1", "@type": "Object",
      "fields": [
        { "name": "registerAddress", "displayName": "Register Address", "schema": "integer",
          "description": "Starting register address (required, range 0..65535)" },
        { "name": "registerCount", "displayName": "Register Count", "schema": "integer",
          "description": "(range 1..125, default 1)" },
        { "name": "registerType", "displayName": "Register Type",
          "schema": "...:RegisterType;1" },
        { "name": "dataType", "displayName": "Data Type",
          "schema": "...:DataType;1" }
      ]
    }
  ],
  "contents": [
    { "@type": "Property", "name": "Parameters",
      "schema": "...:Parameters;1", "writable": true }
  ]
}
```

### 4.5 Device communication + properties

Device 一個 Interface 內含 **兩個** top-level Property：`Communication` 表 transport 層、`Properties` 表 protocol-specific。

```csharp
// Transport-layer POCO -- already exists in the SubNode SDK, generic across TCP
// protocols (Modbus / OPC-UA / custom). Uses { get; set; } to preserve existing
// runtime path (dict -> POCO via GetObject<>()); has no [DefaultValue] for Port
// so DTDL does not bake the Modbus-specific 502 into a generic TCP shape.
public class TcpCommunicationSettings
{
    [Required, Display(Name = "Host")]
    [Description("TCP host name or IP address.")]
    [JsonPropertyName("host")]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535), Display(Name = "Port")]
    [JsonPropertyName("port")]
    public int Port { get; set; } = 502;
}

// Protocol-specific POCO -- Modbus only. Transport-independent (works for TCP,
// RTU, ASCII).
public class ModbusProperties
{
    [Range(1, 247), Display(Name = "Slave ID")]
    [JsonPropertyName("slaveId")]
    public byte SlaveId { get; init; } = 1;

    [Display(Name = "Byte Order")]
    [JsonPropertyName("byteOrder")]
    public ModbusByteOrder ByteOrder { get; init; } = ModbusByteOrder.BigEndian;
}

// Capability registration lives on the strongly-typed config class -- pure
// descriptor (no Create / UpdateProperties; sensor lifecycle stays with the
// owning device implementation).
public class TcpModbusDeviceConfiguration
    : IDeviceConfiguration,
      IConfigurableDevice<TcpCommunicationSettings, ModbusProperties>
{
    public static string DeviceTypeName => "tcp-modbus";
    public static string? Description   => "Modbus/TCP master client.";

    // ... existing instance members (DeviceName, Host, Port, SlaveId,
    // ToDeviceConfiguration(), AddSensor(...), etc.) unchanged ...
}
```

Emit 出 Device Interface — 注意 `contents[]` 有兩個 Property：

```json
{
  "@id": "dtmi:advantech:EdgeSync:SubNode:Device:tcp-modbus;1",
  "@type": "Interface",
  "schemas": [
    { "@id": "...:ByteOrder;1", "@type": "Enum",
      "enumValues": [
        { "name": "BigEndian",            "enumValue": "BigEndian" },
        { "name": "LittleEndian",         "enumValue": "LittleEndian" },
        { "name": "BigEndianByteSwap",    "enumValue": "BigEndianByteSwap" },
        { "name": "LittleEndianByteSwap", "enumValue": "LittleEndianByteSwap" }
      ]
    },
    { "@id": "...:Communication;1", "@type": "Object",
      "fields": [
        { "name": "host", "displayName": "Host", "schema": "string",
          "description": "(required)" },
        { "name": "port", "displayName": "Port", "schema": "integer",
          "description": "(range 1..65535, default 502)" }
      ]
    },
    { "@id": "...:Properties;1", "@type": "Object",
      "fields": [
        { "name": "slaveId",   "displayName": "Slave ID",   "schema": "integer",
          "description": "(range 1..247, default 1)" },
        { "name": "byteOrder", "displayName": "Byte Order", "schema": "...:ByteOrder;1" }
      ]
    }
  ],
  "contents": [
    { "@type": "Property", "name": "Communication",
      "schema": "...:Communication;1", "writable": true },
    { "@type": "Property", "name": "Properties",
      "schema": "...:Properties;1", "writable": true }
  ]
}
```

---

## 5. POCO 欄位 → DTDL field 對應規則

完整列表（emitter 一視同仁套用到所有 6 種 capability）：

### 5.1 型別映射

| C# 型別 | DTDL field schema | 落點 |
|---|---|---|
| `bool` | `"boolean"` | inline |
| `byte` / `sbyte` / `short` / `ushort` / `int` / `uint` | `"integer"` | inline |
| `long` / `ulong` | `"long"` | inline |
| `float` | `"float"` | inline |
| `double` / `decimal` | `"double"` | inline |
| `string` | `"string"` | inline |
| `enum E` | DTMI 引用 | `schemas[]` → `Enum` |
| 巢狀 `class T` | DTMI 引用 | `schemas[]` → `Object`，遞迴展開 |
| `List<T>` / `T[]` / `IReadOnlyList<T>` etc. | DTMI 引用 | `schemas[]` → `Array(elementSchema)` |
| `Dictionary<string, T>` | DTMI 引用 | `schemas[]` → `Map(mapKey=string, mapValue=...)` |
| `Nullable<T>` | unwrap 成 `T`，按上面對應 | — |

### 5.2 不支援（emitter throw `DtdlInterfaceEmissionException`）

| 型別 | 替代 |
|---|---|
| `DateTime` / `DateTimeOffset` / `DateOnly` / `TimeOnly` | 用 `long`（Unix milliseconds） |
| `TimeSpan` | 用 `long` (ms / ticks) |
| `Guid` / `Uri` | 用 `string` |
| abstract class / interface property | 用 concrete class；polymorphism 用 enum discriminator 表達 |
| 自我引用的 POCO (`class A { A? Self }`) | 不允許（throw `Circular`） |

### 5.3 DataAnnotation 落點

| Annotation | 落到 DTDL | 落到 description hint | 落到 runtime |
|---|---|---|---|
| `[Required]` | — | `(required)` | `Validator.TryValidateObject` |
| `[Range(min, max)]` | — | `(range min..max)` | runtime |
| `[StringLength(max, MinimumLength=min)]` | — | `(length min..max)` 或 `(max length max)` | runtime |
| `[MinLength(n)]` / `[MaxLength(n)]` | — | `(min length n)` / `(max length n)` | runtime |
| `[RegularExpression(pattern)]` | — | `(pattern ...)` | runtime |
| `[AllowedValues(v1, v2)]` | — | — | runtime（建議改用 `enum`） |
| `[DefaultValue(v)]` | — | `(default v)` | runtime |
| `[Description("...")]` | `description` 主體 | — | — |
| `[Display(Name="...")]` | `displayName` | — | — |
| `[Display(Description="...")]` | `description` 主體（fallback） | — | — |
| `[JsonPropertyName("...")]` | field `name` 直接覆寫 camelCase | — | — |
| `[EnumMember(Value="...")]` | enumValue 覆寫 enum name | — | — |

### 5.4 命名

| 階段 | 大小寫 |
|---|---|
| C# property | PascalCase (`RegisterAddress`) |
| Default DTDL field name | camelCase (`registerAddress`) — emitter 自動轉 |
| 想客製 | `[JsonPropertyName("register_address")]` |
| Default DTDL field `displayName` | **不存在** — 沒寫 `[Display]` 就 omit，前端 fallback |
| 客製 displayName | `[Display(Name = "Register Address")]` |

---

## 6. 不能在 DTDL 表達的 runtime invariants

DTDL 只描述 shape。下列規則一律 **不進 DTDL**，全部走 runtime / config-rules 二層架構：

| 規則類型 | 表達在哪裡 |
|---|---|
| 數值區間 / 字串長度 / pattern | `[Range]` / `[StringLength]` / `[RegularExpression]` annotation，runtime `Validator.TryValidateObject` 強制 |
| 同 POCO 內跨欄位 rule（互斥、條件必填） | `IValidatableObject.Validate` 內手寫 |
| 跨 POCO / 依賴 runtime state（時間、device 狀態）| 獨立 `ICommandValidator<TCmd>` class（command 專用） |
| 給 cloud / 前端用的 declarative mirror | `config-rules.cs` / `config-rules.js`（對齊 system-agent 兩層 pattern） |

詳見 [docs/refactor/typed-poco-dtdl-emit.md §7](./refactor/typed-poco-dtdl-emit.md)。

---

## 7. C# API 介面

### 7.1 Emit

```csharp
DtdlInterfaceEmitter.Emit(
    new DtdlInterfaceEmitter.Options(
        Prefix: "dtmi:advantech:EdgeSync:SubNode",
        Category: "Transform",
        TypeName: "movingaverage",
        Description: "Sliding window arithmetic mean."),
    new DtdlInterfaceEmitter.PropertyBinding("Parameters", typeof(MovingAverageParameters)));
```

Device 兩 POCO：

```csharp
DtdlInterfaceEmitter.Emit(
    opts with { Category = "Device", TypeName = "tcp-modbus" },
    new DtdlInterfaceEmitter.PropertyBinding("Communication", typeof(TcpCommunicationSettings)),
    new DtdlInterfaceEmitter.PropertyBinding("Properties",    typeof(ModbusProperties)));
```

回傳 `JsonObject` — 完整 v3 Interface JSON，可直接序列化進 `SubNodeCapabilitiesDto` 上傳。

### 7.2 Dictionary ↔ POCO

```csharp
// runtime config-update 收到 dict，轉強型別 + 驗證：
var typed = TypedParameterConverter.FromDictionary<MovingAverageParameters>(rawDict);

// 寫回檔案 / 上傳 cloud：
var dict = TypedParameterConverter.ToDictionary(typed);
```

`FromDictionary` 在 deserialization 失敗或 DataAnnotation 違規時 throw `ValidationException`，訊息含每條失敗的欄位名跟原因。

---

## 8. 上傳到 cloud / 前端後的消費路徑

```
┌─────────────────┐                    ┌──────────────────────────┐
│  SubNode binary │                    │   cloud / 前端           │
│                 │                    │                          │
│  POCO + Anno    │                    │  @azure/dtdl-parser      │
│        │        │   DTDL Interface   │     ↓                    │
│  DtdlEmitter ───┼───── JSON ────────►│  渲染 form               │
│        │        │                    │     ↓                    │
│  capabilities   │   config-rules     │  config-rules validator  │
│  upload ────────┼─── declarative ───►│     ↓                    │
│                 │                    │  pre-flight check        │
└─────────────────┘                    └──────────────────────────┘
```

DTDL JSON 過官方 parser → 拿到 entity tree → 前端遞迴渲染：

| DTDL element | 前端 component |
|---|---|
| Interface | form root，title=`displayName` ?? `name` |
| Property | form section，label=`displayName` ?? `name`，readonly 由 `writable` 控制 |
| Command | action button / form modal |
| Object | fieldset，內部遞迴 |
| Enum | `<select>`，options 來自 `enumValues` |
| Array | repeater，element 依 `elementSchema` 遞迴 |
| Map | key-value editor |
| primitive (`string`/`integer`/`boolean`/...) | 對應 `<input>` 型別 |

---

## 9. 已知限制

| 限制 | 處理 |
|---|---|
| DTDL v3 沒有 `decimal` 原生型別 | emitter 降級到 `double`（足以表達 IEEE 754 精度） |
| DTDL Property 不能表達 `Range` / `RegularExpression` | 落到 description hint + runtime validator + config-rules 三層保護 |
| 同 POCO 屬性互斥 / 條件必填等跨欄位 rule | `IValidatableObject` + `config-rules` |
| `ulong` 大於 `long.MaxValue` 時溢位 | emit 為 `long`，但 runtime 序列化會出問題 — 不建議用 `ulong` |
| `[AllowedValues]` 字串 enum | DTDL 沒對應；emitter 不 emit，runtime 驗即可，**建議改用 C# `enum`** |
| Polymorphic types（abstract class / interface field） | emitter throw；用 concrete class 配 enum discriminator 表達 |

---

## 10. 對齊參考

- [examples/system-agent/docs/Metrics/dtdl/CpuSensorConfig.v2.json](../examples/system-agent/docs/Metrics/dtdl/CpuSensorConfig.v2.json) — 手寫的標竿 shape
- [examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md](../examples/system-agent/docs/Metrics/HOWTO_EVALUATE_DTDL.md) — DTDL parser + config-rules 兩層驗證方法
- [src/Weda.SubNode.Core/Schema/DtdlInterfaceEmitter.cs](../src/Weda.SubNode.Core/Schema/DtdlInterfaceEmitter.cs) — emitter 實作（~330 行）
- [src/Weda.SubNode.Core/Schema/TypedParameterConverter.cs](../src/Weda.SubNode.Core/Schema/TypedParameterConverter.cs) — Dictionary 橋接
- [tests/Weda.SubNode.Core.Tests/Schema/DtdlInterfaceEmitterTests.cs](../tests/Weda.SubNode.Core.Tests/Schema/DtdlInterfaceEmitterTests.cs) — 36 facts 覆蓋全部 emit 規則（Shouldly 風格）
- [tests/Weda.SubNode.Core.Tests/Schema/TypedParameterConverterTests.cs](../tests/Weda.SubNode.Core.Tests/Schema/TypedParameterConverterTests.cs) — 19 facts 覆蓋 round-trip / validation

---

## 11. 設計確認紀錄（2026-05-29）

> 與需求方確認後鎖定的 envelope / sensor 形狀決策。與 §2~§4 衝突處以本節為準。

### 11.1 已確認

| 決策 | 結論 |
|---|---|
| DTDL 版本 | v3（`dtmi:dtdl:context;3`） |
| Capability 上傳形狀 | envelope 內每類是 `{ name, dtmi }` 索引（sensor 多帶 `deviceType`）；完整 Interface 定義另存（落點見 11.2） |
| Sensor Interface | `Sensor:base` 放通用欄位，各 sensor type 用 `extends` 只貢獻自己的 `Parameters`。取代 §4.4「只放 Parameters」與 `CpuSensorConfig.v2.json`「每份重複 Report/SensorInfo」 |
| Sensor base contents | Name、SensorGroup、Report、**Record**、SensorInfo |
| Pipeline step 形狀 | `Report.transformPipeline[]` / `dspPipeline[]` 的 element = `{ type, dtmi, enabled }`——純引用 catalog，**不**內嵌參數值（值屬 instance，shape 由所引用的 capability Interface 解析）。移除草案中的 `ParamMap` |
| device ↔ sensor 對應 | sensor catalog entry 的 `deviceType` 對應 device catalog entry 的 `name`（即 `IConfigurableSensor.DeviceTypeName` 必須 match `IConfigurableDevice.DeviceTypeName`） |
| Command 分層 | 全部 SubNode level，不分 SubNode / WedaNode |

### 11.2 三個 artifact（已確認）

職責分離成三層，兩段式上傳：

| Artifact | 角色 | 內容 | 上傳時機 |
|---|---|---|---|
| **dtdl** | definition（model） | 完整 DTDL v3 Interface（含 `schemas[]` / `contents[]` / ConfigConstraint） | 註冊 / binary 版本變更時一次 |
| **device-cap** | catalog | 每類 `{ name, dtmi }` 索引（sensor 多帶 `deviceType`） | 隨 cfg-update（輕量） |
| **devicecfg** | instance | 實際設定值（`DeviceConfigs` / Sensors / pipeline 選用 + 參數值） | 隨 cfg-update |

- 一顆 binary 的能力型別固定，故 **dtdl 定義只送一次**；之後 device-cap 用 `{name, dtmi}` 引用、devicecfg 給值。
- DTMI 命名：採 §11.3 的「type 段換 `_`、保留大小寫」。
- DTMI 命名空間：**用 example/owner 名分段以避免碰撞**（SDK 內建 `…:subnode:…`、example 自帶 `…:systemagent:…`）——前端對來源無差別處理，分段純為 DTMI 全域唯一。

### 11.3 DTMI case / 合法字元

**DTDL DTMI 文法**：每個 segment 為 `[A-Za-z](?:[A-Za-z0-9_]*[A-Za-z0-9])?`——首字為字母，內含字母/數字/底線，**不可**以底線結尾。合法：大小寫字母、數字、底線。**不合法**：`.`、`-`、空白。

`DtdlInterfaceEmitter.Sanitize` 現行是 `ToLowerInvariant()` + 只留字母數字（連底線都砍），這是 emitter 的**選擇**、不是 DTDL 的要求——底線與大小寫其實都合法，所以不必接受 `report.historical → reporthistorical` 這種失真。

**採用慣例**（取代現行 Sanitize：從「小寫 + 砍非字母數字」改成「保留大小寫、只換底線」）：

- **保留原始大小寫**，只把 `.` / `-` / 空白換成 `_`、trim 尾端 `_`。**不做 case folding**。大寫合法，無須小寫。
- namespace + category：沿用品牌/分類 PascalCase → `advantech:EdgeSync:SubNode:Device`（對齊既有 `CpuSensorConfig.v2.json`，亦合 DTDL 慣例：org 段小寫、型別/分類段 PascalCase）。
- type 段（runtime id）：`report.historical → report_historical`、`tcp-modbus → tcp_modbus`（原本小寫故維持小寫）。
- nested schema 末段：沿用 C# 型別/屬性名（`Communication` / `ByteOrder`）。
- field `name`：沿用 `[JsonPropertyName]`（`slaveId`）。
- catalog 的 `name` 欄位永遠保留 canonical id（`report.historical`）；`dtmi` 只當定址。
- 例外：擴充 context `dtmi:advantech:edgesync:validation;1` 是 Weda.Dtdl 既定常數，維持小寫；要全一致需在 Weda.Dtdl 改名（breaking）。

### 11.4 Constraint = `validation;1` ConfigConstraint（取代 §5.3 的 description hint）

Range / Required / 等約束**不**塞進 `description`，而是用 emitter 既有的 `dtmi:advantech:edgesync:validation;1` 擴充（`Weda.Dtdl.Shared.DtdlExtensionConstants`）：

- Interface `@context` 為陣列：`["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"]`。
- `contents[]` 的 Property：`@type: ["Property", "ConfigConstraint"]`。
- Object `fields[]` 的 field：`@type: ["Field", "ConfigConstraint"]`。
- 約束欄位（直接放在 Property / Field 物件上）：`required`(bool, 必出)、`writable`(bool, 必出)、`minimum` / `maximum`（[Range]）、`minLength` / `maxLength`（[StringLength]/[MinLength]/[MaxLength]）、`pattern`（[RegularExpression]）、`default`（[DefaultValue]，enum 用 EnumMember 線格字串）。
- `description` 只放純 `[Description]` 文字，不再附「(range 1..247, default 1)」。

### 11.5 職責與元件命名（已確認）

- DTDL 的 parse / validate / emit 全部留在 **Weda.Dtdl** 套件；元件改名：`WedaDtValidator`、`WedaDtdlParser`、`WedaDtdlEmitter`（cross-repo：`weda_dt_validator`）。
- **subnode 不手寫 DTDL**，負責兩件事：(1) 把各 capability 的 DTO/POCO 餵給 `WedaDtdlEmitter` 產生 definition；(2) 組出 device-cap catalog（`{ name, dtmi }` 索引，sensor 多帶 `deviceType`）。
- SDK 共用定義（transform / dsp / command）此階段**跟 example 一起輸出**（暫不抽到 SDK 層）。
- 對應三 artifact：`WedaDtdlEmitter` 產 **dtdl(definition)**；subnode 產 **device-cap(catalog)**；devicecfg 仍是 **instance**。

---

## 12. 分期（Phasing）

### Phase 1 — 串起 device-cap + DTDL 故事，建立強型別 SDK 基礎

**目標**：管線端到端打通（devicecfg → emit → catalog → 上雲），SDK 具備強型別 capability 的完整 emit + catalog 能力。**不**改 example 的字典實作。

- **Weda.Dtdl（track A，cross-repo）**：改名 + `Sanitize` 保留大小寫 + `extends` 支援（見 [TODO-dtdl-refactor.md](../../weda_dt_validator/docs/TODO-dtdl-refactor.md)）。
- **subnode（track B）**：
  - `Sensor:base` POCO + base/extends emit。
  - 把 `IConfigurableSensor` / `IConfigurableDevice` 接進 capability scan → `SubNodeCapabilitiesDto` 增 `devices[]` / `sensors[]`。
  - device-cap catalog DTO（`{ name, dtmi (, deviceType) }`）。
  - 兩段式：definition（註冊時）與 catalog（cfg-update）分離。
  - wire schema 模型定案（B6）。
- **驗證載具**：用**既有強型別的 `tcp-modbus` / testdevice** 跑通 E2E。system-agent 仍字典實作，留 Phase 2。

### Phase 2 — 各 example 改寫為 strong-typed

逐一把 example 的 sensor/device 從字典字串改成強型別 POCO（解 B1 / B2）：

- `system-agent`：各 metric family → `IConfigurableSensor` + Parameters POCO（MetricName enum、MountPoint / Interfaces / PinIds / Sources）；device → `IConfigurableDevice`（或確認空殼）。
- 其他 example（opcua / image-sensor / stock-monitor / power-aggregation / robot / wise-4012 …）同步。

> [dtdl-systemagent-package.md](./dtdl-systemagent-package.md) 是 Phase 2 完成後 system-agent 應 emit 出的 target；Phase 1 期間它仍是手寫參考。

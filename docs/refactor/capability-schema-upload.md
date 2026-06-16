# Capability Schema Upload 研究

> Branch: `feature/capability-schema-upload`
> 撰寫日期：2026-05-22

## 背景

目前 branch (`3449d3e feat: surface transform / DSP filter / command capabilities in configuration upload`) 把 `transform` / `DSP filter` / `command` 的弱型別參數轉為強型別 POCO + DataAnnotations，啟動時用 `JsonSchemaEmitter` 反射產生 **JSON Schema**，包成 `SubNodeCapabilitiesDto` 跟著 `DeviceConfigurationDto` 上傳到 cloud。

本文研究三個延伸問題：

1. 能不能改用 **DTDL** 取代 JSON Schema 上傳？ TBD (try run)
2. `Device.DeviceCommunication`, `Device.Properties` 與 `Sensor.Parameters` 能不能也改成這個強型別模式？
3. 在不轉強型別時，能不能維持 dictionary 的舊路徑（向後相容）？

---

## 現況拆解

### 上傳 payload 結構

```
DeviceConfigurationDto
├── DeviceId
├── Dtdl                          ← 已經是 DTDL Interface JSON（telemetry / properties）
└── DeviceCapabilities (DeviceCapDto)
    ├── Manufacturer, Model, SubNodeType, SwVersion, DeviceName
    ├── DeviceInfo  (Dictionary<string, object>)
    ├── Sensors[]   (ResourceId, Dtmi, Name, SensorGroup, DeviceResourceId)
    └── Capabilities (SubNodeCapabilitiesDto)
        ├── Transforms[]    (TypeName + Description + ParameterSchema)
        ├── DspFilters[]    (TypeName + Description + ParameterSchema)
        └── Commands[]      (Name + Description + ParameterSchema + ResponseSchema + AutoAck)
```

- `Dtdl` 欄位：device twin model（telemetry / properties / commands 的形狀），由 `DtdlGenerator` 從 `Sensor` 產生
- `Capabilities`：SubNode binary 註冊的擴充能力（pipeline 元件、可呼叫的 command）

兩者語意上是不同層的東西：`Dtdl` 描述「device 長什麼樣」，`Capabilities` 描述「SubNode 這顆 binary 能做什麼」。

### Schema 來源

| 項目 | 強型別 POCO | 註冊機制 | Schema 來源 |
| --- | --- | --- | --- |
| Transform | `MovingAverageParameters` 等 | `IConfigurableTransform<TSelf, TParameter>` + `TransformFactory.RegisterAssemblies` | `JsonSchemaEmitter.Emit(typeof(TParameter))` |
| DSP filter | `KalmanParameters` 等 | `IConfigurableDspFilter<TSelf, TParameter>` + `DspFilterFactory` | 同上 |
| Command | `BatchReportParameters` 等 | `CommandRegistry.ScanAssembly` + `[DeviceCmd]` | 同上（含 response 型別） |

### 還在用 `Dictionary<string, object>` 的位置

```csharp
// src/Weda.SubNode.Abstractions/Devices/DeviceConfiguration.cs
public Dictionary<string, object> DeviceCommunication { get; set; } = []; // Host/Port/SlaveId/MacAddress
public Dictionary<string, object> Properties { get; set; } = [];          // ByteOrder, protocol-specific
public Dictionary<string, object> Metadata { get; set; } = [];            // 給 cloud reporting

// src/Weda.SubNode.Abstractions/Telemetry/Sensor.cs
public Dictionary<string, object>? Parameters { get; set; }               // RegisterAddress, DataType...
public Dictionary<string, object>? Metadata { get; set; }
```

值得注意：`TcpModbusDeviceConfiguration` / `OpcUaDeviceConfigurationTyped` / `Wise4012SeConfiguration` 等已經是強型別，但在 `ToDeviceConfiguration()` 投影成 dictionary，**型別資訊在投影過程被擦掉**，因此前端看不到欄位約束。

---

## 1. 改用 DTDL 上傳 capability？

**結論：採 DTDL（v3 context），對齊 system-agent 既有 pattern。**

> 原本傾向「保留 JSON Schema、Commands 並列進 DTDL」的折衷，但盤點 `examples/system-agent/docs/Metrics/dtdl/*` 後改變主意 — 原本擔心的三條技術限制都已經被 system-agent 既有 pattern 解掉了。詳見 [dtdl-design.md](../dtdl-design.md)。

### 為何原本傾向 JSON Schema 的反對都不再成立

| 原反對 | 為何不再成立 |
| --- | --- |
| v2 不能在 Property 的 schema graph 表達 `Array<T>` | system-agent 已改用 `dtmi:dtdl:context;3`，Array 可在 `schemas[]` standalone 由 Property DTMI 引用（`NetworkSensorConfig.Interfaces: Array<string>` 跑得起來） |
| DTDL 沒有 `min/max/pattern/required` | 兩層 pattern：DTDL 描述形狀 + `config-rules.js` / `ConfigRules.cs` 描述 runtime invariants。`HOWTO_EVALUATE_DTDL.md §2` 已把這條協議化 |
| 識別語意混亂 | DTMI naming `dtmi:advantech:EdgeSync:<Project>:<Type>;1` 已是 convention，擴到 `:SubNode:MovingAverage;1` 是 mechanical |

### 推薦走向

- ✅ `Capabilities.Transforms` / `DspFilters` / `Commands` 全部改 DTDL Interface
- ✅ `Device` / `Sensor` 的強型別也走 DTDL（新增 Category）
- ❌ 不要再維護兩套 schema 格式

詳細映射規則與每種 capability 的 DTDL 形狀見 [docs/dtdl-design.md](../dtdl-design.md)。

---

## 2. Device / Sensor properties 強型別化

**結論：可以套同一套模式，且回報品質會明顯提升。**

### 設計提案

複用 `JsonSchemaEmitter`、`IConfigurableXxx` 的同樣模式，給 device 與 sensor parameter 加上：

```csharp
// 給 device impl 用
public interface IConfigurableDevice<TSelf, TCommunication, TProperties>
    where TSelf : IDevice, IConfigurableDevice<TSelf, TCommunication, TProperties>
    where TCommunication : class, new()
    where TProperties   : class, new()
{
    static abstract string DeviceTypeName { get; }   // e.g. "tcp-modbus"
    static abstract string? Description { get; }
}

// 給 sensor impl 用（per-protocol）
public interface IConfigurableSensor<TSelf, TParameter>
    where TParameter : class, new()
{
    static abstract string SensorTypeName { get; }   // e.g. "modbus-register"
}
```

對應 DTO：

```csharp
public record DevicePropertyDescriptorDto(
    string DeviceTypeName,
    string? Description,
    JsonObject DtdlInterface);     // 兩個 Property: Communication + Properties

public record SensorParameterDescriptorDto(
    string DeviceTypeName,         // 跟 device 對齊
    string SensorTypeName,
    string? Description,
    JsonObject DtdlInterface);     // 一個 Property: Parameters
```

掛進 `SubNodeCapabilitiesDto`：

```csharp
public record SubNodeCapabilitiesDto(
    IReadOnlyList<TransformDescriptorDto> Transforms,
    IReadOnlyList<DspFilterDescriptorDto> DspFilters,
    IReadOnlyList<CommandDescriptorDto> Commands,
    IReadOnlyList<DevicePropertyDescriptorDto> Devices,   // ★ new
    IReadOnlyList<SensorParameterDescriptorDto> Sensors); // ★ new
```

### 直接受益

1. `TcpModbusDeviceConfiguration.Host / Port / SlaveId / ByteOrder` 的型別會「上去」到 cloud，前端表單能直接驗證
2. `ModbusSensorReporturation.RegisterAddress (ushort)` / `DataType (enum)` 的限制可直接渲染成下拉、min/max 欄位
3. 不再依賴文件描述「Modbus 的 SlaveId 是 1~247」這種知識散落在 `ModbusDevice.cs` 與 README

### 注意點

- `DeviceCommunication` 中 Host / Port 多協定共用 → 抽 `BaseTransportProperties` 共享
- `Metadata` 是純對外標籤，不必強型別（維持 `additionalProperties: true` 的 open schema）
- Sensor `Parameters` 跟 device type 綁定 → 用 `deviceTypeName + sensorTypeName` 兩段識別

---

## 3. 向後相容 (Dictionary fallback)

**結論：在 factory 層已經支援，新增 device/sensor 強型別也能套同一招。**

### 已驗證可行的路徑

[`TransformFactory.TryRegister`](../../src/Weda.SubNode.Core/Transforms/TransformFactory.cs#L148-L165) 已示範：

```csharp
ITelemetryTransform Factory(Dictionary<string, object> dict)
{
    var json = JsonSerializer.Serialize(dict, JsonOpts);
    var typed = JsonSerializer.Deserialize(json, paramType, JsonOpts);
    // DataAnnotations validate
    return (ITelemetryTransform)createMethod.Invoke(null, [typed])!;
}
```

舊的 dictionary 設定 → JSON round-trip → 新的強型別 POCO → 驗證 → 建構，**舊路徑零改動**。

這個 round-trip 已抽成 [`TypedParameterConverter`](../../src/Weda.SubNode.Core/Schema/TypedParameterConverter.cs)，所有 6 種 capability factory 共用一份。

### 建議的相容策略

| 情境 | 行為 |
| --- | --- |
| Device 已實作 `IConfigurableDevice<...>` | Emit DTDL Interface 上傳；factory 走強型別路徑 |
| Device 還是舊的 `IDevice` + dictionary | 不 emit Interface（或 emit 為空 Object schema）；factory 維持 dictionary 路徑 |
| Cloud 收到沒有 DTDL 的 entry | 前端 fallback 成「自由 key/value 編輯器」 |

關鍵契約：**`DtdlInterface` 欄位在 descriptor 中設計成 nullable / 可省略**，前端有 DTDL 就渲染 form，沒有就 fallback。

### `DtdlInterfaceEmitter` 對 dictionary 的處理

[`DtdlInterfaceEmitter`](../../src/Weda.SubNode.Core/Schema/DtdlInterfaceEmitter.cs) 已能把 `Dictionary<string, T>` 映成 DTDL `Map` schema（`mapKey: string`, `mapValue: T`），所以強型別 POCO 內部仍可保留半結構化欄位（例如自由 metadata 字段），不會逼整棵都強型別。

---

## 行動建議

優先順序（由高到低）：

1. **採 DTDL v3** 全面取代 JSON Schema 路線（見 [dtdl-design.md](../dtdl-design.md)）
2. **擴充強型別到 device / sensor properties**：先把已經是強型別的 `TcpModbusDeviceConfiguration` / `Wise4012SeConfiguration` 透過 `IConfigurableDevice<...>` 註冊，emit `DevicePropertyDescriptorDto`
3. **descriptor 中的 `DtdlInterface` 設為可選**，舊 device impl 不掛強型別 → DtdlInterface 為 null → 前端 fallback dictionary 編輯器
4. **同步維護 `config-rules` 鏡射** runtime invariants（Range / 跨欄位 / 條件必填等 DTDL 不能表達的）

### 不建議做的事

- 把所有 transform / DSP filter 的參數同時保留 JSON Schema + DTDL 兩套：複雜度高、同步負擔大；DTDL v3 + config-rules 已涵蓋所有需求
- 一次把 `Metadata` / `DeviceInfo` 也強型別：這兩個是設計為對外延伸標籤的 open dictionary，保留 Map / `additionalProperties` 即可

---

## 相關文件

- [docs/dtdl-design.md](../dtdl-design.md) — 6 種 capability 完整 DTDL 對應規則（master design doc）
- [docs/command-dtdl.md](../command-dtdl.md) — Command 三件套（Command + Handler + Validator）的 DTDL 映射
- [docs/refactor/typed-poco-dtdl-emit.md](./typed-poco-dtdl-emit.md) — POCO 寫法守則 + annotation 對照

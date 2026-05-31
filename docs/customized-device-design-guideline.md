# Customized Device Design Guideline

> 對象：要在 Weda SubNode SDK 之上實作自己的 device（讀感測器、上報遙測、被雲控）的工程師。
> 範圍：Phase 1 SDK 已具備的「強型別 device + sensor」管線。Phase 2 各 example 改寫的對照範本附在文末。
> 配套：[dtdl-design.md](./dtdl-design.md)（DTDL emit 規則）、[dtdl-systemagent-package.md](./dtdl-systemagent-package.md)（system-agent 目標包樣本）。

---

## 1. 心智模型

`Device` 是 aggregate root，`Sensor` 是它內含的 Entity，`Transform`/`DspFilter` 是裝進 sensor pipeline 的 ValueObject。SDK 上雲時拆三層：

| Artifact | 內容 | 範例片段 |
|---|---|---|
| **dtdl[]** | 完整 DTDL v3 Interface 定義（型別） | `Sensor:base`、`Device:tcp_modbus`、`Sensor:tcp_modbus_modbus_register`、`Transform:unitconversion` … |
| **deviceCapabilities.devices[]** / **sensorTypes[]** / **transforms[]** / **dspFilters[]** / **commands[]** | 能力目錄（catalog），thin `{name, dtmi}` 索引 | `{ "name": "tcp-modbus", "dtmi": "..." }` |
| **deviceCapabilities.sensors[]** | sensor 實例（entity，有 `resourceId`） | `{ "resourceId", "dtmi", "name", "sensorGroup", "deviceResourceId" }` |
| **devicecfg.json**（不在 upload 內） | 實例設定值（哪顆 sensor、Parameters 什麼） | 沿用既有格式 |

關鍵：sensor instance 的 `dtmi` **指向 sensor TYPE 的 dtmi**（多個 instance 共用 dtmi），instance identity 走 `resourceId`。

---

## 2. 何時強型別、何時走 fallback

| 情境 | 走法 |
|---|---|
| 你的 device + sensor 形狀**穩定**、欲讓前端用 catalog 自動渲染 form | **強型別**（本文重點）：寫 `IConfigurableDevice` + `IConfigurableSensor` + Parameters POCO |
| 過渡期、Parameters 仍是 `Dictionary<string, object>` 字典字串 | **untyped fallback**（legacy）：什麼都不做；SDK 啟動會印 warning 提醒 |
| 你只要原型 / 一次性 | untyped fallback 也行；正式上線前再 typed |

untyped 路徑沒被刪——它 fallback 到 `DtdlGenerator.PopulateSensorDtmis` 為每顆 sensor 產獨立 autogen dtmi。Phase 2 各 example 逐個遷移後可以整批退場。

---

## 3. 寫 device 強型別（`IConfigurableDevice`）

### 3.1 拆兩個 POCO：Communication（傳輸層）+ Properties（協定層）

```csharp
// Communication：跨協定共用的 transport 設定
public class TcpCommunicationSettings
{
    [Required, Display(Name = "Host"), Description("TCP host or IP")]
    [JsonPropertyName("host")]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535), Display(Name = "Port")]
    [JsonPropertyName("port")]
    public int Port { get; set; } = 502;
}

// Properties：特定協定才有的設定
public class ModbusProperties
{
    [Range(1, 247), Display(Name = "Slave ID")]
    [JsonPropertyName("slaveId")]
    public byte SlaveId { get; init; } = 1;

    [Display(Name = "Byte Order")]
    [JsonPropertyName("byteOrder")]
    public ModbusByteOrder ByteOrder { get; init; } = ModbusByteOrder.BigEndian;
}
```

### 3.2 在 Configuration class 上掛 `IConfigurableDevice<TComm, TProps>`

```csharp
public class TcpModbusDeviceConfiguration
    : IDeviceConfiguration,
      IConfigurableDevice<TcpCommunicationSettings, ModbusProperties>
{
    public static string DeviceTypeName => "tcp-modbus";
    public static string? Description   => "Modbus/TCP master client.";
    // … 既有 runtime fields 不動
}
```

SDK 啟動時 `DeviceTypeRegistry` 掃到、產生一份 Device Interface（contents 是 `Communication` + `Properties` 兩個 Property，schemas 自動展開），放進 `dtdl[]`，並在 catalog `devices[]` 加 `{name: "tcp-modbus", dtmi: "..."}`。

### 3.3 在 device runtime class 上掛 `[DeviceType]`（**Phase 1 過渡**）

```csharp
[DeviceType("tcp-modbus")]
public class TcpModbusDevice : ModbusDevice
{
    // …
}
```

為什麼還要這個 attribute？`AddDevice<TDevice>("sectionName")` 註冊的是 device class（`MyFirstDevice : TcpModbusDevice`），但 `IConfigurableDevice` 在 config class 上。Phase 1 用 `[DeviceType]` 把兩者連起來；Phase 2 / 將來把這兩件事歸併到一個 class 後可移除。attribute 預設 `Inherited = true`，base class 標一次衍生類自動繼承。

---

## 4. 寫 sensor 強型別（`IConfigurableSensor`）

### 4.1 寫 Parameters POCO——**discriminator 用 enum 鎖**

關鍵原則：當同一個 device type 底下有多個 sensor type（N:1），SDK 用 **try-validate-each** 找出某個 instance 屬於哪個 type。**Parameters POCO 必須讓自己「驗得起、別人驗不起」**——通常靠單值 enum 鎖一個 discriminator 欄位。

```csharp
public enum CpuMetricType
{
    [EnumMember(Value = "cpu")] Cpu     // 只一個值，鎖死
}

public enum CpuMetricName
{
    [EnumMember(Value = "usage")]            Usage,
    [EnumMember(Value = "load1")]            Load1,
    [EnumMember(Value = "load5")]            Load5,
    [EnumMember(Value = "load15")]           Load15,
    [EnumMember(Value = "context_switches")] ContextSwitches,
}

public class CpuMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public CpuMetricType MetricType { get; init; }     // discriminator: enum 把 "memory" 等值擋掉

    [Required, JsonPropertyName("metricName")]
    public CpuMetricName MetricName { get; init; }
}
```

### 4.2 宣告 `IConfigurableSensor<TParam>`

```csharp
public class CpuMetricSensor : IConfigurableSensor<CpuMetricParameters>
{
    public static string DeviceTypeName => "system-monitor";   // 對應某個 IConfigurableDevice
    public static string SensorTypeName => "cpu-metric";
    public static string? Description   => "CPU usage / load counters.";
}
```

SDK 啟動時 `SensorTypeRegistry`：
- 掃到 → emit DTDL Interface（`extends Sensor:base` + 一個 `Parameters` Property，POCO 內欄位變 Object schema），放進 `dtdl[]`。
- catalog `sensorTypes[]` 加 `{ name: "cpu-metric", dtmi, deviceType: "system-monitor" }`。
- runtime 載入 sensor instance 時，用 try-validate-each 找匹配的 type，把 `sensor.Dtmi` 寫成該 type 的 dtmi。

### 4.3 同 device 多 sensor type（N:1）的設計範例

system-agent 一個 device 下有 13 種 metric family，5 種 Parameters shape。每個 family 一份 POCO + sensor class：

| Sensor type | Parameters POCO 差異 |
|---|---|
| `cpu-metric` | MetricType(enum{cpu}) + MetricName(enum cpu 專屬值) |
| `memory-metric` | MetricType(enum{memory}) + MetricName(enum memory 專屬值) |
| `disk-metric` | + `mountPoint: string` Required |
| `network-metric` | + `interfaces: string[]` |
| `gpio-metric` | + `pinIds: int[]` |
| `temperature-metric` | + `sources: string[]` |
| …（其餘 simple 同 cpu pattern） | |

只要每個 POCO 的 discriminator 欄位（`MetricType`）是**單值 enum** + 必要欄位不一致，try-validate 就能唯一命中。

---

## 5. devicecfg.json 完全不動

舊 devicecfg.json sensor entry：
```jsonc
{
  "Name": "cpu_usage",
  "SensorGroup": "SYS",
  "Parameters": { "MetricType": "cpu", "MetricName": "usage" },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": { "Schema": "double", "DisplayName": "CPU Usage", "Description": "..." }
}
```

強型別化後**這個檔案不動**——SDK 由 `Parameters` 內容自動推出 sensor type、寫回 type dtmi。

---

## 6. DTDL 輸出對照（`cpu-metric` 為例）

```jsonc
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:weda:sensor:system_monitor_cpu_metric;1",
  "@type": "Interface",
  "displayName": "cpu-metric",
  "description": "CPU usage / load counters.",
  "extends": "dtmi:advantech:weda:sensor:base;1",
  "schemas": [
    { "@id": "...:CpuMetricType;1", "@type": "Enum", "valueSchema": "string",
      "enumValues": [{ "name": "Cpu", "enumValue": "cpu" }] },
    { "@id": "...:CpuMetricName;1", "@type": "Enum", "valueSchema": "string",
      "enumValues": [{ "name": "Usage", "enumValue": "usage" }, …] },
    { "@id": "...:Parameters;1", "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "metricType",
          "schema": "...:CpuMetricType;1", "required": true, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "metricName",
          "schema": "...:CpuMetricName;1", "required": true, "writable": true } ] }
  ],
  "contents": [
    { "@type": ["Property","ConfigConstraint"], "name": "Parameters",
      "schema": "...:Parameters;1", "required": true, "writable": true }
  ]
}
```

`Sensor:base` Interface 自己一份（hand-authored，含 Name / SensorGroup / Report / Record / SensorInfo 共用欄位），上面這份 `extends` 它。

---

## 7. Startup log 對照

**全部 typed**（健康）：
```
[INF] Scanned IConfigurableSensor<>: 13 sensor types under 'system-monitor'
[INF] Typed dispatch resolved 33 sensors for device 'system-monitor'
[INF] Capability catalog: 1 device, 13 sensor types, 3 transforms, 3 dspFilters, 10 commands
```

**部分 untyped fallback**（提醒遷移）：
```
[WRN] Device 'TestDevice' is not strongly-typed (no IConfigurableDevice registered
       for its DeviceConfigs section). Falling back to legacy DtdlGenerator autogen
       (5 sensors). Migrate via IConfigurableDevice + IConfigurableSensor to enable
       strong-typed catalog and retire this path.
```

**discriminator 寫壞**（啟動就 fail-fast）：
```
[ERR] Sensor 'cpu_usage' Parameters {MetricName, MetricType} ambiguously match
       multiple sensor types for device 'system-monitor': [cpu-metric, memory-metric].
       Disambiguate the POCOs with single-value enums or distinct required fields.
```

**沒任何 type 命中**（POCO 約束太鬆或實際 Parameters 不對）：
```
[ERR] Sensor 'x' Parameters {Foo, Bar} do not match any registered sensor type for
       device 'system-monitor'. Candidates tried: [cpu-metric, memory-metric, …].
```

---

## 8. 常見錯誤對照表

| Startup 報這句 | POCO 該怎麼改 |
|---|---|
| `ambiguously match multiple sensor types: [A, B]` | A、B 的 Parameters POCO discriminator 欄位（通常某個 type 標籤）換成**單值 enum**，互不重疊；或加一個只有自己有的 `[Required]` 欄位 |
| `do not match any registered sensor type` | 對 candidates 列表，逐一檢查：是否 `[Required]` 欄位實際缺值？enum 是否拼錯？POCO 的 `[JsonPropertyName]` 是否對應 dict 的 key（case insensitive） |
| `Duplicate IConfigurableSensor registration: device 'X' sensor 'Y' implemented by multiple classes` | 同 (DeviceTypeName, SensorTypeName) 被兩個 class 註冊 → 砍掉重複 |
| `Duplicate IConfigurableDevice registration: 'X'` | 兩個 class 都用了 `DeviceTypeName=X` → 砍掉重複 |
| `TypedSensorDispatch.Resolve hook is unset` | SensorTypeRegistry 還沒被 touch 過。應該不該發生（host loader 會主動 touch），若見到，檢查 capability registries 初始化順序 |

---

## 9. 不能在 DTDL 表達的 runtime invariants

DTDL Interface 描述形狀；下列規則放在 runtime layer（POCO DataAnnotations / `IValidatableObject`）：

| 規則 | 表達方式 |
|---|---|
| 數值區間、字串長度、pattern | `[Range]` / `[StringLength]` / `[RegularExpression]`（emitter 會轉成 `ConfigConstraint` 擴充欄位 `minimum`/`maximum`/`pattern`） |
| 跨欄位互斥 / 條件必填 | POCO implement `IValidatableObject.Validate` |
| `[DefaultValue(v)]` | 出在 ConfigConstraint 的 `default` 欄位 |
| `[Display(Name="...")]` | 進 DTDL 的 `displayName` |
| `[Description("...")]` | 進 DTDL 的 `description` |
| `[JsonPropertyName("...")]` | 覆寫 DTDL field `name`（預設 camelCase） |
| `[EnumMember(Value="...")]` | enum 線格字串覆寫 |

---

## 10. Phase 2 — example 遷移 step-by-step

以 system-agent 的 `cpu` family 為例。要把字典字串轉強型別。

**Step 1**：在 example 內建一個 Parameters POCO + sensor class。

```csharp
// examples/system-agent/Sensors/Cpu/CpuMetricParameters.cs
public enum CpuMetricType { [EnumMember(Value = "cpu")] Cpu }
public enum CpuMetricName {
    [EnumMember(Value = "usage")] Usage, [EnumMember(Value = "load1")] Load1, …
}
public class CpuMetricParameters {
    [Required, JsonPropertyName("metricType")] public CpuMetricType MetricType { get; init; }
    [Required, JsonPropertyName("metricName")] public CpuMetricName MetricName { get; init; }
}

// examples/system-agent/Sensors/Cpu/CpuMetricSensor.cs
public class CpuMetricSensor : IConfigurableSensor<CpuMetricParameters>
{
    public static string DeviceTypeName => "system-monitor";
    public static string SensorTypeName => "cpu-metric";
    public static string? Description   => "CPU usage / load.";
}
```

**Step 2**：device side。建一個 `IConfigurableDevice` + `[DeviceType]`。

```csharp
public class LocalSystemCommunication { /* 本地 monitor 無 transport，空 POCO 也可 */ }
public class LocalSystemProperties    { /* 空 POCO 或放 platform 偵測選項 */ }

public class LocalSystemMonitorConfiguration
    : IDeviceConfiguration,
      IConfigurableDevice<LocalSystemCommunication, LocalSystemProperties>
{
    public static string DeviceTypeName => "system-monitor";
    public static string? Description   => "Local system metrics collector.";
}

[DeviceType("system-monitor")]
public class LocalSystemAgentDevice : DeviceBase { /* 原本的實作 */ }
```

**Step 3**：重複 Step 1 為 memory / disk / network / gpio / temperature 各寫一個 POCO + sensor class。**devicecfg.json 不動**。

**Step 4**：跑起來，看 startup log：
- 收到 `Typed dispatch resolved 33 sensors for device 'system-monitor'` → 成功。
- 收到 `ambiguously match` → 回頭把 POCO discriminator 改更嚴。

**Step 5**：dump 對照——`scripts/dump-capabilities.sh system-agent`，看 `deviceCapabilities.sensors[*].dtmi` 從 `dtmi:autogen:sys:...` 變成 `dtmi:advantech:weda:sensor:system_monitor_cpu_metric;1`（等等）。

---

## 11. 速查：每個元件的住所

| 元件 | 檔案 | 角色 |
|---|---|---|
| `IConfigurableDevice<TComm, TProps>` | `Weda.SubNode.Abstractions/Devices/` | 宣告 device 強型別 capability |
| `IConfigurableSensor<TParam>` | `Weda.SubNode.Abstractions/Telemetry/` | 宣告 sensor 強型別 capability |
| `[DeviceType]` | `Weda.SubNode.Abstractions/Devices/` | runtime device class ↔ DeviceTypeName 連結（Phase 1 過渡） |
| `SensorBase` | `Weda.SubNode.Abstractions/Telemetry/` | 通用 sensor envelope DTDL Interface（每個 sensor type `extends` 它） |
| `TypedSensorDispatch` | `Weda.SubNode.Abstractions/Telemetry/` | Core → Abstractions 的 dispatch hook |
| `SensorTypeRegistry` | `Weda.SubNode.Core/Telemetry/` | scan、emit、try-validate dispatch |
| `DeviceTypeRegistry` | `Weda.SubNode.Core/Devices/` | scan、emit device Interface |
| `DeviceCapDto` | `Weda.SubNode.Abstractions/Cloud/.../ConfigurationUploadRequest.cs` | upload payload 結構，含 `devices` / `sensorTypes` / `sensors` / `transforms` / `dspFilters` / `commands` |
| `CatalogRefDto` / `SensorTypeCatalogRefDto` | 同上目錄 | thin catalog ref types |
| Customized device 工程師主要寫 | `examples/<your-example>/` | Parameters POCO + IConfigurableSensor + IConfigurableDevice + [DeviceType] device class + Program.cs `AddDevice` |

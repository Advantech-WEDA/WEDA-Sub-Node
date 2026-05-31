# DTDL + device-cap 完整包（system-agent 為例）

> 建立日期：2026-05-29
> 母設計：[dtdl-design.md](./dtdl-design.md)（§11 為最新確認紀錄）
> 狀態：**目標包（target）**。system-agent 目前 sensor/device 為字典字串驅動、無強型別 POCO，故本包尚不能直接 emit——見 [§7 Blocking issues](#7-blocking-issues)。

---

## 1. 三個 artifact：definition / catalog / instance

職責分離成三層，避免把「型別」「能力清單」「設定值」混為一談：

| Artifact | 別名 | 角色 | 回答什麼問題 | 內容 |
|---|---|---|---|---|
| **dtdl** | definition / model | 型別定義 | 「這種東西長什麼形狀」 | 完整 DTDL v3 Interface（`schemas[]` / `contents[]` / ConfigConstraint） |
| **device-cap** | catalog | 能力選單 | 「這顆 binary 提供哪些東西」 | 每類 `{ name, dtmi }` 索引（sensor 多帶 `deviceType`） |
| **devicecfg** | instance | 實例 | 「這台裝置實際被設成怎樣」 | `DeviceConfigs` / Sensors / pipeline 選用 + 參數值 |

### 1.1 為什麼分三層

- **DTDL 描述型別、不存值**。`registerAddress=0`、`interval=5000` 是 instance，永遠在 devicecfg。
- **catalog 只是索引**。`{name, dtmi}` 讓 cfg-update 輕量；完整定義靠 dtmi 解析。
- Transform / DspFilter 是裝進 Sensor 的 **ValueObject**（無 identity、by-value）；Sensor 是 **Entity**（有 `ResourceId`、對應 telemetry）；Device 是 **Aggregate Root**。

### 1.2 兩段式上傳

```
註冊 / binary 版本變更：  上傳 dtdl(definitions) 一次   ── 型別固定，不需重送
每次 cfg-update：         上傳 device-cap(catalog) + devicecfg(instance)
```

### 1.3 前端怎麼消費

```
device-cap.sensors[]  ──pick device type──►  用 deviceType 過濾可用 sensor type
        │ {name, dtmi}
        ▼
   解析 dtmi → 取得 dtdl Interface → @azure/dtdl-parser → 渲染設定表單
        ▲
devicecfg(instance) ──預填值──┘   pipeline step 的 dtmi → 撈 transform/dsp Interface 渲染其參數子表單
```

---

## 2. 命名與擴充慣例（摘要，詳見 dtdl-design.md §11）

- **DTMI**：保留原始大小寫，只把 `.`/`-`/空白換成 `_`。namespace/category 用 PascalCase；type 段用 runtime id 的底線形式。
  - 前綴：SDK 內建 `dtmi:advantech:EdgeSync:SubNode`；example 自帶 `dtmi:advantech:EdgeSync:SystemAgent`（分段純為 DTMI 全域唯一，前端對來源無差別）。
- **@context**：`["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"]`。
- **Constraint**：用 `ConfigConstraint` co-type，欄位 `required`/`writable`/`minimum`/`maximum`/`minLength`/`maxLength`/`pattern`/`default`，**不**塞 description。
- **Sensor**：`Sensor:base` 放通用欄位，各 type 用 `extends` 只貢獻 `Parameters`。
- **device ↔ sensor**：sensor catalog entry 的 `deviceType` 對應 device 的 `name`。

---

## 3. device-cap（catalog）

system-agent 的完整 catalog。`transformation`/`dspFilters`/`commands` 前 8 個是 SDK 內建（auto-register）；`commands` 後 2 個與 `sensors`/`devices` 是 system-agent 自帶。

```jsonc
{
  "deviceId": "<cloud 配發>",
  "subNode": {
    "name": "Advantech-System-Agent", "subNodeType": "SystemMonitor",
    "manufacturer": "Advantech", "model": "SystemAgent", "swVersion": "1.0.0"
  },
  "capabilities": {
    "devices": [
      { "name": "system-monitor", "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Device:system_monitor;1" }
    ],
    "sensors": [
      { "name": "cpu",               "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu;1",               "deviceType": "system-monitor" },
      { "name": "memory",            "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:memory;1",            "deviceType": "system-monitor" },
      { "name": "disk",              "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:disk;1",              "deviceType": "system-monitor" },
      { "name": "network",           "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:network;1",           "deviceType": "system-monitor" },
      { "name": "system",            "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:system;1",            "deviceType": "system-monitor" },
      { "name": "gpu",               "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:gpu;1",               "deviceType": "system-monitor" },
      { "name": "hwinfo",            "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:hwinfo;1",            "deviceType": "system-monitor" },
      { "name": "temperature",       "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:temperature;1",       "deviceType": "system-monitor" },
      { "name": "voltage",           "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:voltage;1",           "deviceType": "system-monitor" },
      { "name": "fanspeed",          "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:fanspeed;1",          "deviceType": "system-monitor" },
      { "name": "gpio",              "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:gpio;1",              "deviceType": "system-monitor" },
      { "name": "watchdog",          "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:watchdog;1",          "deviceType": "system-monitor" },
      { "name": "thermalprotection", "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:thermalprotection;1", "deviceType": "system-monitor" }
    ],
    "commands": [
      { "name": "report.historical", "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical;1" },
      { "name": "report.data",       "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:report_data;1" },
      { "name": "ai.get",            "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get;1" },
      { "name": "ao.get",            "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:ao_get;1" },
      { "name": "di.get",            "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:di_get;1" },
      { "name": "do.get",            "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:do_get;1" },
      { "name": "ao.set",            "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:ao_set;1" },
      { "name": "do.set",            "dtmi": "dtmi:advantech:EdgeSync:SubNode:Command:do_set;1" },
      { "name": "system.reboot",     "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Command:system_reboot;1" },
      { "name": "system.shutdown",   "dtmi": "dtmi:advantech:EdgeSync:SystemAgent:Command:system_shutdown;1" }
    ],
    "transformation": [
      { "name": "unitconversion", "dtmi": "dtmi:advantech:EdgeSync:SubNode:Transform:unitconversion;1" },
      { "name": "calibration",    "dtmi": "dtmi:advantech:EdgeSync:SubNode:Transform:calibration;1" },
      { "name": "chunking",       "dtmi": "dtmi:advantech:EdgeSync:SubNode:Transform:chunking;1" }
    ],
    "dspFilters": [
      { "name": "kalman",        "dtmi": "dtmi:advantech:EdgeSync:SubNode:DspFilter:kalman;1" },
      { "name": "movingaverage", "dtmi": "dtmi:advantech:EdgeSync:SubNode:DspFilter:movingaverage;1" },
      { "name": "relu",          "dtmi": "dtmi:advantech:EdgeSync:SubNode:DspFilter:relu;1" }
    ]
  }
}
```

> `system.reboot` / `system.shutdown` 的 `[DeviceCmd]` 字串以 system-agent 原始碼為準（此處用慣例名）。

---

## 4. DTDL definitions — Sensor

### 4.1 Sensor:base（通用，所有 sensor `extends` 它）

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SubNode:Sensor:base;1",
  "@type": "Interface",
  "displayName": "Sensor (base)",
  "description": "Generic sensor-entry fields shared by every sensor type.",
  "schemas": [
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:PipelineStep;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "type",    "schema": "string",  "required": true,  "writable": true, "description": "Chosen transform/dsp catalog name, e.g. unitconversion" },
        { "@type": ["Field","ConfigConstraint"], "name": "dtmi",    "schema": "string",  "required": true,  "writable": true, "description": "DTMI of the chosen capability Interface; resolve to render its param form" },
        { "@type": ["Field","ConfigConstraint"], "name": "enabled", "schema": "boolean", "required": false, "writable": true } ] },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:PipelineList;1",
      "@type": "Array", "elementSchema": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:PipelineStep;1" },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:Report;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "enabled",           "schema": "boolean", "required": false, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "interval",          "schema": "integer", "required": false, "writable": true, "description": "Sampling interval (ms)" },
        { "@type": ["Field","ConfigConstraint"], "name": "unit",              "schema": "string",  "required": false, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "transformPipeline", "schema": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:PipelineList;1", "required": false, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "dspPipeline",       "schema": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:PipelineList;1", "required": false, "writable": true } ] },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:Record;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "enabled",  "schema": "boolean", "required": false, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "interval", "schema": "integer", "required": false, "writable": true, "description": "Recording interval (ms)" } ] },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:SensorInfo;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "schema",      "schema": "string", "required": true,  "writable": true, "description": "Wire schema: double / long / integer / boolean / string / MIME type" },
        { "@type": ["Field","ConfigConstraint"], "name": "description", "schema": "string", "required": false, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "displayName", "schema": "string", "required": false, "writable": true } ] }
  ],
  "contents": [
    { "@type": ["Property","ConfigConstraint"], "name": "Name",        "schema": "string", "required": true,  "writable": false },
    { "@type": ["Property","ConfigConstraint"], "name": "SensorGroup", "schema": "string", "required": true,  "writable": false, "description": "AI/DO/DI/SYS/TEMP/PWR" },
    { "@type": ["Property","ConfigConstraint"], "name": "Report",      "schema": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:Report;1",     "required": false, "writable": true },
    { "@type": ["Property","ConfigConstraint"], "name": "Record",      "schema": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:Record;1",     "required": false, "writable": true },
    { "@type": ["Property","ConfigConstraint"], "name": "SensorInfo",  "schema": "dtmi:advantech:EdgeSync:SubNode:Sensor:base:SensorInfo;1", "required": false, "writable": true }
  ]
}
```

### 4.2 Sensor type 三種 archetype

system-agent 13 個 sensor type 落在三種形狀：**simple**（只有 MetricType+MetricName）、**+string**（多一個 string 參數）、**+array**（多一個 array 參數）。

**Archetype A — simple（以 `cpu` 為例）**

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu;1",
  "@type": "Interface",
  "displayName": "CPU Sensor",
  "extends": "dtmi:advantech:EdgeSync:SubNode:Sensor:base;1",
  "schemas": [
    { "@id": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu:MetricType;1",
      "@type": "Enum", "valueSchema": "string",
      "enumValues": [ { "name": "Cpu", "enumValue": "cpu" } ] },
    { "@id": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu:MetricName;1",
      "@type": "Enum", "valueSchema": "string",
      "enumValues": [
        { "name": "Usage",           "enumValue": "usage" },
        { "name": "Load1",           "enumValue": "load1" },
        { "name": "Load5",           "enumValue": "load5" },
        { "name": "Load15",          "enumValue": "load15" },
        { "name": "ContextSwitches", "enumValue": "context_switches" } ] },
    { "@id": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu:Parameters;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "metricType", "schema": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu:MetricType;1", "required": true, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "metricName", "schema": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu:MetricName;1", "required": true, "writable": true } ] }
  ],
  "contents": [
    { "@type": ["Property","ConfigConstraint"], "name": "Parameters", "schema": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:cpu:Parameters;1", "required": true, "writable": true }
  ]
}
```

**Archetype B — +string（以 `disk` 為例，多 `mountPoint`）**

`Parameters` 多一個 field：

```json
{ "@type": ["Field","ConfigConstraint"], "name": "mountPoint", "schema": "string", "required": true, "writable": true, "description": "e.g. /" }
```
`disk` 的 `MetricName` enum：`total / available / used / usage_percent`。

**Archetype C — +array（以 `network` 為例，多 `interfaces`）**

新增一個 Array schema 並由 `Parameters` 引用：

```json
{ "@id": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:network:InterfacesList;1",
  "@type": "Array", "elementSchema": "string" }
```
```json
{ "@type": ["Field","ConfigConstraint"], "name": "interfaces", "schema": "dtmi:advantech:EdgeSync:SystemAgent:Sensor:network:InterfacesList;1", "required": false, "writable": true }
```
`network` 的 `MetricName` enum：`bytes_sent / bytes_received / packets_sent / packets_received / errors`。

### 4.3 全部 13 個 sensor type 速查表

每個 type = 一份 `extends Sensor:base` 的 Interface，差別只在 `MetricType`（固定）、`MetricName` enum、及額外參數。

| Sensor type | archetype | MetricName enum 值 | 額外參數 | 備註 |
|---|---|---|---|---|
| `cpu` | A | usage, load1, load5, load15, context_switches | — | |
| `memory` | A | total, available, used, cached, swap_total, swap_free | — | |
| `system` | A | time, boot_time | — | |
| `gpu` | A | utilization | — | |
| `hwinfo` | A | motherboardname, manufacturer, biosrevision | — | wire schema=string |
| `voltage` | A | voltage | — | |
| `fanspeed` | A | fanspeed | — | |
| `watchdog` | A | isSupported | — | wire schema=boolean |
| `thermalprotection` | A | isSupported | — | wire schema=boolean |
| `disk` | B | total, available, used, usage_percent | `mountPoint: string` | |
| `temperature` | C | therm | `sources: Array<string>` | |
| `gpio` | C | isSupported, pinState | `pinIds: Array<integer>` | pinIds 僅 pinState 用 |
| `network` | C | bytes_sent, bytes_received, packets_sent, packets_received, errors | `interfaces: Array<string>` | |

> wire schema（double/long/integer/...）是**實例層**欄位（`SensorInfo.schema`），同一 type 不同 metric 可不同（如 `cpu.usage`=double、`cpu.context_switches`=long），故不固定在型別定義。

---

## 5. DTDL definitions — Device

system-agent 的 device 是本地監控，**無 transport**（無 Communication / Properties）：

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SystemAgent:Device:system_monitor;1",
  "@type": "Interface",
  "displayName": "Local System Monitor",
  "description": "Monitors the local host. No transport configuration.",
  "contents": []
}
```

> 對照「肥」的 Device（如 Modbus）：`contents[]` 會有 `Communication`（host/port）+ `Properties`（slaveId/byteOrder）兩個 ConfigConstraint Property，各帶 `minimum`/`maximum`/`default`。完整範例見 dtdl-design.md §4.5 與本次對話。

---

## 6. DTDL definitions — Command / Transform / DspFilter

### 6.1 Transform（3 個，SDK 內建）

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SubNode:Transform:unitconversion;1",
  "@type": "Interface",
  "displayName": "Unit Conversion",
  "description": "Convert numeric measurements between temperature units (C/F/K).",
  "schemas": [
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Transform:unitconversion:Parameters;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "fromUnit", "schema": "string", "required": true, "writable": true, "description": "Source unit. Accepted: C, F, K, celsius, fahrenheit, kelvin." },
        { "@type": ["Field","ConfigConstraint"], "name": "toUnit",   "schema": "string", "required": true, "writable": true, "description": "Target unit. Accepted: C, F, K, celsius, fahrenheit, kelvin." } ] }
  ],
  "contents": [
    { "@type": ["Property","ConfigConstraint"], "name": "Parameters", "schema": "dtmi:advantech:EdgeSync:SubNode:Transform:unitconversion:Parameters;1", "required": true, "writable": true }
  ]
}
```

- **`calibration`**：`Parameters` = `scale`(double, default 1.0)、`offset`(double, default 0.0)、`calibrationCurve`(Array of `{rawValue:double, calibratedValue:double}`, optional)。
- **`chunking`**：`Parameters` = `chunkSize`(integer, **minimum 1024, maximum 768000, default 262144**)。

### 6.2 DspFilter（3 個，SDK 內建）

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SubNode:DspFilter:kalman;1",
  "@type": "Interface",
  "displayName": "Kalman Filter",
  "description": "1D Kalman filter that fuses noisy measurements with a process model.",
  "schemas": [
    { "@id": "dtmi:advantech:EdgeSync:SubNode:DspFilter:kalman:Parameters;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "processNoise",     "schema": "double", "required": false, "writable": true, "minimum": 0, "default": 0.01, "description": "Process noise (Q), >= 0." },
        { "@type": ["Field","ConfigConstraint"], "name": "measurementNoise", "schema": "double", "required": false, "writable": true, "default": 0.1, "description": "Measurement noise (R), > 0." } ] }
  ],
  "contents": [
    { "@type": ["Property","ConfigConstraint"], "name": "Parameters", "schema": "dtmi:advantech:EdgeSync:SubNode:DspFilter:kalman:Parameters;1", "required": true, "writable": true }
  ]
}
```

- **`movingaverage`**：`Parameters` = `window`(integer, **minimum 1, maximum 2147483647, default 5**)。
- **`relu`**：`Parameters` = 空 Object（`fields: []`）。

### 6.3 Command — `report.historical`（特例：`@type":"Command"`）

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical;1",
  "@type": "Interface",
  "displayName": "Batch Report",
  "description": "Query historical telemetry within a time range and emit batched records.",
  "schemas": [
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:TimeRange;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "startTime", "schema": "long", "required": false, "writable": true, "description": "Unix ms" },
        { "@type": ["Field","ConfigConstraint"], "name": "endTime",   "schema": "long", "required": false, "writable": true, "description": "Unix ms" } ] },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:StringList;1",
      "@type": "Array", "elementSchema": "string" },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:SensorFilter;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "include", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:StringList;1", "required": false, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "exclude", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:StringList;1", "required": false, "writable": true } ] },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:Parameters;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "timeRange",             "schema": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:TimeRange;1",    "required": false, "writable": true, "description": "Defaults to last 10 minutes when omitted." },
        { "@type": ["Field","ConfigConstraint"], "name": "sensorFilter",          "schema": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:SensorFilter;1", "required": false, "writable": true },
        { "@type": ["Field","ConfigConstraint"], "name": "maxBatchesPerMessage",  "schema": "integer", "required": false, "writable": true, "minimum": 1, "maximum": 1000,   "default": 1 },
        { "@type": ["Field","ConfigConstraint"], "name": "maxBatchSize",          "schema": "integer", "required": false, "writable": true, "minimum": 1, "maximum": 100000, "default": 10000 },
        { "@type": ["Field","ConfigConstraint"], "name": "transmissionRateLimit", "schema": "integer", "required": false, "writable": true, "minimum": 0, "maximum": 10000,  "default": 0, "description": "msgs/sec, 0 disables." } ] },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:Result;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "status",      "schema": "integer", "required": true,  "writable": false },
        { "@type": ["Field","ConfigConstraint"], "name": "message",     "schema": "string",  "required": false, "writable": false },
        { "@type": ["Field","ConfigConstraint"], "name": "executedAt",  "schema": "long",    "required": false, "writable": false },
        { "@type": ["Field","ConfigConstraint"], "name": "completedAt", "schema": "long",    "required": false, "writable": false } ] }
  ],
  "contents": [
    { "@type": "Command", "name": "report.historical",
      "request":  { "name": "parameters", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:Parameters;1" },
      "response": { "name": "result",     "schema": "dtmi:advantech:EdgeSync:SubNode:Command:report_historical:Result;1" } }
  ]
}
```

> `Result` 只列頂層欄位；`resultData`（BatchesSent/TotalSamples/DataGaps[]…）依同規則展開。`AutoAck` 不進 DTDL（runtime 行為），放 descriptor。Command 的 `name` 用 canonical（含點），與 `@id` 的 `report_historical` 區隔。

### 6.4 Command — `report.data`

`request` Parameters：`sensorShortResourceId`(string, **required**)、`resourceTimestamp`(long, **required**)、`transferId`(string, optional)。`response` Result：status/message/executedAt/completedAt + `resultData{ sensorShortResourceId, resourceTimestamp, transferId?, dataTransferred:boolean }`。

### 6.5 Command — get/set 家族（6 個，形狀同構）

`ai.get` / `ao.get` / `di.get` / `do.get` / `ao.set` / `do.set` 共用同一骨架，只差讀/寫與 value 型別：

| Command | request Parameters | response resultData |
|---|---|---|
| `ai.get` / `ao.get` | `deviceName?:string`, `inputs`/`outputs`:`Array<string>` | `values: Array<{name, value:double, deviceName?}>`, `errors?: Array<{name, error}>` |
| `di.get` / `do.get` | `deviceName?:string`, `inputs`/`outputs`:`Array<string>` | `values: Array<{name, state:boolean, deviceName?}>`, `errors?` |
| `ao.set` | `deviceName?`, `outputs`:`Array<{name(req), value:object}>`(**required, minLength 1**) | `{successCount, failureCount, outputs: Array<{name, value?, success, error?, deviceName?}>}` |
| `do.set` | `deviceName?`, `outputs`:`Array<{name(req), state:boolean}>`(**required, minLength 1**) | `{successCount, failureCount, outputs: Array<{name, state, success, error?, deviceName?}>}` |

以 `ai.get` 為完整範例：

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get;1",
  "@type": "Interface",
  "displayName": "Get Analog Input",
  "schemas": [
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get:InputsList;1",
      "@type": "Array", "elementSchema": "string" },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get:Parameters;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "deviceName", "schema": "string", "required": false, "writable": true, "displayName": "Target Device" },
        { "@type": ["Field","ConfigConstraint"], "name": "inputs",     "schema": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get:InputsList;1", "required": false, "writable": true, "displayName": "Input Names" } ] },
    { "@id": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get:Result;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "status",  "schema": "integer", "required": true,  "writable": false },
        { "@type": ["Field","ConfigConstraint"], "name": "message", "schema": "string",  "required": false, "writable": false } ] }
  ],
  "contents": [
    { "@type": "Command", "name": "ai.get",
      "request":  { "name": "parameters", "schema": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get:Parameters;1" },
      "response": { "name": "result",     "schema": "dtmi:advantech:EdgeSync:SubNode:Command:ai_get:Result;1" } }
  ]
}
```

### 6.6 Command — `system.reboot` / `system.shutdown`（system-agent 自帶）

兩者共用 `SystemCommandParameters`：`delaySeconds`(integer, default 5)。

```json
{
  "@context": ["dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"],
  "@id": "dtmi:advantech:EdgeSync:SystemAgent:Command:system_reboot;1",
  "@type": "Interface",
  "displayName": "System Reboot",
  "schemas": [
    { "@id": "dtmi:advantech:EdgeSync:SystemAgent:Command:system_reboot:Parameters;1",
      "@type": "Object",
      "fields": [
        { "@type": ["Field","ConfigConstraint"], "name": "delaySeconds", "schema": "integer", "required": false, "writable": true, "default": 5, "displayName": "Delay (seconds)" } ] }
  ],
  "contents": [
    { "@type": "Command", "name": "system.reboot",
      "request": { "name": "parameters", "schema": "dtmi:advantech:EdgeSync:SystemAgent:Command:system_reboot:Parameters;1" } }
  ]
}
```
`system.shutdown` 同構，換 `@id` / `name` / displayName。

---

## 7. Blocking issues

按嚴重度排序。前 3 個不解掉，本包就只能是手寫 target、無法由 SDK 自動產生。

| # | Blocking issue | 影響 | 解法 / 依賴 |
|---|---|---|---|
| B1 | **system-agent sensor 無強型別 POCO**：參數是 `Parameters["MetricType"]` 字典字串（`SensorResolver` / `SystemMetricsParser` 直接讀 dict） | emitter 反射不到型別 → §4 的 cpu/disk/network… **無法 emit**，只能手寫 | 需為各 metric family 撰寫 `IConfigurableSensor<TParam>` + Parameters POCO（含 MetricName enum、MountPoint/Interfaces/PinIds/Sources） |
| B2 | **system-agent device 非 `IConfigurableDevice`**：`LocalSystemAgentDevice` 是自訂 device，無 comm/props POCO | §5 的 Device Interface 只能空殼或手寫 | 決定：要嘛接受空 `contents[]`、要嘛補一個（即使空的）comm/props POCO |
| B3 | **Weda.Dtdl 尚未改造**：`Sanitize` 還是小寫+砍、無 `extends`、元件未改名 | emit 出的 dtmi 與本包（保留大小寫+底線、base+extends）**對不上** | 依 [weda_dt_validator/docs/TODO-dtdl-refactor.md](../../weda_dt_validator/docs/TODO-dtdl-refactor.md) 任務 2、3 |
| B4 | **subnode 未 emit Sensor/Device**：`SubNodeCapabilitiesDto` 只有 transforms/dspFilters/commands | catalog 的 `devices[]` / `sensors[]` 還產不出來 | subnode 接 `IConfigurableSensor`/`IConfigurableDevice` 進 `GetDescriptors()` |
| B5 | **兩段式未實作**：現行 descriptor 把完整 DTDL inline（path X） | 還沒有「definition 一次 + catalog `{name,dtmi}`」的分離 | 定義 device-cap catalog DTO（`{name,dtmi(,deviceType)}`），definition 另存 |
| B6 | **wire schema 模型未定**：`SensorInfo.schema` 用 `string` vs `Enum` | 影響 base 定義（本包用 string，CpuSensorConfig.v2 用 Enum） | 採 string（含 MIME，無法窮舉）；若只允許原生型別可改 Enum |
| B7 | **`system.reboot/shutdown` 的 `[DeviceCmd]` 字串未核對** | catalog 的 command name 可能不精確 | 對照 system-agent 原始碼確認 |

---

## 8. 附錄：instance → definition 對照（一筆 devicecfg sensor 如何被理解）

以 `devicecfg.json` 的 `cpu_usage`：

```jsonc
{ "Name": "cpu_usage", "SensorGroup": "SYS",
  "Parameters": { "MetricType": "cpu", "MetricName": "usage" },   // ← 對 dtmi:...:Sensor:cpu;1 的 Parameters
  "Report": { "Enabled": true, "Interval": 5000 },                // ← 對 Sensor:base:Report
  "SensorInfo": { "Schema": "double", "DisplayName": "CPU Usage", "Description": "..." } }  // ← 對 Sensor:base:SensorInfo
```

- instance 的 `Parameters` → 由 catalog `sensors[].deviceType="system-monitor"` 找到 device，`name="cpu"` 找到 `dtmi:...:Sensor:cpu;1`，用其 `Parameters` schema 驗證/渲染。
- `Report.transformPipeline` 若有 `{type:"unitconversion", dtmi:"...:Transform:unitconversion;1"}` → 用該 dtmi 的 Interface 渲染參數子表單，值留在 instance。

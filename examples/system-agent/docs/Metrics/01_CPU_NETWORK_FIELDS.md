# Sensor Field Reference — CPU & Network

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **CPU** and **Network** MetricTypes.

---

## Legend

Flag columns:

| Symbol | Changeable Flag | Required Flag |
|--------|------------------|---------------|
| ✅ | Modifiable at runtime / between deployments | Field MUST be present |
| ❌ | Immutable after creation (changing it is equivalent to a new sensor) | Field MAY be omitted |

The **Schema** column gives the JSON value type (`string`, `integer`, `boolean`, `object`, `array`, or a `oneOf` of these). Container objects use `—` for the **Changeable Flag** column (only their children mutate).

---

## CPU Metric

The CPU MetricType does **not** support Explicit list / Auto-detect mode. The schema is identical between v1.0 and v1.1.

### Example

```json
{
  "Name": "cpu_usage",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "cpu",
    "MetricName": "usage"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "CPU usage percentage",
    "DisplayName": "CPU Usage"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form string, e.g., `cpu_usage`, `cpu_load_1m`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique across all sensors. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. CPU sensors conventionally use `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `cpu` | Must equal `cpu` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific CPU metric to collect. | `usage`, `load1`, `load5`, `load15`, `context_switches` | Enum constraint above. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `5000`, `10000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. Must match the underlying MetricName's native type. | `double` (for `usage`, `load1`, `load5`, `load15`); `long` (for `context_switches`). | Enum value above; AND must equal the MetricName's native return type. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### CPU MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `usage` | `double` | % (0–100) |
| `load1` | `double` | — |
| `load5` | `double` | — |
| `load15` | `double` | — |
| `context_switches` | `long` | count |

---

## Network Metric (v1.0 & v1.1)

Single merged table covering both versions. The **Required (v1.0 / v1.1)** column uses a slash notation when the value differs between versions; otherwise a single value applies to both. `n/a` means the field does not exist in that version.

**v1.0 model**: every network sensor must be explicitly bound to a single interface via the required `Parameters.Interface`. No expansion.

**v1.1 model**: supports three resolution modes —

1. **Bound mode** — `Parameters.Interface` set. No expansion.
2. **Explicit list mode** — `Parameters.Interfaces` set to a JSON array or comma-separated string.
3. **Auto-detect mode** — `Parameters.Interface` and `Parameters.Interfaces` both omitted (or `Interfaces` = `[]` / `""`).

**v1.1 priority**: `Interface` **>** `Interfaces` **>** auto-detect. At least one mode must resolve at startup.

### Examples

**Explicit binding (only mode)** — single interface; works in both v1.0 and v1.1:

```json
{
  "Name": "network_eth0_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "eth0"
  },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Total bytes sent on eth0",
    "DisplayName": "Network Bytes Sent (eth0)"
  }
}
```

**Explicit list mode** (v1.1):

```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interfaces": ["eth0", "eth1"]
  },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Network bytes sent",
    "DisplayName": "Network Bytes Sent"
  }
}
```

**Auto-detect mode** (v1.1):

```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interfaces": []
  },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Network bytes sent",
    "DisplayName": "Network Bytes Sent"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required (v1.0 / v1.1) | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|------------------------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. v1.1: after expansion, runtime appends `_<interface>` (e.g., `network_bytes_sent_eth0`). | Free-form, e.g., `network_eth0_bytes_sent` (bound) or `network_bytes_sent` (generic in v1.1). | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64` (after expansion suffix in v1.1); unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Network uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | v1.0: must contain `MetricType`, `MetricName`, `Interface`. v1.1: must contain `MetricType`, `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `network` | Must equal `network` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific network metric. | `bytes_sent`, `bytes_received`, `packets_sent`, `packets_received`, `errors`, `errors_in`, `errors_out` | Enum constraint above. |
| `Parameters.Interface` | string | ✅ | ✅ / ❌ | OS-level interface name. v1.0: required single binding. v1.1: bound mode only — when set, overrides `Interfaces` and disables expansion. | e.g., `eth0`, `en0`, `wlan0`. | Non-empty string. Must resolve to an interface present on the host at startup. v1.1: semantically mutually exclusive with `Interfaces`. |
| `Parameters.Interfaces` | `oneOf: [array<string>, string]` | ✅ | n/a / ❌ | **v1.1 only.** List of interfaces, or empty for auto-detect. | JSON array `["eth0","eth1"]`; CSV `"eth0,eth1"`; `[]`; `""`. | Array of strings or a single string. Empty array/string → auto-detect. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For network: `long`. | Must equal `long`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. v1.1: runtime appends ` (<interface>)` after expansion. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. v1.1: runtime prepends `<interface> ` after expansion. | Any string. | None. |

> Footnote on `Interface` / `Interfaces` in v1.1: the validator should permit any of — only `Interface` set, only `Interfaces` set, or neither set. If both are set, the runtime breaks the tie by priority (`Interface` wins).

---

## DTDL Schema

DTDL v2 Interface that models every `(MetricType, MetricName)` pair documented above as a Telemetry, with validation rules and reusable Enum schemas embedded directly in the DTDL document. Each Telemetry's `schema` matches the `SensorInfo.Schema` value the System Agent emits; each Telemetry carries a `comment` field describing the range / monotonicity rule downstream consumers should enforce. The auto-generation path in `SystemAgentDeviceConfig.Dtdl.AutoGenEnabled` produces a similar Interface at startup, but **without** the embedded Enums and comments — this file is the canonical reference for IoT Plug-and-Play registration, twin schema review, and validator generation.

The standalone Interface file is published alongside this doc as [`01_CPU_NETWORK.dtdl.json`](./01_CPU_NETWORK.dtdl.json). For end-to-end **.NET / C# consumption examples** (parser setup, Enum extraction, Sensor validation, telemetry publishing), see [`01_CPU_NETWORK_USAGE.md`](./01_CPU_NETWORK_USAGE.md).

### Document anatomy

| Section | DTDL key | Purpose |
|---------|----------|---------|
| Reusable Enums | `schemas[]` | `MetricType`, `CpuMetricName`, `NetworkMetricName`, `SensorGroup`, `SensorInfoSchema` — machine-readable enumerations of every constrained string field. |
| Telemetries | `contents[]` | One Telemetry per `(MetricType, MetricName)` pair, each with a `comment` field carrying the validation rule. |
| Semantic types | `@type` array | Byte-count Telemetries carry `["Telemetry", "DataSize"]` + `unit: "byte"` so consumers can auto-format units. |

### Identifier conventions

- **Interface DTMI**: `dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork;1`.
- **Telemetry DTMI**: `dtmi:advantech:EdgeSync:SystemInfo:<TelemetryName>;1` — the `SystemInfo` namespace is shared with `02_MEMORY_DISK_SYSTEM_GPU_FIELDS.md` so consumers can mix metrics from both files without DTMI collisions.
- **Reusable schema DTMI**: `dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:<SchemaName>;1` — scoped to this Interface so additions in other field-reference docs do not clash.
- **Telemetry `name`**: PascalCase derivation of the `(MetricType, MetricName)` pair — `cpu.usage` → `CpuUsage`, `network.errors_in` → `NetworkErrorsIn`.
- **DTDL `schema`**: matches the `SensorInfo.Schema` column in §"Field Table" of each metric type. A schema mismatch between this DTDL and the deployed `devicecfg.json` raises a startup error.

### CPU MetricName → DTMI / DTDL Telemetry mapping

| `MetricType.MetricName` | DTDL `name` | DTDL `schema` | DTMI |
|--------------------------|-------------|---------------|------|
| `cpu.usage` | `CpuUsage` | `double` | `dtmi:advantech:EdgeSync:SystemInfo:CpuUsage;1` |
| `cpu.load1` | `CpuLoad1` | `double` | `dtmi:advantech:EdgeSync:SystemInfo:CpuLoad1;1` |
| `cpu.load5` | `CpuLoad5` | `double` | `dtmi:advantech:EdgeSync:SystemInfo:CpuLoad5;1` |
| `cpu.load15` | `CpuLoad15` | `double` | `dtmi:advantech:EdgeSync:SystemInfo:CpuLoad15;1` |
| `cpu.context_switches` | `CpuContextSwitches` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:CpuContextSwitches;1` |

### Network MetricName → DTMI / DTDL Telemetry mapping

| `MetricType.MetricName` | DTDL `name` | DTDL `schema` | DTMI |
|--------------------------|-------------|---------------|------|
| `network.bytes_sent` | `NetworkBytesSent` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesSent;1` |
| `network.bytes_received` | `NetworkBytesReceived` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:NetworkBytesReceived;1` |
| `network.packets_sent` | `NetworkPacketsSent` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsSent;1` |
| `network.packets_received` | `NetworkPacketsReceived` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:NetworkPacketsReceived;1` |
| `network.errors` | `NetworkErrors` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:NetworkErrors;1` |
| `network.errors_in` | `NetworkErrorsIn` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsIn;1` |
| `network.errors_out` | `NetworkErrorsOut` | `long` | `dtmi:advantech:EdgeSync:SystemInfo:NetworkErrorsOut;1` |

> **Per-interface expansion (v1.1).** When `Parameters.Interfaces` or auto-detect is used, the runtime appends the interface name to the sensor `Name` and to the DTDL `description` / `displayName` so each interface gets its own twin property. The DTMI itself does **not** change — every expanded sensor for `network.bytes_sent` continues to publish against the same Telemetry id. Differentiate by sensor `name` or by `Parameters.Interface` in the message payload.

### Reusable Enum schemas

Five Enum schemas live in the Interface's `schemas[]` array. Consumer code (config validators, code generators, dashboard plugins) can resolve them by DTMI and treat the `enumValues[].enumValue` set as the canonical allow-list.

| Enum DTMI | `valueSchema` | Allowed `enumValue`s | Applied to |
|-----------|---------------|----------------------|-----------|
| `dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:MetricType;1` | `string` | `"cpu"`, `"network"` | `Sensor.Parameters.MetricType` |
| `dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:CpuMetricName;1` | `string` | `"usage"`, `"load1"`, `"load5"`, `"load15"`, `"context_switches"` | `Sensor.Parameters.MetricName` when `MetricType == "cpu"` |
| `dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:NetworkMetricName;1` | `string` | `"bytes_sent"`, `"bytes_received"`, `"packets_sent"`, `"packets_received"`, `"errors"`, `"errors_in"`, `"errors_out"` | `Sensor.Parameters.MetricName` when `MetricType == "network"` |
| `dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:SensorGroup;1` | `string` | `"AI"`, `"AO"`, `"DI"`, `"DO"`, `"TEMP"`, `"PWR"`, `"SYS"` | `Sensor.SensorGroup` |
| `dtmi:advantech:EdgeSync:SystemAgent:CpuNetwork:SensorInfoSchema;1` | `string` | `"double"`, `"long"`, `"integer"`, `"boolean"`, `"string"` | `Sensor.SensorInfo.Schema` (must additionally agree with the per-Telemetry schema column above) |

### Validation rules embedded in DTDL

Each Telemetry's `comment` field carries the validation rule a consumer must apply when ingesting the telemetry. The agent itself does **not** enforce these — invalid hardware readings pass through, so the cloud-side or gateway-side validator owns the check.

| Telemetry | DTDL `comment` (validation rule) |
|-----------|----------------------------------|
| `CpuUsage` | `0 <= v <= 100` (rounded 2 dp); reject `NaN` / `±Inf`; sustained `v > 95` for ≥ 5 min indicates saturation. |
| `CpuLoad1` / `CpuLoad5` / `CpuLoad15` | `v >= 0`; unitless; `v > 2 × nproc` for ≥ 5 min indicates sustained overload. Returns `0.0` on non-Linux. |
| `CpuContextSwitches` | Monotonic counter; `0 <= v <= 2⁶³−1`; `Δ(v) >= 0` between samples; resets only on reboot (detect via `system.boot_time`). Graph as rate. |
| `NetworkBytesSent` / `NetworkBytesReceived` | Monotonic counter; `0 <= v <= 2⁶³−1`; `Δ(v) >= 0`; resets only on reboot. Graph as `rate(...) [bps]`. Carries DTDL semantic type `DataSize` with `unit: "byte"`. |
| `NetworkPacketsSent` / `NetworkPacketsReceived` | Monotonic counter; `0 <= v <= 2⁶³−1`; `Δ(v) >= 0`. Graph as `rate(...) [pps]`. |
| `NetworkErrors` | Monotonic counter; `Δ(v) >= 0`. Sustained non-zero rate on Ethernet is abnormal — investigate cable / NIC / driver. |
| `NetworkErrorsIn` | Monotonic counter; `Δ(v) >= 0`. Non-zero rate typically signals layer-1 problems (cabling, transceiver, FIFO overflow). |
| `NetworkErrorsOut` | Monotonic counter; `Δ(v) >= 0`. Non-zero rate typically signals carrier loss or device-side TX failure. |

> The validator pattern for monotonic counters and bounded gauges is documented in detail in `docs/05_DEVICECFG.md` §21 — apply those recipes against the comment text above.

### Full Interface

> The block below is a copy of [`01_CPU_NETWORK.dtdl.json`](./01_CPU_NETWORK.dtdl.json). Treat the JSON file as the source of truth — this inline copy is for offline-reading convenience.

### Validation checklist

Before deploying this schema:

- [ ] `SensorInfo.Schema` in every `devicecfg.json` Sensor matches the `schema` column of the corresponding Telemetry above. A mismatch raises a startup error.
- [ ] No two Sensors share the same DTDL `name` after v1.1 expansion (the runtime appends `_<interface>` to `Name` but the Telemetry id is shared — that is intended).
- [ ] If `SystemAgentDeviceConfig.Dtdl.AutoGenEnabled = true`, the auto-generated Interface SHOULD be equivalent to this file. Compare DTMI sets after deployment to catch drift.
- [ ] When extending: increment the Interface DTMI version (`;1` → `;2`) on **any** breaking change (Telemetry removal or schema downgrade). Additions can keep `;1` since DTDL v2 treats them as backward-compatible.

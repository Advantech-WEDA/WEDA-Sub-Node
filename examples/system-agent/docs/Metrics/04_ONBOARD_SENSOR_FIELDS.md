# Sensor Field Reference — Onboard Sensors

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **Onboard Sensors** category: `temperature`, `voltage`, and `fanspeed`.

All three are **platform-dependent** and require the host to ship a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver, sensors remain defined but emit no value.

All three follow the same v1.1 resolution modes — Bound / Explicit list / Auto-detect — with `therm`, `voltage` and `fanspeed` as the generic `MetricName`. A generic sensor expands into one sensor per source the driver reports, so a source name never has to be typed into `MetricName`; the v1.0 style (source name in `MetricName`) is still accepted.

---

## Legend

Flag columns:

| Symbol | Changeable Flag | Required Flag |
|--------|------------------|---------------|
| ✅ | Modifiable at runtime / between deployments | Field MUST be present |
| ❌ | Immutable after creation (changing it is equivalent to a new sensor) | Field MAY be omitted |

The **Schema** column gives the JSON value type. Container objects use `—` for the **Changeable Flag** column (only their children mutate).

---

## Temperature Metric (v1.0 & v1.1)

Single merged table covering both versions. The **Required (v1.0 / v1.1)** column uses a slash notation when the value differs between versions; otherwise a single value applies to both. `n/a` means the field does not exist in that version.

### Semantic Difference — `MetricName`

- **v1.0**: `MetricName` is the **hardware sensor source name** (e.g., `cpU-therm`, `gpU-therm`). Case-insensitive matching.
- **v1.1**: `MetricName` is fixed to `"therm"` (recommended). Source identification moves to the independent parameters `Source` / `Sources`. For backward compatibility, v1.1 still accepts `MetricName` set directly to a source name (treated as v1.0 mode).

### v1.1 Resolution Modes

1. **Bound mode** — `MetricName = "therm"`, `Source` set. No expansion.
2. **Explicit list mode** — `MetricName = "therm"`, `Sources` set to a JSON array or comma-separated string.
3. **Auto-detect mode** — `MetricName = "therm"`, `Source` and `Sources` both omitted (or `Sources` = `[]` / `""`).
4. **v1.0-compatible mode** — `MetricName` set directly to a source name; `Source`/`Sources` absent.

**v1.1 priority**: `Source` **>** `Sources` **>** v1.0-compatible `MetricName` **>** auto-detect.

### Examples

**Explicit binding (only mode)** — single source via `Source` (v1.1 syntax):

```json
{
  "Name": "temperature_cpu_therm",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Source": "cpU-therm"
  },
  "Report": { "Enabled": true, "Interval": 1000 },
  "SensorInfo": {
    "Schema": "double",
    "Description": "CPU thermal sensor temperature",
    "DisplayName": "CPU Thermal"
  }
}
```

> v1.0 backward-compatible syntax (still accepted in v1.1): set `MetricName` directly to the source name (e.g., `"MetricName": "cpU-therm"`) and omit `Source`.

**Explicit list mode** (v1.1):

```json
{
  "Name": "temperature",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Sources": ["cpU-therm", "gpU-therm"]
  },
  "Report": { "Enabled": true, "Interval": 1000 },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Temperature sensor",
    "DisplayName": "Temperature"
  }
}
```

**Auto-detect mode** (v1.1):

```json
{
  "Name": "temperature",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Sources": []
  },
  "Report": { "Enabled": true, "Interval": 1000 },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Temperature sensor",
    "DisplayName": "Temperature"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required (v1.0 / v1.1) | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|------------------------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. v1.1: after expansion, runtime appends `_<source>` (e.g., `temperature_cpU-therm`). | Free-form, e.g., `temperature_cpu_therm` (bound) or `temperature` (generic in v1.1). | Non-empty string; `minLength: 1`, `maxLength: 64`; pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Temperature uses `TEMP`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above (the `TemperatureSensor` schema permits all seven). |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `temperature` | Must equal `temperature` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | v1.0: hardware sensor source name. v1.1: recommended fixed value `"therm"` when using `Source`/`Sources`; otherwise the source name (v1.0 compatible). | v1.0: e.g., `cpU-therm`, `gpU-therm` (hardware-dependent). v1.1: `therm`, or any source name (compat). | Non-empty string; `minLength: 1`, `maxLength: 64`. Not enumerated by the schema (free-form `string`). Case-insensitive when matched as a source name. |
| `Parameters.Source` | string | ❌ | n/a / ❌ | **v1.1 only.** Bound mode: single source name. Overrides `Sources` and auto-detect. Used only when `MetricName = "therm"`. | e.g., `cpU-therm`. | Non-empty string; `minLength: 1`, `maxLength: 64`. Allowed only when `MetricName = "therm"` (`allowedOnlyWhen`). Mutually exclusive with `Sources` (`mutexGroup: tempSource`). |
| `Parameters.Sources` | `array<string>` (or CSV string) | ❌ | n/a / ❌ | **v1.1 only.** List of source names, or empty for auto-detect. Used only when `MetricName = "therm"`. | JSON array `["cpU-therm","gpU-therm"]`; CSV `"cpU-therm,gpU-therm"`; `[]`; `""`. | JSON array of strings; a comma-separated / empty string is also accepted (`csvStringAccepted`). Empty → auto-detect. Allowed only when `MetricName = "therm"` (`allowedOnlyWhen`). Mutually exclusive with `Source` (`mutexGroup: tempSource`). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `1000`. | Integer; `1 ≤ value ≤ 300000`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For temperature: `double`. | Must equal `double`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. v1.1: runtime appends ` (<source>)` after expansion. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 512`. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. v1.1: runtime prepends `<source> ` after expansion. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 64`. |

> Schema-enforced bounds and the `Source`/`Sources` mutual exclusivity + `MetricName = "therm"` conditional come from [`devicecfg/TemperatureSensor.dtdl.json`](devicecfg/TemperatureSensor.dtdl.json) (`ConfigConstraint` extension). A v1.0-compat sensor (`MetricName ≠ "therm"`) carrying `Source`/`Sources` is rejected.

> Footnote on `Source` / `Sources` / `MetricName` in v1.1: the validator should accept any of the four modes above. If `Source` and `Sources` are both set, the runtime breaks the tie by priority (`Source` wins). If `MetricName ≠ "therm"` and `Source`/`Sources` are present, the runtime treats `MetricName` as a source name (v1.0 mode) and ignores the others.

### v1.0 vs v1.1 — Temperature Field Summary

| Field | v1.0 | v1.1 |
|------|------|------|
| `Parameters.MetricName` | Sensor source name (variable, hardware-dependent) | `"therm"` (recommended) or source name (v1.0 compat) |
| `Parameters.Source` | ❌ Not supported | Optional (bound mode) |
| `Parameters.Sources` | ❌ Not supported | Optional (list or auto-detect mode) |
| Auto-detect on missing parameters | ❌ Not supported | ✅ Supported |
| Expansion of `Name` / `DisplayName` / `Description` | ❌ Not supported | ✅ Runtime suffixes/prefixes source name |

---

## Voltage Metric (v1.0 & v1.1)

`voltage` uses the same resolution modes as temperature, with `"voltage"` as the generic `MetricName`. The hardware driver reports one reading per rail — for example `Vcore`, `5V`, `12V` and `CMOS Battery` on an ARK-2250L — so one generic sensor cannot carry them all: v1.1 expands it into one sensor per rail.

### Resolution Modes

1. **Bound mode** — `MetricName = "voltage"`, `Source` set to one rail name. No expansion.
2. **Explicit list mode** — `MetricName = "voltage"`, `Sources` set to a JSON array or comma-separated string.
3. **Auto-detect mode** — `MetricName = "voltage"`, `Source` and `Sources` both omitted (or `Sources` = `[]` / `""`). This is the shipped default.
4. **v1.0-compatible mode** — `MetricName` set directly to a rail name (e.g., `"Vcore"`); `Source`/`Sources` absent.

**Priority**: `Source` **>** `Sources` **>** v1.0-compatible `MetricName` **>** auto-detect.

**Expanded names**: the runtime appends `_<rail>` to `Name`, replacing every character outside `[A-Za-z0-9_]` with `_` so the result stays a valid sensor name (e.g., `CMOS Battery` → `_CMOS_Battery`, `+3.3V` → `_3_3V`). If two rails clean up to the same name, only the first is kept. The raw rail name is kept in `Source`, which is what readings are matched by (case-insensitive). Temperature does not clean names, so data already reported under names such as `temperature_cpU-therm` keeps its name.

A generic sensor that could not be expanded (no rails discovered) reports the single reading when the driver reports exactly one rail, and nothing otherwise. A sensor that produces no value logs one warning, and logs again only if it recovers and later fails anew.

### Examples

**Auto-detect mode** (shipped default):

```json
{
  "Name": "voltage",
  "SensorGroup": "PWR",
  "Parameters": {
    "MetricType": "voltage",
    "MetricName": "voltage"
  },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Voltage sensor reading",
    "DisplayName": "Voltage"
  }
}
```

On an ARK-2250L this expands to:

| Sensor | `Source` | Reports |
|--------|----------|---------|
| `voltage` | — | nothing (disabled template, kept for capability reporting) |
| `voltage_Vcore` | `Vcore` | CPU core voltage |
| `voltage_5V` | `5V` | 5 V rail |
| `voltage_12V` | `12V` | 12 V rail |
| `voltage_CMOS_Battery` | `CMOS Battery` | CMOS battery |

**Explicit list mode**:

```json
{
  "Name": "voltage",
  "SensorGroup": "PWR",
  "Parameters": {
    "MetricType": "voltage",
    "MetricName": "voltage",
    "Sources": ["Vcore", "12V"]
  },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Voltage sensor reading",
    "DisplayName": "Voltage"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required (v1.0 / v1.1) | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|------------------------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. v1.1: after expansion, runtime appends `_<rail>` with invalid characters replaced (e.g., `voltage_CMOS_Battery`). | Free-form, e.g., `voltage` (generic) or `voltage_CMOS_Battery` (bound). | Non-empty string; `minLength: 1`, `maxLength: 64`; pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Voltage conventionally uses `PWR`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above (the `VoltageSensor` schema permits all seven). |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `voltage` | Must equal `voltage` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | v1.0: the rail name. v1.1: the generic value `"voltage"`, with the rail chosen by `Source`/`Sources` or auto-detected; a rail name is still accepted (v1.0 compatible). | `voltage`, or a rail name such as `Vcore` (platform-dependent). | Non-empty string; `minLength: 1`, `maxLength: 64`. Not enumerated by the schema (free-form `string`). Case-insensitive when matched as a rail name. |
| `Parameters.Source` | string | ❌ | n/a / ❌ | **v1.1 only.** Bound mode: single rail name. Set by the runtime on each expanded sensor. Used only when `MetricName = "voltage"`. | e.g., `Vcore`. | Non-empty string; `minLength: 1`, `maxLength: 64`. Allowed only when `MetricName = "voltage"` (`allowedOnlyWhen`). Mutually exclusive with `Sources` (`mutexGroup: voltSource`). |
| `Parameters.Sources` | `array<string>` (or CSV string) | ❌ | n/a / ❌ | **v1.1 only.** List of rail names, or empty for auto-detect. Used only when `MetricName = "voltage"`. | JSON array `["Vcore", "12V"]`; CSV string; `[]`; `""`. | JSON array of strings; a comma-separated / empty string is also accepted (`csvStringAccepted`). Empty → auto-detect. Allowed only when `MetricName = "voltage"` (`allowedOnlyWhen`). Mutually exclusive with `Source` (`mutexGroup: voltSource`). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `5000`. | Integer; `1 ≤ value ≤ 300000`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For voltage: `double`. | Must equal `double`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. v1.1: runtime appends ` (<rail>)` after expansion. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 512`. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. v1.1: runtime prepends `<rail> ` after expansion. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 64`. |

> Schema-enforced bounds and the `Source`/`Sources` mutual exclusivity + `MetricName = "voltage"` conditional come from [`devicecfg/VoltageSensor.dtdl.json`](devicecfg/VoltageSensor.dtdl.json) (`ConfigConstraint` extension). A v1.0-compat sensor (`MetricName ≠ "voltage"`) carrying `Source`/`Sources` is rejected.

### Voltage MetricName → Schema Mapping

Every `voltage` sensor maps to `SensorInfo.Schema = "double"`.

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `voltage` (generic, v1.1) | `double` | V |
| a rail name, e.g. `Vcore` (v1.0) | `double` | V |

---

## Fanspeed Metric (v1.0 & v1.1)

`fanspeed` uses the same resolution modes as temperature, with `"fanspeed"` as the generic `MetricName`. The hardware driver reports one reading per fan — for example `CPU` and `System` on a board with two tachometer inputs — so one generic sensor cannot carry them all: v1.1 expands it into one sensor per fan.

### Resolution Modes

1. **Bound mode** — `MetricName = "fanspeed"`, `Source` set to one fan name. No expansion.
2. **Explicit list mode** — `MetricName = "fanspeed"`, `Sources` set to a JSON array or comma-separated string.
3. **Auto-detect mode** — `MetricName = "fanspeed"`, `Source` and `Sources` both omitted (or `Sources` = `[]` / `""`). This is the shipped default.
4. **v1.0-compatible mode** — `MetricName` set directly to a fan name (e.g., `"CPU"`); `Source`/`Sources` absent.

**Priority**: `Source` **>** `Sources` **>** v1.0-compatible `MetricName` **>** auto-detect.

**Expanded names**: the runtime appends `_<fan>` to `Name`, replacing every character outside `[A-Za-z0-9_]` with `_` so the result stays a valid sensor name (e.g., `CMOS Battery` → `_CMOS_Battery`, `+3.3V` → `_3_3V`). If two fans clean up to the same name, only the first is kept. The raw fan name is kept in `Source`, which is what readings are matched by (case-insensitive). Temperature does not clean names, so data already reported under names such as `temperature_cpU-therm` keeps its name.

A generic sensor that could not be expanded (no fans discovered) reports the single reading when the driver reports exactly one fan, and nothing otherwise. A sensor that produces no value logs one warning, and logs again only if it recovers and later fails anew.

### Examples

**Auto-detect mode** (shipped default):

```json
{
  "Name": "fanspeed",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "fanspeed",
    "MetricName": "fanspeed"
  },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Fanspeed sensor reading",
    "DisplayName": "Fan Speed"
  }
}
```

On a board reporting `CPU` and `System` fans this expands to `fanspeed` (disabled template), `fanspeed_CPU` and `fanspeed_System`. A board without fan tachometers (e.g., ARK-2250L) keeps the template as-is and publishes nothing for it.

**Explicit list mode**:

```json
{
  "Name": "fanspeed",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "fanspeed",
    "MetricName": "fanspeed",
    "Sources": ["CPU"]
  },
  "Report": { "Enabled": true, "Interval": 5000 },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Fanspeed sensor reading",
    "DisplayName": "Fan Speed"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required (v1.0 / v1.1) | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|------------------------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. v1.1: after expansion, runtime appends `_<fan>` with invalid characters replaced (e.g., `fanspeed_CPU`). | Free-form, e.g., `fanspeed` (generic) or `fanspeed_CPU` (bound). | Non-empty string; `minLength: 1`, `maxLength: 64`; pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Fanspeed conventionally uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above (the `FanspeedSensor` schema permits all seven). |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `fanspeed` | Must equal `fanspeed` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | v1.0: the fan name. v1.1: the generic value `"fanspeed"`, with the fan chosen by `Source`/`Sources` or auto-detected; a fan name is still accepted (v1.0 compatible). | `fanspeed`, or a fan name such as `CPU` (platform-dependent). | Non-empty string; `minLength: 1`, `maxLength: 64`. Not enumerated by the schema (free-form `string`). Case-insensitive when matched as a fan name. |
| `Parameters.Source` | string | ❌ | n/a / ❌ | **v1.1 only.** Bound mode: single fan name. Set by the runtime on each expanded sensor. Used only when `MetricName = "fanspeed"`. | e.g., `CPU`. | Non-empty string; `minLength: 1`, `maxLength: 64`. Allowed only when `MetricName = "fanspeed"` (`allowedOnlyWhen`). Mutually exclusive with `Sources` (`mutexGroup: fanSource`). |
| `Parameters.Sources` | `array<string>` (or CSV string) | ❌ | n/a / ❌ | **v1.1 only.** List of fan names, or empty for auto-detect. Used only when `MetricName = "fanspeed"`. | JSON array `["CPU"]`; CSV string; `[]`; `""`. | JSON array of strings; a comma-separated / empty string is also accepted (`csvStringAccepted`). Empty → auto-detect. Allowed only when `MetricName = "fanspeed"` (`allowedOnlyWhen`). Mutually exclusive with `Source` (`mutexGroup: fanSource`). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `5000`. | Integer; `1 ≤ value ≤ 300000`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For fanspeed: `double`. | Must equal `double`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. v1.1: runtime appends ` (<fan>)` after expansion. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 512`. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. v1.1: runtime prepends `<fan> ` after expansion. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 64`. |

> Schema-enforced bounds and the `Source`/`Sources` mutual exclusivity + `MetricName = "fanspeed"` conditional come from [`devicecfg/FanspeedSensor.dtdl.json`](devicecfg/FanspeedSensor.dtdl.json) (`ConfigConstraint` extension). A v1.0-compat sensor (`MetricName ≠ "fanspeed"`) carrying `Source`/`Sources` is rejected.

### Fanspeed MetricName → Schema Mapping

Every `fanspeed` sensor maps to `SensorInfo.Schema = "double"`.

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `fanspeed` (generic, v1.1) | `double` | RPM |
| a fan name, e.g. `CPU` (v1.0) | `double` | RPM |

---

## Notes on Changeable Flag Semantics

- ❌ **Immutable fields** (`Name`, `SensorGroup`, `Parameters.MetricType`, `Parameters.MetricName`, `Parameters.Source` / `Parameters.Sources` for v1.1 onboard sensors, `SensorInfo.Schema`): Cannot be modified after creation; changing them is equivalent to defining a different sensor.
- ✅ **Operational fields** (`Report.Enabled`, `Report.Interval`, `SensorInfo.Description`, `SensorInfo.DisplayName`): Safely modifiable at any time.

## Notes on Required Flag Semantics

- Required ✅ → MUST appear in the JSON.
- Container objects (`Parameters`, `Report`, `SensorInfo`) are required even though their own values are objects.
- For v1.1 onboard sensors, the quadruple (`Source`, `Sources`, v1.0-compat `MetricName`, neither) is collectively optional — at least one mode must resolve at runtime.

---

## Platform Dependency

All three onboard sensors require a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver:

- Sensors remain defined in the JSON config (no validation error).
- Reports no value at runtime, and logs one warning per sensor saying so.
- Does not throw a fatal error — the System Agent continues to operate.

The discovery / matching of source names also depends on the driver. Auto-detect mode on an unsupported host yields no expanded sensors.

---

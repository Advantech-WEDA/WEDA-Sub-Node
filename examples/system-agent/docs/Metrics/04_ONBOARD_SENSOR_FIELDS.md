# Sensor Field Reference — Onboard Sensors

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **Onboard Sensors** category: `temperature`, `voltage`, and `fanspeed`.

All three are **platform-dependent** and require the host to ship a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver, sensors remain defined but emit no value.

Of these, only `temperature` differs between v1.0 and v1.1 — v1.1 introduces Explicit list / Auto-detect mode. `voltage` and `fanspeed` are identical between versions.

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
| `Name` | string | ❌ | ✅ | Unique sensor identifier. v1.1: after expansion, runtime appends `_<source>` (e.g., `temperature_cpU-therm`). | Free-form, e.g., `temperature_cpu_therm` (bound) or `temperature` (generic in v1.1). | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64` (after expansion suffix in v1.1); unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Temperature uses `TEMP`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `temperature` | Must equal `temperature` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | v1.0: hardware sensor source name. v1.1: recommended fixed value `"therm"` when using `Source`/`Sources`; otherwise the source name (v1.0 compatible). | v1.0: e.g., `cpU-therm`, `gpU-therm` (hardware-dependent). v1.1: `therm`, or any source name (compat). | Non-empty string. Case-insensitive when matched as a source name. |
| `Parameters.Source` | string | ❌ | n/a / ❌ | **v1.1 only.** Bound mode: single source name. Overrides `Sources` and auto-detect. Used only when `MetricName = "therm"`. | e.g., `cpU-therm`. | Non-empty string. Case-insensitive. Semantically mutually exclusive with `Sources`. |
| `Parameters.Sources` | `oneOf: [array<string>, string]` | ❌ | n/a / ❌ | **v1.1 only.** List of source names, or empty for auto-detect. Used only when `MetricName = "therm"`. | JSON array `["cpU-therm","gpU-therm"]`; CSV `"cpU-therm,gpU-therm"`; `[]`; `""`. | Array of strings or a single string. Empty array/string → auto-detect. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `1000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For temperature: `double`. | Must equal `double`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. v1.1: runtime appends ` (<source>)` after expansion. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. v1.1: runtime prepends `<source> ` after expansion. | Any string. | None. |

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

## Voltage Metric

The `voltage` MetricType has no additional Parameters and behaves identically between v1.0 and v1.1.

### Example

```json
{
  "Name": "voltage",
  "SensorGroup": "PWR",
  "Parameters": {
    "MetricType": "voltage",
    "MetricName": "voltage"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Voltage sensor reading",
    "DisplayName": "Voltage"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `voltage`, `voltage_5v`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Voltage conventionally uses `PWR`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `voltage` | Must equal `voltage` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific voltage metric. | `voltage` | Must equal `voltage` (const). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `5000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For voltage: `double`. | Must equal `double`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### Voltage MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `voltage` | `double` | V |

---

## Fanspeed Metric

The `fanspeed` MetricType has no additional Parameters and behaves identically between v1.0 and v1.1.

### Example

```json
{
  "Name": "fanspeed",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "fanspeed",
    "MetricName": "fanspeed"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Fan speed reading",
    "DisplayName": "Fan Speed"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `fanspeed`, `fan_cpu`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Fanspeed conventionally uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `fanspeed` | Must equal `fanspeed` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific fanspeed metric. | `fanspeed` | Must equal `fanspeed` (const). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `5000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For fanspeed: `double`. | Must equal `double`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### Fanspeed MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `fanspeed` | `double` | RPM |

---

## Notes on Changeable Flag Semantics

- ❌ **Immutable fields** (`Name`, `SensorGroup`, `Parameters.MetricType`, `Parameters.MetricName`, `Parameters.Source` / `Parameters.Sources` for v1.1 temperature, `SensorInfo.Schema`): Cannot be modified after creation; changing them is equivalent to defining a different sensor.
- ✅ **Operational fields** (`Report.Enabled`, `Report.Interval`, `SensorInfo.Description`, `SensorInfo.DisplayName`): Safely modifiable at any time.

## Notes on Required Flag Semantics

- Required ✅ → MUST appear in the JSON.
- Container objects (`Parameters`, `Report`, `SensorInfo`) are required even though their own values are objects.
- For v1.1 temperature, the quadruple (`Source`, `Sources`, v1.0-compat `MetricName`, neither) is collectively optional — at least one mode must resolve at runtime.

---

## Platform Dependency

All three onboard sensors require a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver:

- Sensors remain defined in the JSON config (no validation error).
- Reports no value at runtime.
- Does not throw a fatal error — the System Agent continues to operate.

For temperature specifically, the discovery / matching of source names also depends on the driver. Auto-detect mode on an unsupported host yields no expanded sensors.

---

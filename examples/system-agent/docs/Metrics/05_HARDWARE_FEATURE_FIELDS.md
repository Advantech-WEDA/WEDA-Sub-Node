# Sensor Field Reference — Hardware Features

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **Hardware Features** category: `gpio`, `watchdog`, and `thermalprotection`.

All three are **platform-dependent** and require the host to ship a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver, sensors remain defined but emit no value.

Of these, only `gpio` (specifically `MetricName = pinState`) differs between v1.0 and v1.1 — v1.1 introduces Sensor Expansion. The `gpio` MetricName `isSupported`, plus `watchdog` and `thermalprotection`, are identical between versions.

---

## Legend

Flag columns:

| Symbol | Changeable Flag | Required Flag |
|--------|------------------|---------------|
| ✅ | Modifiable at runtime / between deployments | Field MUST be present |
| ❌ | Immutable after creation (changing it is equivalent to a new sensor) | Field MAY be omitted |

The **Schema** column gives the JSON value type. Container objects use `—` for the **Changeable Flag** column (only their children mutate).

---

## GPIO Metric — Overview

The `gpio` MetricType supports two MetricNames with different parameter shapes. Each is documented as its own sub-section below.

| MetricName | Returns | Additional Parameters (v1.0) | Additional Parameters (v1.1) | Expansion (v1.1) |
|------------|---------|------------------------------|------------------------------|------------------|
| `isSupported` | boolean | None | None | ❌ Not supported |
| `pinState` | integer (0=Low, 1=High) | `PinId` (required) | `PinId` (single) or `PinIds` (list / auto-detect) | ✅ Supported |

---

## GPIO Metric — `MetricName = isSupported`

Identical between v1.0 and v1.1. No expansion.

### Example

```json
{
  "Name": "gpio_isSupported",
  "SensorGroup": "DI",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "isSupported"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "boolean",
    "Description": "GPIO support status",
    "DisplayName": "GPIO Supported"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `gpio_isSupported`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. GPIO conventionally uses `DI` (or `SYS` for the support flag). | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `gpio` | Must equal `gpio` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific GPIO query. | `isSupported` | Must equal `isSupported` (const) for this sub-section. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `6000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For `isSupported`: `boolean`. | Must equal `boolean`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

---

## GPIO Metric — `MetricName = pinState` (v1.0 & v1.1)

Single merged table covering both versions. The **Required (v1.0 / v1.1)** column uses a slash notation when the value differs between versions; otherwise a single value applies to both. `n/a` means the field does not exist in that version.

### v1.1 Resolution Modes (`pinState` only)

1. **Bound mode** — `PinId` set. No expansion.
2. **Explicit list mode** — `PinIds` set to a JSON array or comma-separated string.
3. **Auto-detect mode** — `PinId` and `PinIds` both omitted (or `PinIds` = `[]` / `""`).

**v1.1 priority**: `PinId` **>** `PinIds` **>** auto-detect. At least one mode must resolve at startup.

### Examples

**Explicit binding (only mode)** — single pin via `PinId`; works in both v1.0 and v1.1:

```json
{
  "Name": "gpio_pin_UIO_GPIO2",
  "SensorGroup": "DI",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinId": "UIO_GPIO2"
  },
  "Report": { "Enabled": true, "Interval": 6000 },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state for UIO_GPIO2",
    "DisplayName": "UIO_GPIO2 State"
  }
}
```

**Explicit list mode** (v1.1):

```json
{
  "Name": "gpio_pinState",
  "SensorGroup": "DI",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinIds": ["DI_0", "DI_1"]
  },
  "Report": { "Enabled": true, "Interval": 6000 },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "Pin State"
  }
}
```

**Auto-detect mode** (v1.1):

```json
{
  "Name": "gpio_pinState",
  "SensorGroup": "DI",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinIds": []
  },
  "Report": { "Enabled": true, "Interval": 6000 },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "Pin State"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required (v1.0 / v1.1) | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|------------------------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. v1.1: after expansion, runtime appends `_<pin>` (e.g., `gpio_pin_DI_0`). | Free-form, e.g., `gpio_pin_UIO_GPIO2` (bound) or `gpio_pinState` (generic in v1.1). | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64` (after expansion suffix in v1.1); unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. `pinState` conventionally uses `DI`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | v1.0: must contain `MetricType`, `MetricName`, `PinId`. v1.1: must contain `MetricType`, `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `gpio` | Must equal `gpio` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific GPIO query. | `pinState` | Must equal `pinState` (const) for this sub-section. |
| `Parameters.PinId` | string | ❌ | ✅ / ❌ | GPIO pin name or numeric index (single binding). v1.0: required. v1.1: bound mode only — overrides `PinIds` when present. | e.g., `UIO_GPIO2`, `DI_0`, `4`. | Non-empty string. Must resolve to a pin present on the host at startup. v1.1: semantically mutually exclusive with `PinIds`. |
| `Parameters.PinIds` | `oneOf: [array<string>, string]` | ❌ | n/a / ❌ | **v1.1 only.** List of pin names/indices, or empty for auto-detect. | JSON array `[4, 17]` or `["DI_0","DI_1"]`; CSV `"DI_0,DI_1"`; `[]`; `""`. | Array of strings or a single string. Empty array/string → auto-detect. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `6000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For `pinState`: `integer` (0=Low, 1=High). | Must equal `integer`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. v1.1: runtime appends ` (<pin>)` after expansion. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. v1.1: runtime prepends `<pin> ` after expansion. | Any string. | None. |

> Footnote on `PinId` / `PinIds` in v1.1: the validator should accept any of — only `PinId` set, only `PinIds` set, or neither set. If both are set, the runtime breaks the tie by priority (`PinId` wins).

### v1.0 vs v1.1 — GPIO Field Summary

| Field | v1.0 | v1.1 |
|------|------|------|
| `Parameters.PinId` (with `pinState`) | **Required** (single, bound) | Optional (bound mode only) |
| `Parameters.PinIds` (with `pinState`) | ❌ Not supported | Optional (list or auto-detect mode) |
| Auto-detect on missing parameters | ❌ Not supported | ✅ Supported (omit both / empty array / empty string) |
| Expansion of `Name` / `DisplayName` / `Description` | ❌ Not supported | ✅ Runtime suffixes/prefixes pin identifier |
| `MetricName = isSupported` | Unchanged | Unchanged |

---

## Watchdog Metric

The `watchdog` MetricType has no additional Parameters and behaves identically between v1.0 and v1.1. No expansion.

### Example

```json
{
  "Name": "watchdog_isSupported",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "watchdog",
    "MetricName": "isSupported"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "boolean",
    "Description": "Watchdog support status",
    "DisplayName": "Watchdog Supported"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `watchdog_isSupported`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Watchdog conventionally uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `watchdog` | Must equal `watchdog` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific watchdog query. | `isSupported` | Must equal `isSupported` (const). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `6000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For watchdog: `boolean`. | Must equal `boolean`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### Watchdog MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Description |
|------------|---------------------|-------------|
| `isSupported` | `boolean` | Whether watchdog timer is supported on the platform |

---

## Thermal Protection Metric

The `thermalprotection` MetricType has no additional Parameters and behaves identically between v1.0 and v1.1. No expansion.

### Example

```json
{
  "Name": "thermalprotection_isSupported",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "thermalprotection",
    "MetricName": "isSupported"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "boolean",
    "Description": "Thermal protection support status",
    "DisplayName": "Thermal Protection Supported"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `thermalprotection_isSupported`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Thermal protection conventionally uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `thermalprotection` | Must equal `thermalprotection` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific thermal-protection query. | `isSupported` | Must equal `isSupported` (const). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `6000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For thermal protection: `boolean`. | Must equal `boolean`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### Thermal Protection MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Description |
|------------|---------------------|-------------|
| `isSupported` | `boolean` | Whether thermal protection is supported on the platform |

---

## Notes on Changeable Flag Semantics

- ❌ **Immutable fields** (`Name`, `SensorGroup`, `Parameters.MetricType`, `Parameters.MetricName`, `Parameters.PinId` / `Parameters.PinIds` for v1.1 gpio `pinState`, `SensorInfo.Schema`): Cannot be modified after creation; changing them is equivalent to defining a different sensor.
- ✅ **Operational fields** (`Report.Enabled`, `Report.Interval`, `SensorInfo.Description`, `SensorInfo.DisplayName`): Safely modifiable at any time.

## Notes on Required Flag Semantics

- Required ✅ → MUST appear in the JSON.
- Container objects (`Parameters`, `Report`, `SensorInfo`) are required even though their own values are objects.
- For v1.1 gpio `pinState`, the trio (`PinId`, `PinIds`, neither) is collectively optional — at least one mode must resolve at runtime.
- `watchdog` and `thermalprotection` accept exactly one MetricName (`isSupported`); they have no optional Parameters.

---

## Platform Dependency

All three hardware-feature MetricTypes require a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver:

- Sensors remain defined in the JSON config (no validation error).
- Reports no value at runtime; `isSupported` returns `false`.
- Does not throw a fatal error — the System Agent continues to operate.

For `gpio` specifically, auto-detect mode (v1.1) on an unsupported host yields no expanded sensors; the generic sensor is kept as-is with a warning log.

# Sensor Field Reference — Hardware Information

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **Hardware Information** category — currently a single MetricType: `hwinfo`.

The `hwinfo` MetricType is **platform-dependent** and requires the host to ship a hardware-platform driver (e.g., Advantech SUSI Driver). On hosts without the driver, sensors remain defined but emit no value.

`hwinfo` behaves **identically between v1.0 and v1.1** and does not support Explicit list / Auto-detect mode.

---

## Legend

Flag columns:

| Symbol | Changeable Flag | Required Flag |
|--------|------------------|---------------|
| ✅ | Modifiable at runtime / between deployments | Field MUST be present |
| ❌ | Immutable after creation (changing it is equivalent to a new sensor) | Field MAY be omitted |

The **Schema** column gives the JSON value type. Container objects use `—` for the **Changeable Flag** column (only their children mutate).

---

## hwinfo Metric

The `hwinfo` MetricType has no additional Parameters. All MetricNames return strings sourced from the platform driver.

### Example

```json
{
  "Name": "hwinfo_motherboard",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "hwinfo",
    "MetricName": "motherboardname"
  },
  "Report": {
    "Enabled": true,
    "Interval": 60000
  },
  "SensorInfo": {
    "Schema": "string",
    "Description": "Motherboard name",
    "DisplayName": "Motherboard"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `hwinfo_motherboard`, `hwinfo_biosrevision`. | Non-empty string; `minLength: 1`, `maxLength: 64`; pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; unique across all sensors. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. `hwinfo` sensors conventionally use `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above (the `HwinfoSensor` schema permits all seven). |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `hwinfo` | Must equal `hwinfo` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific hwinfo field to read — the platform identity field name. | Free-form non-empty string, e.g., `motherboard`, `motherboard_serial`, `bios_revision`, `bios_version`, `ec_revision`, `driver_version`. The set is platform-dependent. | Non-empty string; `minLength: 1`, `maxLength: 64`. Not enumerated by the schema (free-form `string`). |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. Hardware info is static at runtime; long intervals (e.g., 60 s) are typical. | Positive integer, e.g., `60000`. | Integer; `1 ≤ value ≤ 300000`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For `hwinfo`: `string` (all MetricNames). | Must equal `string`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 512`. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 64`. |

> Schema-enforced bounds above come from [`devicecfg/HwinfoSensor.dtdl.json`](devicecfg/HwinfoSensor.dtdl.json) (`ConfigConstraint` extension). `MetricName` is a free-form `string` in the schema — the field set is platform-dependent, so it is not constrained to an enum.

### hwinfo MetricName → Schema Mapping

`MetricName` is a free-form `string` (platform-dependent); **every** `hwinfo` MetricName maps to `SensorInfo.Schema = "string"`. The rows below are representative identity fields, not an exhaustive enum.

| MetricName (example) | `SensorInfo.Schema` | Description |
|----------------------|---------------------|-------------|
| `motherboard` | `string` | Motherboard name |
| `motherboard_serial` | `string` | Motherboard serial number |
| `manufacturer` | `string` | Manufacturer name |
| `bios_revision` | `string` | BIOS revision |
| `bios_version` | `string` | BIOS version |
| `ec_revision` | `string` | Embedded controller revision |
| `driver_version` | `string` | Platform (SUSI) driver version |

---

## Notes on Changeable Flag Semantics

- ❌ **Immutable fields** (`Name`, `SensorGroup`, `Parameters.MetricType`, `Parameters.MetricName`, `SensorInfo.Schema`): Cannot be modified after creation; changing them is equivalent to defining a different sensor.
- ✅ **Operational fields** (`Report.Enabled`, `Report.Interval`, `SensorInfo.Description`, `SensorInfo.DisplayName`): Safely modifiable at any time.

## Notes on Required Flag Semantics

- Required ✅ → MUST appear in the JSON.
- Container objects (`Parameters`, `Report`, `SensorInfo`) are required even though their own values are objects.
- All `hwinfo` fields are required; the type has no optional Parameters.

---

## Platform Dependency

`hwinfo` reads from a hardware-platform driver (e.g., Advantech SUSI Driver). If the driver is not installed or the host is not a supported platform, the sensor:

- Remains defined in the JSON config (no validation error).
- Reports no value at runtime.
- Does not throw a fatal error — the System Agent continues to operate.

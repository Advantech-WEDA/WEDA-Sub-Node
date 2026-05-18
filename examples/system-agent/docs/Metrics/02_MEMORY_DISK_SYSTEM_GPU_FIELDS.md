# Sensor Field Reference — Memory · Disk · System · GPU

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **Memory**, **Disk**, **System**, and **GPU** MetricTypes — the remaining System-Resources metrics after [CPU & Network](01_CPU_NETWORK_FIELDS.md).

All four metric types are platform-portable (Linux / Windows / macOS) and behave **identically between v1.0 and v1.1**. None of them support Explicit list / Auto-detect mode.

---

## Legend

Flag columns:

| Symbol | Changeable Flag | Required Flag |
|--------|------------------|---------------|
| ✅ | Modifiable at runtime / between deployments | Field MUST be present |
| ❌ | Immutable after creation (changing it is equivalent to a new sensor) | Field MAY be omitted |

The **Schema** column gives the JSON value type. Container objects use `—` for the **Changeable Flag** column (only their children mutate).

---

## Memory Metric

The Memory MetricType has no additional Parameters.

### Example

```json
{
  "Name": "memory_total",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "memory",
    "MetricName": "total"
  },
  "Report": {
    "Enabled": true,
    "Interval": 10000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Total memory in bytes",
    "DisplayName": "Memory Total"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form string, e.g., `memory_total`, `memory_used`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique across all sensors. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Memory sensors conventionally use `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `memory` | Must equal `memory` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific memory metric to collect. | `total`, `available`, `used`, `free`, `cached`, `buffers`, `swap_total`, `swap_free` | Enum constraint above. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `10000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For memory: `long` (all MetricNames). | Must equal `long`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### Memory MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `total` | `long` | bytes |
| `available` | `long` | bytes |
| `used` | `long` | bytes |
| `free` | `long` | bytes |
| `cached` | `long` | bytes |
| `buffers` | `long` | bytes |
| `swap_total` | `long` | bytes |
| `swap_free` | `long` | bytes |

---

## Disk Metric

The Disk MetricType requires the additional parameter `Parameters.MountPoint`. No expansion.

### Example

```json
{
  "Name": "disk_root_usage_percent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "disk",
    "MetricName": "usage_percent",
    "MountPoint": "/"
  },
  "Report": {
    "Enabled": true,
    "Interval": 30000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Disk usage percentage for root volume",
    "DisplayName": "Disk Usage (root)"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `disk_root_total`, `disk_root_usage_percent`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. Disk uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType`, `MetricName`, `MountPoint`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `disk` | Must equal `disk` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific disk metric to collect. | `total`, `available`, `free`, `used`, `usage_percent`, `reads_completed`, `writes_completed`, `read_bytes`, `written_bytes` | Enum constraint above. |
| `Parameters.MountPoint` | string | ❌ | ✅ | OS-level mount point to monitor. | e.g., `/`, `/home`, `C:\`. | Non-empty string. Must resolve to a mount point present on the host at startup. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `30000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. Must match the MetricName's native type. | `double` (for `usage_percent`); `long` (all other MetricNames). | Must equal the MetricName's native return type. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### Disk MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `total` | `long` | bytes |
| `available` | `long` | bytes |
| `free` | `long` | bytes |
| `used` | `long` | bytes |
| `usage_percent` | `double` | % (0–100) |
| `reads_completed` | `long` | count |
| `writes_completed` | `long` | count |
| `read_bytes` | `long` | bytes |
| `written_bytes` | `long` | bytes |

---

## System Metric

The System MetricType has no additional Parameters. Exposes OS-level system counters (time, file descriptors, processes, interrupts).

### Example

```json
{
  "Name": "system_time",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "system",
    "MetricName": "time"
  },
  "Report": {
    "Enabled": true,
    "Interval": 60000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Current system time (Unix timestamp)",
    "DisplayName": "System Time"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `system_time`, `system_boot_time`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. System uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `system` | Must equal `system` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific system metric. | `time`, `timex_offset`, `boot_time`, `filefd_allocated`, `filefd_maximum`, `procs_running`, `procs_blocked`, `intr_total` | Enum constraint above. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `60000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. Must match the MetricName's native type. | `long` (time, boot_time, filefd_allocated, filefd_maximum, intr_total); `double` (timex_offset); `integer` (procs_running, procs_blocked). | Must equal the MetricName's native return type. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### System MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `time` | `long` | Unix timestamp (seconds) |
| `timex_offset` | `double` | seconds |
| `boot_time` | `long` | Unix timestamp (seconds) |
| `filefd_allocated` | `long` | count |
| `filefd_maximum` | `long` | count |
| `procs_running` | `integer` | count |
| `procs_blocked` | `integer` | count |
| `intr_total` | `long` | count |

---

## GPU Metric

The GPU MetricType uses the NVIDIA NVML library and requires the NVIDIA driver to be installed on the host. On hosts without a supported GPU/driver, the sensor remains defined but emits no value.

### Example

```json
{
  "Name": "gpu_utilization",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "gpu",
    "MetricName": "utilization"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPU utilization percentage",
    "DisplayName": "GPU Utilization"
  }
}
```

### Field Table

| Hierarchy Key Name | Schema | Changeable | Required | Description | Allowed Values | Validation Rule |
|--------------------|--------|------------|----------|-------------|----------------|-----------------|
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `gpu_utilization`. | Pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; `maxLength: 64`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. GPU uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above. |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `gpu` | Must equal `gpu` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific GPU metric. | `utilization` | Enum constraint above. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `5000`. | `> 0`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For GPU: `integer`. | Must equal `integer`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | None. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | None. |

### GPU MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `utilization` | `integer` | % (0–100) |

---

## Notes on Changeable Flag Semantics

- ❌ **Immutable fields** (`Name`, `SensorGroup`, `Parameters.MetricType`, `Parameters.MetricName`, `Parameters.MountPoint` for disk, `SensorInfo.Schema`): Cannot be modified after creation; changing them is equivalent to defining a different sensor.
- ✅ **Operational fields** (`Report.Enabled`, `Report.Interval`, `SensorInfo.Description`, `SensorInfo.DisplayName`): Safely modifiable at any time.

## Notes on Required Flag Semantics

- Required ✅ → MUST appear in the JSON.
- Container objects (`Parameters`, `Report`, `SensorInfo`) are required even though their own values are objects.
- `Parameters.MountPoint` is required for every disk sensor; there is no auto-detect mode for disks.

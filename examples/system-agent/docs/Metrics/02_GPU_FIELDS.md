# Sensor Field Reference — GPU

This wiki page enumerates every JSON field of a Sensor entry in `devicecfg` of WEDA Node System-Agent for the **GPU** MetricType — the sole member of the **GPU Resource** category (Interface DTMI `dtmi:advantech:EdgeSync:SystemAgent:GpuResource;1`).

GPU uses the NVIDIA NVML library and requires the NVIDIA driver to be installed on the host. On hosts without a supported GPU/driver, the sensor remains defined but emits no value (suppressed — never zero, per the SIL2 fail-safe contract).

The GPU MetricType behaves **identically between v1.0 and v1.1**. It does not support Explicit-list / Auto-detect mode.

---

## Legend

| Symbol | Changeable Flag | Required Flag |
|--------|------------------|---------------|
| ✅ | Modifiable at runtime / between deployments | Field MUST be present |
| ❌ | Immutable after creation (changing it is equivalent to a new sensor) | Field MAY be omitted |

The **Schema** column gives the JSON value type. Container objects use `—` for the **Changeable Flag** column (only their children mutate).

---

## GPU Metric

The GPU MetricType has no additional Parameters.

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
| `Name` | string | ❌ | ✅ | Unique sensor identifier. | Free-form, e.g., `gpu_utilization`. | Non-empty string; `minLength: 1`, `maxLength: 64`; pattern `^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$`; unique. |
| `SensorGroup` | string | ❌ | ✅ | Logical grouping. GPU uses `SYS`. | `AI`, `AO`, `DI`, `DO`, `TEMP`, `PWR`, `SYS` | Enum constraint above (the `GpuSensor` schema permits all seven). |
| `Parameters` | object | — | ✅ | Container for metric routing parameters. | — | Must contain `MetricType` and `MetricName`. |
| `Parameters.MetricType` | string | ❌ | ✅ | Metric type discriminator. | `gpu` | Must equal `gpu` (const). |
| `Parameters.MetricName` | string | ❌ | ✅ | Specific GPU metric. | `utilization` | Enum constraint above. |
| `Report` | object | — | ✅ | Container for periodic reporting settings. | — | Must contain `Enabled` and `Interval`. |
| `Report.Enabled` | boolean | ✅ | ✅ | Enable/disable periodic reporting. | `true`, `false` | Boolean. |
| `Report.Interval` | integer | ✅ | ✅ | Reporting period in milliseconds. | Positive integer, e.g., `5000`. | Integer; `1 ≤ value ≤ 300000`. |
| `SensorInfo` | object | — | ✅ | Container for sensor metadata. | — | Must contain `Schema`, `Description`, `DisplayName`. |
| `SensorInfo.Schema` | string | ❌ | ✅ | Expected return data type. | For GPU: `integer`. | Must equal `integer`. |
| `SensorInfo.Description` | string | ✅ | ✅ | Human-readable description. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 512`. |
| `SensorInfo.DisplayName` | string | ✅ | ✅ | Human-readable display name. | Any string. | Non-empty string; `minLength: 1`, `maxLength: 64`. |

> Schema-enforced bounds above come from [`devicecfg/GpuSensor.dtdl.json`](devicecfg/GpuSensor.dtdl.json) (`ConfigConstraint` extension).

### GPU MetricName → Schema Mapping

| MetricName | `SensorInfo.Schema` | Unit |
|------------|---------------------|------|
| `utilization` | `integer` | % (0–100) |

---

## Notes on Changeable Flag Semantics

- ❌ **Immutable fields** (`Name`, `SensorGroup`, `Parameters.MetricType`, `Parameters.MetricName`, `SensorInfo.Schema`): Cannot be modified after creation.
- ✅ **Operational fields** (`Report.Enabled`, `Report.Interval`, `SensorInfo.Description`, `SensorInfo.DisplayName`): Safely modifiable at any time.

## Notes on Required Flag Semantics

- Required ✅ → MUST appear in the JSON.
- Container objects (`Parameters`, `Report`, `SensorInfo`) are required even though their own values are objects.
- GPU has no optional Parameters; the type accepts exactly one MetricName (`utilization`) in v1.x.

## Platform Dependency

- Requires NVIDIA drivers and `libnvidia-ml.so` to be present on the host.
- On non-NVIDIA hosts, the sensor remains defined in the JSON config (no validation error) and `health.is_healthy` reflects the unsupported state. No telemetry is published — the agent never publishes a zero / fallback value.

---

## Related

- [`02_GPU_USAGE.md`](02_GPU_USAGE.md) — .NET / C# consumption guide
- [`02_GPU_RESOURCE.dtdl.json`](02_GPU_RESOURCE.dtdl.json) — the DTDL v2 Interface
- [`01_SYS_RES_CPU_NETWORK_FIELDS.md`](01_SYS_RES_CPU_NETWORK_FIELDS.md), [`01_SYS_RES_MEMORY_DISK_FIELDS.md`](01_SYS_RES_MEMORY_DISK_FIELDS.md) — sibling System Resource categories

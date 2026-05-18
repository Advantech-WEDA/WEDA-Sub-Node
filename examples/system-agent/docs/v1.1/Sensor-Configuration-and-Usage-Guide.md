# Sensor Configuration Quick Reference — v1.1 Differences

> This document only covers changes between v1.1 and v1.0. For full field descriptions, see [v1.0 Sensor-Configuration-and-Usage-Guide](../v1.0/Sensor-Configuration-and-Usage-Guide.md).

---

## Sensor Field Modification Rules

### Modifiable Fields

| Field | Applicable Types | Allowed Values |
|------|-----------------|----------------|
| `Report.Enabled` | All | `true` / `false` |
| `Report.Interval` | All | Positive integer (milliseconds) |
| `SensorInfo.Description` | All | Any string |
| `SensorInfo.DisplayName` | All | Any string |
| `Parameters.MountPoint` | MetricType: `disk` + any MetricName | String, e.g., `/`, `/home` |
| `Parameters.Interface` | MetricType: `network` + any MetricName | String, e.g., `eth0` |
| `Parameters.Interfaces` | MetricType: `network` + any MetricName | Array, e.g., `[]`, `["eth0", "eth1"]` |
| `Parameters.PinId` | MetricType: `gpio` + MetricName: `pinState` | String, e.g., `gpio4`, `4` (name or numeric index) |
| `Parameters.PinIds` | MetricType: `gpio` + MetricName: `pinState` | Array, e.g., `[]`, `[4, 17, 27]`, `["gpio4", "gpio17"]` (name or numeric index) |
| `Parameters.Source` | MetricType: `temperature` + MetricName: `therm` | String, e.g., `cpu-therm` |
| `Parameters.Sources` | MetricType: `temperature` + MetricName: `therm` | Array, e.g., `[]`, `["cpu-therm", "gpu-therm"]` |

### Non-modifiable Fields

| Field | Description |
|------|------|
| `Name` | Unique sensor identifier, cannot be changed after creation |
| `SensorGroup` | Sensor group (`AI`/`AO`/`DI`/`DO`/`TEMP`/`PWR`/`SYS`) |
| `SensorInfo.Schema` | Data type (`double`/`long`/`integer`/`boolean`/`string`) |
| `Parameters.MetricType` | Metric type (`cpu`/`memory`/`disk`/`network`/`gpu`/`system`/`hwinfo`/`temperature`/`voltage`/`fanspeed`/`gpio`/`watchdog`/`thermalprotection`) |
| `Parameters.MetricName` | Metric name (fixed enum values per MetricType) |

---

## Additional Parameters (v1.1 Updates)

v1.1 adds list parameters for Explicit list / Auto-detect mode. Existing single parameters remain valid (Bound mode).

| MetricType + MetricName | v1.0 Parameter | v1.1 Addition | Description |
|------------------------|-----------|----------|------|
| `disk` + any | `MountPoint` (required) | — | No change |
| `network` + any | `Interface` (single) | `Interfaces` (array, comma-separated, or `[]` for auto-detect) | Expands to multiple network interface sensors |
| `gpio` + `pinState` | `PinId` (single) | `PinIds` (array, comma-separated, or `[]` for auto-detect) | Expands to multiple GPIO pin sensors |
| `temperature` + `therm` | `MetricName` (source name) | `Source` (single) / `Sources` (array, comma-separated, or `[]` for auto-detect) | `MetricName` fixed to `"therm"`, source identification via `Source`/`Sources` |

> **Note**: `[]` (empty array), `""` (empty string), and omitting the parameter all trigger auto-detect.
>
> **Format**: List parameters support JSON arrays (e.g., `["eth0", "eth1"]`) or comma-separated strings (e.g., `"eth0,eth1"`).

> For naming rules and edge cases, see [METRIC-TYPES v1.1](METRIC-TYPES.md#naming-rules-after-expansion).

---

## Examples of Changes

### Onboard Sensors — Temperature (v1.1)

**v1.0 syntax** (still compatible):
```json
{
  "Name": "temperature_cpu_therm",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpu-therm"
  }
}
```

**v1.1 syntax** (bound to single source):
```json
{
  "Name": "temperature_cpu_therm",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Source": "cpu-therm"
  }
}
```

**v1.1 syntax** (auto-detect all sources):
```json
{
  "Name": "temperature",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Sources": []
  },
  "Report": {
    "Enabled": true,
    "Interval": 1000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "Temperature sensor",
    "DisplayName": "Temperature"
  }
}
```

### Network Traffic — Auto-detect All Interfaces (v1.1 new)

```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interfaces": []
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Total bytes sent",
    "DisplayName": "Network Bytes Sent"
  }
}
```

### GPIO Pins — Auto-detect All Pins (v1.1 new)

```json
{
  "Name": "gpio_pinState",
  "SensorGroup": "DI",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinIds": []
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "GPIO Pin State"
  }
}
```

---

## Unchanged Parts

The following remain identical to v1.0. See [v1.0 documentation](../v1.0/Sensor-Configuration-and-Usage-Guide.md):

- Field overview table (which fields are modifiable)
- SensorGroup enum values
- Supported Schema values
- Supported MetricType list
- Configuration for `cpu`, `memory`, `disk`, `gpu`, `system`, `hwinfo`, `voltage`, `fanspeed`, `watchdog`, `thermalprotection`


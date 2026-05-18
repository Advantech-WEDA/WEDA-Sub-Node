# MetricType Configuration Guide (v1.1)

> This document describes the **Explicit list / Auto-detect** sensor resolution modes added in v1.1.
> For MetricType categories, Sensor configuration structure, and safety checks from v1.0, see [METRIC-TYPES v1.0](../v1.0/METRIC-TYPES_en.md).
> For Sensor configuration field quick reference and v1.1 parameter changes, see [Sensor Configuration Quick Reference v1.1](Sensor-Configuration-and-Usage-Guide_en.md).

---

## v1.1 New Feature: Explicit list / Auto-detect Mode

In v1.0, each Sensor must be explicitly bound to a specific resource (e.g., specifying `Interface`, `PinId`, `MetricName`) — this is **Bound mode**. v1.1 adds **Explicit list mode** and **Auto-detect mode**, allowing definition of "generic Sensors" that are resolved into per-resource Sensors at system startup.

### Core Concepts

| Concept | Description |
|------|------|
| **Generic Sensor** | A Sensor definition without specific resource identifier parameters |
| **Auto-detect Mode** | No list provided (or set to empty array `[]` / empty string `""`), system automatically discovers all available resources and expands |
| **Explicit List Mode** | Specify a subset of resources to expand via JSON array or comma-separated string |
| **Bound Sensor** | A Sensor with a single resource parameter already specified (e.g., `Interface`), will not be expanded (same as v1.0 behavior) |

### Priority Order

```
Bound single resource parameter (v1.0 mode) > Explicit list parameter (e.g., Interfaces) > Auto-detect
```

---

## MetricTypes Supporting Explicit list / Auto-detect Mode

Only the following three MetricTypes support Explicit list / Auto-detect mode. All other MetricTypes (`cpu`, `memory`, `disk`, `system`, `gpu`, `hwinfo`, `voltage`, `fanspeed`, `watchdog`, `thermalprotection`, `health`) behave identically to v1.0.

| MetricType | Single Resource Param (v1.0) | Single Resource Param (v1.1) | List Param (v1.1) | Auto-detect Trigger (v1.1) |
|------------|---------------------|---------------------|------------------|----------------|
| `network` | `Interface` | `Interface` | `Interfaces` (array or comma-separated) | Neither `Interface` nor `Interfaces` is set |
| `gpio` (`pinState`) | `PinId` | `PinId` | `PinIds` (array or comma-separated) | Neither `PinId` nor `PinIds` is set |
| `temperature` | `MetricName` (doubles as source name) | `Source` | `Sources` (array or comma-separated) | Neither `Source` nor `Sources` nor `MetricName` is set |

> **temperature v1.0 → v1.1 change**: In v1.0, `MetricName` served as both "metric name" and "sensor source identifier". v1.1 separates source identification into independent `Source` / `Sources` parameters, and `MetricName` reverts to a fixed metric name (recommended: `"therm"`). v1.0 configurations (only setting `MetricName`) remain compatible.

---

## 1. network (Explicit list / Auto-detect mode)

### Auto-detect Mode (no list)

Omit the `Interface` parameter; the system automatically detects all network interfaces at startup and expands.

**Configuration Example**:
```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Network bytes sent",
    "DisplayName": "Network bytes sent"
  }
}
```

If the system detects `eth0` and `wlan0`, it auto-expands to:

| Expanded Name | Interface | DisplayName |
|-------------|-----------|-------------|
| `network_bytes_sent_eth0` | `eth0` | `eth0 Network bytes sent` |
| `network_bytes_sent_wlan0` | `wlan0` | `wlan0 Network bytes sent` |

### Explicit List Mode (with list)

Use the `Interfaces` array or comma-separated string to specify a subset of interfaces to monitor:

```json
{
  "Name": "network_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interfaces": ["eth0", "eth1"]
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Network bytes sent",
    "DisplayName": "Network bytes sent"
  }
}
```

Even if `wlan0`, `lo`, or other interfaces exist on the system, only `eth0` and `eth1` Sensors will be created.

### Bound Mode (v1.0 compatible)

If `Interface` is already specified, behavior is identical to v1.0 — no expansion occurs:

```json
{
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "eth0"
  }
}
```

---

## 2. gpio (Explicit list / Auto-detect mode)

Expansion only triggers when `MetricName` is `pinState`. Other MetricNames like `isSupported` are unaffected.

### Auto-detect Mode (no list)

Omit the `PinId` parameter; the system automatically detects all GPIO pins and expands.

**Configuration Example**:
```json
{
  "Name": "gpio_pin",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "Pin state"
  }
}
```

If the system detects `DI_0`, `DI_1`, `DO_0`, it auto-expands to:

| Expanded Name | PinId | DisplayName |
|-------------|-------|-------------|
| `gpio_pin_DI_0` | `DI_0` | `DI_0 Pin state` |
| `gpio_pin_DI_1` | `DI_1` | `DI_1 Pin state` |
| `gpio_pin_DO_0` | `DO_0` | `DO_0 Pin state` |

### Explicit List Mode (with list)

Use the `PinIds` array or comma-separated string to specify a subset of pins to monitor:

```json
{
  "Name": "gpio_pin",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinIds": ["DI_0", "DI_1"]
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state",
    "DisplayName": "Pin state"
  }
}
```

Even if `DO_0` or other pins exist on the system, only `DI_0` and `DI_1` Sensors will be created.

### Bound Mode (v1.0 compatible)

If `PinId` is already specified, behavior is identical to v1.0:

```json
{
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinId": "UIO_GPIO2"
  }
}
```

---

## 3. temperature (Explicit list / Auto-detect mode)

> **v1.1 Change**: In v1.0, `MetricName` served as both metric name and sensor source identifier. v1.1 separates source identification into independent `Source` (single) / `Sources` (list) parameters, and `MetricName` is fixed to `"therm"`. v1.0 configurations (only setting `MetricName` as source name) remain compatible.

### Auto-detect Mode (no list)

Omit the `Source` parameter, use `Sources: []` or leave it out entirely; the system automatically detects all temperature sensor sources at startup and expands.

**Configuration Example**:
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

If the system detects `cpU-therm` and `gpU-therm`, it auto-expands to:

| Expanded Name | Source | DisplayName |
|-------------|--------|-------------|
| `temperature_cpU-therm` | `cpU-therm` | `cpU-therm Temperature` |
| `temperature_gpU-therm` | `gpU-therm` | `gpU-therm Temperature` |

### Explicit List Mode (with list)

Use the `Sources` array or comma-separated string to specify a subset of temperature sources:

```json
{
  "Name": "temperature",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Sources": ["cpU-therm", "gpU-therm"]
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

Even if other temperature sensors exist on the system, only the specified sources will be expanded.

### Bound Mode (single source specified)

If `Source` is specified, no expansion occurs:

```json
{
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "therm",
    "Source": "cpU-therm"
  }
}
```

### v1.0 Compatible Mode

If only `MetricName` is set (without `Source` or `Sources`), behavior is identical to v1.0 — `MetricName` is used as the sensor source name:

```json
{
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpU-therm"
  }
}
```

### v1.0 vs v1.1 Comparison

| | v1.0 | v1.1 |
|------|------|------|
| **MetricName** | Sensor source name (e.g., `cpU-therm`) | Fixed to `"therm"` |
| **Source Identification** | Handled by `MetricName` | Independent `Source` parameter |
| **List Expansion** | ❌ Not supported | `Sources` array or comma-separated string |
| **Auto-detect** | ❌ Not supported | Omit `Source` + `Sources: []` |

---

## Naming Rules After Expansion

When a generic Sensor is expanded, the generated Sensor properties follow these rules:

| Property | Rule | Example |
|------|------|------|
| `Name` | `{genericName}_{resourceName}` | `network_bytes_sent_eth0` |
| `DisplayName` | `{resourceName} {genericDisplayName}` | `eth0 Network bytes sent` |
| `Description` | `{genericDescription} ({resourceName})` | `Network bytes sent (eth0)` |

After expansion, the list parameter (e.g., `Interfaces`) is removed and a single resource parameter (e.g., `Interface`) is added. All other settings (`Report`, `SensorGroup`, `Schema`, etc.) are inherited from the generic Sensor.

---

## Edge Cases

| Scenario | Behavior |
|------|------|
| Auto-detect mode but no resources found | Generic Sensor is kept as-is without expansion, warning log emitted |
| Explicit list is empty array `[]` or empty string `""` | Falls back to auto-detect mode |
| Using GPIO/Temperature auto-detect on unsupported hardware | No resources found, kept as-is (same as v1.0 behavior) |
| Non-expansion types (cpu, memory, etc.) with list parameters set | List parameters are ignored, behavior identical to v1.0 |

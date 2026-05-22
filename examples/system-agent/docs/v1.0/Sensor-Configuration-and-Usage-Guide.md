## Sensor Configuration Quick Reference

### Field Overview

| Field | Modifiable | Description |
|------|---------|------|
| `Name` | ❌ Not modifiable | Unique sensor identifier, cannot be changed after creation |
| `SensorGroup` | ❌ Not modifiable | Sensor group, fixed enumeration (see table below) |
| `Report.Enabled` | ✅ Modifiable | Enable/disable periodic reporting (`true` / `false`) |
| `Report.Interval` | ✅ Modifiable | Reporting interval in milliseconds |
| `SensorInfo.Schema` | ⚠️ Modifiable, enum values only | Expected data type, must match MetricName return type |
| `SensorInfo.Description` | ✅ Modifiable | Sensor description text, free-form |
| `SensorInfo.DisplayName` | ✅ Modifiable | Sensor display name, free-form |
| `Parameters.MetricType` | ⚠️ Modifiable, enum values only | Metric type, must be a supported MetricType |
| `Parameters.MetricName` | ⚠️ Modifiable, enum values only | Metric name, must be supported for the corresponding MetricType |

---

### Supported SensorGroup Values

| Value | Full Name | Purpose |
|----|------|------|
| `AI` | Analog Input | Analog input sensor readings |
| `AO` | Analog Output | Analog output control |
| `DI` | Digital Input | Digital input sensors |
| `DO` | Digital Output | Digital output control |
| `TEMP` | Temperature | Temperature sensors |
| `PWR` | Power | Power monitoring sensors |
| `SYS` | System | System resource monitoring |

### Supported SensorInfo.Schema Values

| Category | Schema Value | Description |
|------|----------|------|
| Basic Numeric | `double` | Double-precision floating point |
| Basic Numeric | `integer` | 32-bit integer |
| Basic Numeric | `long` | 64-bit integer |
| Basic Non-numeric | `boolean` | Boolean |
| Basic Non-numeric | `string` | String |
| MIME Type | `image/jpeg` | JPEG image |
| MIME Type | `image/png` | PNG image |
| MIME Type | `application/json` | JSON data |
| MIME Type | `application/octet-stream` | Binary data |

### Supported MetricType Values

| MetricType | Category | Description |
|-----------|------|------|
| `cpu` | System Resources | CPU usage, load |
| `memory` | System Resources | Memory usage |
| `disk` | System Resources | Disk capacity and I/O |
| `network` | System Resources | Network traffic and packets |
| `gpu` | System Resources | GPU utilization (requires NVIDIA driver) |
| `system` | System Resources | System time, processes, file descriptors |
| `hwinfo` | Hardware Info | Motherboard, BIOS, driver versions (requires hardware platform driver) |
| `temperature` | Onboard Sensors | Temperature sensors (requires hardware platform driver) |
| `voltage` | Onboard Sensors | Voltage sensors (requires hardware platform driver) |
| `fanspeed` | Onboard Sensors | Fan speed sensors (requires hardware platform driver) |
| `gpio` | Hardware Features | GPIO pin state (requires hardware platform driver) |
| `watchdog` | Hardware Features | Watchdog timer (requires hardware platform driver) |
| `thermalprotection` | Hardware Features | Thermal protection (requires hardware platform driver) |

### Combinations Requiring Additional Parameters

| MetricType + MetricName | Additional Parameter | Description |
|------------------------|---------|------|
| `disk` + any MetricName | `MountPoint` (required, e.g., `/` or `C:\`) | Specifies the disk mount point to monitor |
| `network` + any MetricName | `Interface` (required, e.g., `eth0`, `en0`) | Specifies the network interface to monitor |
| `gpio` + `pinState` | `PinId` (required, e.g., `UIO_GPIO2`) | Specifies the GPIO pin name or index to query |

> Other MetricType combinations only require `MetricType` + `MetricName`, no additional parameters.

#### ⚠️ Special Usage of MetricName for temperature

Although `temperature` has no additional parameters, the semantics of `MetricName` differ from other MetricTypes:

| MetricType | MetricName Semantics | Example |
|-----------|----------------|------|
| Others (cpu, memory, etc.) | Fixed enumeration value specifying the metric to collect | `usage`, `total`, `bytes_sent` |
| `temperature` | **Hardware sensor source name**, determined by hardware | `cpU-therm`, `gpU-therm` |

Users must know which temperature sensor names exist on the hardware to correctly fill in `MetricName`. See [METRIC-TYPES — temperature](METRIC-TYPES.md#temperature) for details.

> **v1.1 Note**: v1.1 separates this semantic into independent `Source` / `Sources` parameters, and `MetricName` becomes a fixed value `"therm"`. See [v1.1 METRIC-TYPES](../v1.1/METRIC-TYPES.md#3-temperature-sensor-expansion).

---

## Examples

### Basic Example — CPU Usage

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

### Additional Parameters — Disk Usage (MountPoint)

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
    "Description": "Disk usage percentage for root",
    "DisplayName": "Disk Usage (/)"
  }
}
```

### Additional Parameters — Network Traffic (Interface)

```json
{
  "Name": "network_eth0_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "eth0"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "Total bytes sent on eth0",
    "DisplayName": "Network Bytes Sent (eth0)"
  }
}
```

### Additional Parameters — GPIO Pin State (PinId)

```json
{
  "Name": "gpio_pin_UIO_GPIO2",
  "SensorGroup": "DI",
  "Parameters": {
    "MetricType": "gpio",
    "MetricName": "pinState",
    "PinId": "UIO_GPIO2"
  },
  "Report": {
    "Enabled": true,
    "Interval": 6000
  },
  "SensorInfo": {
    "Schema": "integer",
    "Description": "GPIO pin state for UIO_GPIO2",
    "DisplayName": "UIO_GPIO2 State"
  }
}
```

### Hardware Info — Motherboard Name

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

### Onboard Sensors — Temperature

```json
{
  "Name": "temperature_cpu_therm",
  "SensorGroup": "TEMP",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpu-therm"
  },
  "Report": {
    "Enabled": true,
    "Interval": 1000
  },
  "SensorInfo": {
    "Schema": "double",
    "Description": "CPU thermal sensor temperature",
    "DisplayName": "CPU Thermal"
  }
}
```



# MetricType Configuration Guide

Users configure `MetricType` to specify the metrics that System Agent collects. Even if the hardware does not support a metric, System Agent will not throw an exception — it simply cannot collect the corresponding data.

## Sensor Configuration Structure

Each Sensor in `devicecfg.json` has the following structure. The Parameters section varies depending on the Sensor type:

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
    "Description": "SensorInfo Description for cpu.usage",
    "DisplayName": "SensorInfo DisplayName for cpu.usage"
  }
}
```

| Required Field | Description |
|------|------|
| `Name` | Unique sensor identifier |
| `SensorGroup` | Sensor group |
| `Report.Enabled` | Whether reporting is enabled |
| `Report.Interval` | Reporting interval (milliseconds) |
| `SensorInfo.Schema` | Expected data type (`double`, `long`, `string`, `boolean`, `integer`) |
| `SensorInfo.Description` | Sensor description |
| `SensorInfo.DisplayName` | Sensor display name |
| `Parameters.MetricType` | Metric type |
| `Parameters.MetricName` | Metric name |

---

## MetricType Categories and Design Principles

## 1. System Resources

Cross-platform support (Linux, Windows, macOS) using standard OS APIs.

### cpu

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `usage` | Overall CPU usage (calculated from per-core time) | double | % (0-100) |
| `load1` | 1-minute load average | double | - |
| `load5` | 5-minute load average | double | - |
| `load15` | 15-minute load average | double | - |
| `context_switches` | Total context switches | long | count |

**Configuration Example**:
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
    "Description": "SensorInfo Description for cpu.usage",
    "DisplayName": "SensorInfo DisplayName for cpu.usage"
  }
}
```

### memory

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `total` | Total memory | long | bytes |
| `available` | Available memory (including reclaimable cache) | long | bytes |
| `used` | Used memory (total - available) | long | bytes |
| `free` | Free memory | long | bytes |
| `cached` | Cached memory | long | bytes |
| `buffers` | Buffer memory | long | bytes |
| `swap_total` | Total swap | long | bytes |
| `swap_free` | Available swap | long | bytes |

### disk

Requires additional parameter `MountPoint` (e.g., `/` or `C:\`)

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `total` | Total disk capacity | long | bytes |
| `available` | Available capacity (non-privileged user) | long | bytes |
| `free` | Free capacity | long | bytes |
| `used` | Used capacity | long | bytes |
| `usage_percent` | Usage percentage | double | % (0-100) |
| `reads_completed` | Total read operations | long | count |
| `writes_completed` | Total write operations | long | count |
| `read_bytes` | Total bytes read | long | bytes |
| `written_bytes` | Total bytes written | long | bytes |

**Configuration Example**:
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
    "Description": "SensorInfo Description for disk.root.usage_percent",
    "DisplayName": "SensorInfo DisplayName for disk.root.usage_percent"
  }
}
```

### network

Requires additional parameter `Interface` (e.g., `eth0`, `en0`)

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `bytes_sent` | Total bytes sent | long | bytes |
| `bytes_received` | Total bytes received | long | bytes |
| `packets_sent` | Total packets sent | long | packets |
| `packets_received` | Total packets received | long | packets |
| `errors` | Total send/receive errors (errors_in + errors_out) | long | count |
| `errors_in` | Receive errors | long | count |
| `errors_out` | Send errors | long | count |

**Configuration Example**:
```json
{
  "Name": "network_en0_bytes_sent",
  "SensorGroup": "SYS",
  "Parameters": {
    "MetricType": "network",
    "MetricName": "bytes_sent",
    "Interface": "en0"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  },
  "SensorInfo": {
    "Schema": "long",
    "Description": "SensorInfo Description for network.en0.bytes_sent",
    "DisplayName": "SensorInfo DisplayName for network.en0.bytes_sent"
  }
}
```

### system

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `time` | Current system time | long | Unix timestamp (seconds) |
| `timex_offset` | NTP time offset | double | seconds |
| `boot_time` | System boot time | long | Unix timestamp (seconds) |
| `filefd_allocated` | Allocated file descriptors | long | count |
| `filefd_maximum` | Maximum file descriptors | long | count |
| `procs_running` | Running processes | int | count |
| `procs_blocked` | Blocked processes | int | count |
| `intr_total` | Total interrupts | long | count |

### gpu

Uses NVIDIA NVML library. Requires NVIDIA driver.

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `utilization` | GPU utilization percentage | int | % (0-100) |

---

## 2. Hardware Information

Applicable platforms: Industrial PCs (requires hardware platform driver, e.g., Advantech SUSI Driver)

### hwinfo

| MetricName | Description | Data Type |
|-----------|------|---------|
| `motherboardname` | Motherboard name | string |
| `manufacturer` | Manufacturer | string |
| `biosrevision` | BIOS revision | string |
| `driverversion` | Driver version | string |
| `libraryversion` | SDK library version | string |
| `ecrevision` | Embedded controller revision | string |

**Configuration Example**:
```json
{
  "Name": "hwinfo_motherboard",
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
    "Description": "SensorInfo Description for hwinfo.motherboard",
    "DisplayName": "SensorInfo DisplayName for hwinfo.motherboard"
  }
}
```

---

## 3. Onboard Sensors

Applicable platforms: Industrial PCs (requires hardware platform driver, e.g., Advantech SUSI Driver)

### temperature

Set `MetricName` to the sensor source name (e.g., `cpU-therm`, `gpU-therm`). Returns the temperature value (`double`, unit: °C). Supports case-insensitive matching.

**Configuration Example** (specifying sensor):
```json
{
  "Name": "temperature_cpU_therm",
  "Parameters": {
    "MetricType": "temperature",
    "MetricName": "cpU-therm"
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

---

## 4. Hardware Features

Applicable platforms: Industrial PCs (requires hardware platform driver, e.g., Advantech SUSI Driver)

### gpio

Supported MetricNames:

| MetricName | Description | Additional Parameters | Data Type |
|-----------|------|---------|---------|
| `isSupported` | Whether GPIO is supported | None | boolean |
| `pinState` | Pin state (0=Low, 1=High) | `PinId` (required) | integer |

**Configuration Example** (query support status):
```json
{
  "Name": "gpio_isSupported",
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

### voltage

Returns voltage readings (`double`, unit: V).

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `voltage` | Voltage reading | double | V |

### fanspeed

Returns fan speed readings (`double`, unit: RPM).

| MetricName | Description | Data Type | Unit |
|-----------|------|---------|------|
| `fanspeed` | Fan speed | double | RPM |

### watchdog

| MetricName | Description | Data Type |
|-----------|------|---------|
| `isSupported` | Whether watchdog is supported | boolean |

### thermalprotection

| MetricName | Description | Data Type |
|-----------|------|---------|
| `isSupported` | Whether thermal protection is supported | boolean |

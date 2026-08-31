---
sidebar_position: 3
sidebar_label: 'Configuration Reference'
hide_title: true
title: 'Sensor Configuration Reference'
keywords: ['SubNode', 'Sensor', 'Configuration', 'Reference', 'API']
description: 'Complete reference for all SubNode sensor configuration options'
---

# Configuration Reference

> Complete reference for all SubNode sensor configuration options.

## Sensor Properties

### Core Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Name` | string | Yes | - | Unique sensor identifier within device |
| `ResourceId` | string | Ignored | Derived | Always derived by the SDK — see [ResourceId Generation](#resourceid-generation) |
| `SensorGroup` | enum | Yes | - | Sensor category |
| `Dtmi` | string | No | Auto-generated | Digital Twin Model Identifier. Assigned by the SDK — do not set it. The [liveness heartbeat](./heartbeat.md) is stamped with a reserved DTMI, also by the SDK |

## ResourceId Generation

`ResourceId` is the cloud-facing identity of a sensor: it is the key carried on every
`TelemetryMeasure`, and the key WedaCore stores telemetry against. The SDK **always derives it**
— a value written into JSON or set through a builder is overwritten during device initialization.

### Rule

```
ResourceId = UUIDv5( namespace = ns("weda"), name = "{SubNodeDeviceId}.{DeviceName}.{SensorName}" )
```

| Component | Source | Notes |
|-----------|--------|-------|
| `SubNodeDeviceId` | Assigned by WedaCore when the SubNode registers | Globally unique |
| `DeviceName` | `DeviceConfiguration.DeviceInfo.DeviceName` | Unique within the SubNode |
| `SensorName` | `Sensor.Name` | Unique within the device |
| namespace | `ns("weda")` | Fixed constant `76daee46-c820-2357-a8a4-62b354807c5f` |

The value itself is a standard [RFC 4122](https://www.rfc-editor.org/rfc/rfc4122) version 5
(name-based, SHA-1) UUID: `SHA-1(namespace_bytes ‖ UTF-8(name))`, truncated to 16 bytes with the
version and variant bits set. The generator is
`Weda.SubNode.Core.Utilities.ResourceIdGenerator.GenerateSensorResourceId`.

The namespace is derived once from `SHA-1("weda")`, truncated to 16 bytes. Treat it as an **opaque
constant**: the implementation applies the version and variant bits before a byte-order swap, so the
rendered namespace carries version nibble `2` rather than `5` and is not itself a well-formed v5
UUID. This affects nothing downstream — it is only ever consumed as 16 bytes of namespace input —
but reimplementing the rule from the prose alone will produce different IDs. Use the constant above,
or call the SDK.

The three-part name is what makes the result globally unique: the SubNode's ID is unique across
the tenant, the device name within the SubNode, and the sensor name within the device.

### When it is derived

| Trigger | Code path |
|---------|-----------|
| Device initialization | `DeviceBase.InitializeAsync` → `DeviceInitializer.EnrichConfiguration` |
| Cloud-pushed sensor configuration | `ConfigurationUpdateHelper.MapToSensor` |
| SubNode re-registration | `SubNodeManager.ReEnrichConfigurations` |

### Stability guarantees

- **Deterministic** — the same SubNodeId + device name + sensor name always yields the same
  `ResourceId`. Restarting the SubNode, or rebuilding the same configuration on another host, does
  not change it.
- **Not stable across renames** — renaming a device or a sensor produces a *new* `ResourceId`. To
  WedaCore this is a new sensor; historical telemetry stays attached to the old ID.
- **Not stable across re-registration** — if cloud registration is reset and the SubNode is issued a
  new `SubNodeDeviceId`, every `ResourceId` under it is re-derived and changes.

Treat device and sensor names as part of the SubNode's public contract: pick them before the first
deployment and avoid changing them afterwards.

> **`DeviceResourceId`** is a separate field. It is set to the `SubNodeDeviceId` itself, not derived,
> and identifies the SubNode that owns the sensor.

### Worked example: the `cpu_usage` sensor

Taking the System Agent example (`examples/system-agent/`), which declares a CPU usage sensor:

```json
{
  "DeviceConfigs": {
    "SystemAgentDeviceConfig": {
      "Sensors": [
        {
          "Name": "cpu_usage",
          "SensorGroup": "SYS",
          "Parameters": { "MetricType": "cpu", "MetricName": "usage" },
          "Report": { "Enabled": true, "Interval": 5000 }
        }
      ]
    }
  }
}
```

Note there is no `ResourceId` in the JSON — there is nothing to write, because the SDK supplies it.
The three inputs come from:

| Component | Value | Where it comes from |
|-----------|-------|---------------------|
| `SubNodeDeviceId` | `0f7c3a5e-9b21-4d8f-a6c4-1e2d3b4a5c6d` | Assigned by WedaCore at registration — the value below is illustrative; yours will differ |
| `DeviceName` | `SystemAgentDeviceConfig` | The key under `DeviceConfigs`, registered via `builder.AddDevice<LocalSystemAgentDevice>("SystemAgentDeviceConfig")` |
| `SensorName` | `cpu_usage` | `Sensors[].Name` |

Which gives:

```
name       = "0f7c3a5e-9b21-4d8f-a6c4-1e2d3b4a5c6d.SystemAgentDeviceConfig.cpu_usage"
ResourceId = UUIDv5( 76daee46-c820-2357-a8a4-62b354807c5f, name )
           = 9b931c74-d121-5c71-8051-1ca4b10e109d
```

That is the ID the sensor reports under, every five seconds:

```csharp
new TelemetryMeasure
{
    ResourceId = "9b931c74-d121-5c71-8051-1ca4b10e109d",
    Value      = 37.2,
    Timestamp  = 1753939200000
}
```

Sibling sensors on the same device differ only in the last name segment — `cpu_load_1m` under the
same SubNodeId resolves to `6e34ab53-116d-5dbe-ba2d-b83695f94ffb`.

Rename the sensor to `cpu_utilisation`, or the device key to `SystemAgent`, and the `ResourceId`
changes — WedaCore then treats it as a new sensor and the history stays with the old ID.

### SensorGroup Values

| Value | Description | Typical Data Type |
|-------|-------------|-------------------|
| `AI` | Analog Input | double |
| `AO` | Analog Output | double |
| `DI` | Digital Input | boolean |
| `DO` | Digital Output | boolean |
| `TEMP` | Temperature | double |
| `PWR` | Power/Energy | double |
| `SYS` | System metrics | varies |

## Parameters (Protocol-Specific)

### Modbus Parameters

| Parameter | Type | Required | Values | Description |
|-----------|------|----------|--------|-------------|
| `RegisterType` | string | Yes | HoldingRegister, InputRegister, Coil, DiscreteInput | Modbus function code |
| `RegisterAddress` | int | Yes | 0-65535 | Starting register address |
| `RegisterCount` | int | Yes | 1-125 | Number of registers to read |
| `DataType` | string | Yes | See table below | Data interpretation |

#### Modbus Data Types

| DataType | Registers | Range/Precision |
|----------|-----------|-----------------|
| `UInt16` | 1 | 0 to 65,535 |
| `Int16` | 1 | -32,768 to 32,767 |
| `UInt32` | 2 | 0 to 4,294,967,295 |
| `Int32` | 2 | -2,147,483,648 to 2,147,483,647 |
| `Float32` | 2 | IEEE 754 single precision |
| `Float64` | 4 | IEEE 754 double precision |
| `String16` | N | 16-bit characters |

### MQTT Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `Topic` | string | Yes | - | MQTT topic to subscribe |
| `Qos` | int | No | 0 | Quality of Service (0, 1, 2) |
| `JsonPath` | string | No | - | JSONPath expression for value extraction |
| `PayloadType` | string | No | "json" | Payload format: json, binary, text |

### HTTP Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `Endpoint` | string | Yes | - | HTTP endpoint URL |
| `Method` | string | No | "GET" | HTTP method |
| `JsonPath` | string | No | - | JSONPath for response parsing |
| `Headers` | object | No | - | Custom HTTP headers |

## SensorInfo

Metadata for DTDL generation and display:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Schema` | string | No | Inferred from SensorGroup | DTDL schema type |
| `DisplayName` | string | No | Same as Name | Human-readable name |
| `Description` | string | No | - | Sensor description |

### Schema Types

| Schema | Description | C# Type |
|--------|-------------|---------|
| `boolean` | True/false value | bool |
| `integer` | Whole number | int/long |
| `double` | Floating point | double |
| `string` | Text value | string |
| `dateTime` | ISO 8601 timestamp | DateTime |

## Report Configuration

### Basic Report Settings

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Enabled` | bool | No | true | Enable sensor reporting |
| `Interval` | double | No | 1000 | Sampling interval (ms) |
| `Unit` | string | No | - | Unit of measurement |

### TransformPipeline

Array of transform configurations executed in order:

```json
{
  "TransformPipeline": [
    {
      "Type": "transform_type",
      "Enabled": true,
      "Parameters": { ... }
    }
  ]
}
```

#### Built-in Transforms

| Type | Parameters | Description |
|------|------------|-------------|
| `calibration` | Scale, Offset | Linear calibration: `value * Scale + Offset` |
| `unitconversion` | FromUnit, ToUnit | Unit conversion |
| `chunking` | ChunkSize | Batch data points |

##### Calibration Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Scale` | double | 1.0 | Multiplication factor |
| `Offset` | double | 0.0 | Addition offset |

##### Unit Conversion Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `FromUnit` | string | Source unit |
| `ToUnit` | string | Target unit |

Supported unit conversions:
- Temperature: celsius, fahrenheit, kelvin
- Pressure: pascal, bar, psi, atm
- Length: meter, foot, inch

### DspPipeline

Array of DSP filter configurations:

```json
{
  "DspPipeline": [
    {
      "Type": "filter_type",
      "Enabled": true,
      "Parameters": { ... }
    }
  ]
}
```

#### Built-in DSP Filters

| Type | Parameters | Description |
|------|------------|-------------|
| `movingAverage` | WindowSize | Simple moving average |
| `kalman` | ProcessNoise, MeasurementNoise | Kalman filter |
| `lowpass` | CutoffFrequency, SampleRate | Low-pass filter |
| `highpass` | CutoffFrequency, SampleRate | High-pass filter |

##### Moving Average Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `WindowSize` | int | 5 | Number of samples to average |

##### Kalman Filter Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ProcessNoise` | double | 0.01 | Process noise covariance |
| `MeasurementNoise` | double | 0.1 | Measurement noise covariance |

### Thresholds

Alert threshold configuration:

| Property | Type | Description |
|----------|------|-------------|
| `LowerWarning` | double? | Warning when value <= threshold |
| `LowerCritical` | double? | Critical when value <= threshold |
| `UpperWarning` | double? | Warning when value >= threshold |
| `UpperCritical` | double? | Critical when value >= threshold |

Threshold evaluation order: Critical thresholds are checked before Warning thresholds.

## Record Configuration

Local recording settings:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Enabled` | bool | No | **true** | Enable local recording for this sensor. Set `false` to make a sensor transmit-only |
| `Interval` | int | No | 0 | Recording interval in milliseconds. `0` means use the sensor's reporting interval |

Recording is on by default per sensor, so a sensor is recorded unless you opt it out. The
storage location is a device-wide setting, not a per-sensor one.

## DeviceCommunication

Connection settings for device communication:

### TCP/Modbus TCP

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Host` | string | Yes | - | IP address or hostname |
| `Port` | int | Yes | - | TCP port number |
| `Timeout` | int | No | 5000 | Connection timeout (ms) |
| `RetryCount` | int | No | 3 | Number of retries |

### Serial/Modbus RTU

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `PortName` | string | Yes | - | COM port (e.g., "COM1", "/dev/ttyUSB0") |
| `BaudRate` | int | No | 9600 | Baud rate |
| `DataBits` | int | No | 8 | Data bits |
| `Parity` | string | No | "None" | Parity: None, Odd, Even |
| `StopBits` | string | No | "One" | Stop bits: One, OnePointFive, Two |

### MQTT

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `BrokerUrl` | string | Yes | - | MQTT broker URL |
| `ClientId` | string | No | Auto-generated | MQTT client ID |
| `Username` | string | No | - | Authentication username |
| `Password` | string | No | - | Authentication password |
| `UseTls` | bool | No | false | Enable TLS |

## Periods

Timing configuration:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ReadTelemetryInterval` | int | 1000 | Telemetry read interval (ms) |
| `HealthReportInterval` | int | 30000 | Health report interval (ms) |
| `TelemetrySendInterval` | int | 5000 | Cloud send interval (ms) |

## Properties

Protocol-specific properties:

### Modbus Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SlaveId` | int | 1 | Modbus slave/unit ID |
| `ByteOrder` | string | "BigEndian" | Byte order: BigEndian, LittleEndian |
| `WordOrder` | string | "BigEndian" | Word order for 32-bit values |

## Configuration Validation

The SDK validates configurations at startup:

| Validation | Error Message |
|------------|---------------|
| Missing Name | "Sensor name is required" |
| Invalid RegisterType | "Invalid register type: {value}" |
| RegisterAddress out of range | "Register address must be 0-65535" |
| Invalid DataType | "Unsupported data type: {value}" |
| Interval <= 0 | "Interval must be greater than 0" |

## See Also

- [Configuration via Code](./configuration-via-code.md) - Programmatic configuration
- [Configuration via JSON](./configuration-via-json.md) - JSON configuration
- [Liveness Heartbeat](./heartbeat.md) - Reporting SubNode connectivity
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Pipeline details

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

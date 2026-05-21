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
| `ResourceId` | string | No | Auto-generated | UUID following Device Capability Guideline |
| `SensorGroup` | enum | Yes | - | Sensor category |
| `Dtmi` | string | No | Auto-generated | Digital Twin Model Identifier |

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
| `Enabled` | bool | No | false | Enable local recording |
| `Path` | string | No | - | Recording file path |

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
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Pipeline details

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

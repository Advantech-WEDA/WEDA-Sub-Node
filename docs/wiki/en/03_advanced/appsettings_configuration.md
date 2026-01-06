---
title: "Complete appsettings.json Configuration Guide"
description: "Complete configuration reference for Weda SubNode SDK's appsettings.json"
author: "Rain Hu"
date: "2025-11-17"
lang: "en"
translations:
  - lang: "zh"
    path: "../../zh/03_advanced/appsettings_configuration.md"
---

# Complete appsettings.json Configuration Guide

This document provides a complete configuration reference for Weda SubNode SDK's `appsettings.json`, covering all configurable fields and their purposes.

---

## Table of Contents

- [Basic Structure](#basic-structure)
- [Configuration Priority](#configuration-priority)
- [Serilog Logging Configuration](#serilog-logging-configuration)
- [NATS Messaging Configuration](#nats-messaging-configuration)
- [Device Configuration (DeviceConfigs)](#device-configuration-deviceconfigs)
  - [Basic Device Fields](#basic-device-fields)
  - [Background Task Periods](#background-task-periods)
  - [Device Capabilities](#device-capabilities)
  - [Communication Settings](#communication-settings)
  - [Sensor Configuration](#sensor-configuration)
  - [Sensor Parameters](#sensor-parameters)
  - [Sensor Config](#sensor-config)
  - [Transform Pipeline](#transform-pipeline)
  - [DSP Filter Pipeline](#dsp-filter-pipeline)
  - [Thresholds Settings](#thresholds-settings)
- [Complete Example](#complete-example)

---

## Basic Structure

The `appsettings.json` file contains three main sections:

```json
{
  "Serilog": { ... },      // Logging configuration
  "WedaNode": { ... },         // NATS messaging configuration
  "DeviceConfigs": { ... } // Device configurations (multiple devices supported)
}
```

---

## Serilog Logging Configuration

Controls the application's logging output behavior.

```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.File",
      "Serilog.Sinks.Debug"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Weda.SubNode": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

### Field Descriptions

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `Using` | string[] | Serilog sink packages to use | - |
| `MinimumLevel.Default` | string | Default minimum log level (Verbose/Debug/Information/Warning/Error/Fatal) | Information |
| `MinimumLevel.Override` | object | Override log levels for specific namespaces | - |
| `WriteTo` | array | Log output targets (Console, File, Debug, etc.) | - |

### Recommended Configuration

- **Development environment**: `Weda.SubNode`: `Debug`
- **Production environment**: `Weda.SubNode`: `Information` or `Warning`

---

## NATS Messaging Configuration

Configure NATS messaging connection settings for cloud service communication.

```json
{
  "WedaNode": {
    "Url": "nats://localhost:4222",
    "Name": "default",
    "SerializerType": "json",
    "AuthStrategy": "None"
  }
}
```

### Basic Fields

| Field | Type | Required | Description | Default |
|-------|------|----------|-------------|---------|
| `Url` | string | Yes | NATS server address | `nats://localhost:4222` |
| `Name` | string | No | Connection name (for identification) | `default` |
| `SerializerType` | string | No | Serialization type (json/protobuf) | `json` |
| `AuthStrategy` | string | No | Authentication strategy | `None` |

### Authentication Strategies (AuthStrategy)

The SDK supports multiple NATS authentication strategies:

| AuthStrategy | Description | Required Fields |
|--------------|-------------|-----------------|
| `None` | No authentication (anonymous connection) | - |
| `UserPassword` | Username and password authentication | `Username`, `Password` |
| `Token` | Token-based authentication | `Token` |
| `CredFile` | Credential file authentication (JWT + NKey) | `CredFile` |
| `TlsCert` | TLS client certificate authentication (Mutual TLS) | `TlsCertPath`, `TlsKeyPath`, (optional: `TlsCaPath`) |

### Configuration Examples

#### 1. No Authentication (None)

```json
{
  "WedaNode": {
    "Url": "nats://localhost:4222",
    "Name": "default",
    "AuthStrategy": "None"
  }
}
```

#### 2. Username and Password Authentication (UserPassword)

```json
{
  "WedaNode": {
    "Url": "nats://localhost:4222",
    "Name": "default",
    "AuthStrategy": "UserPassword",
    "Username": "myuser",
    "Password": "mypassword"
  }
}
```

#### 3. Token Authentication (Token)

```json
{
  "WedaNode": {
    "Url": "nats://localhost:4222",
    "Name": "default",
    "AuthStrategy": "Token",
    "Token": "your-auth-token-here"
  }
}
```

#### 4. Credential File Authentication (CredFile)

Recommended authentication method for production environments. The credential file contains JWT and NKey seed.

```json
{
  "WedaNode": {
    "Url": "nats://nats.example.com:4222",
    "Name": "default",
    "AuthStrategy": "CredFile",
    "CredFile": "/path/to/credentials.creds"
  }
}
```

#### 5. TLS Client Certificate Authentication (TlsCert)

Use mutual TLS authentication; URL must use `tls://` protocol.

```json
{
  "WedaNode": {
    "Url": "tls://nats.example.com:4222",
    "Name": "default",
    "AuthStrategy": "TlsCert",
    "TlsCertPath": "/path/to/client-cert.pem",
    "TlsKeyPath": "/path/to/client-key.pem",
    "TlsCaPath": "/path/to/ca-cert.pem"
  }
}
```

### Authentication Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `Username` | string | Username (for UserPassword strategy) |
| `Password` | string | Password (for UserPassword strategy) |
| `Token` | string | Authentication token (for Token strategy) |
| `CredFile` | string | Credential file path, containing JWT and NKey seed (for CredFile strategy) |
| `TlsCertPath` | string | TLS client certificate file path (PEM or PFX format) |
| `TlsKeyPath` | string | TLS client private key file path (PEM format) |
| `TlsCaPath` | string | CA certificate file path (for server verification) |

### Use Cases

- **Development environment**: Use `AuthStrategy: "None"` or `WedaFactory.Cloud.Mock`
- **Testing environment**: Use `UserPassword` or `Token` for simple authentication
- **Production environment**: Recommended to use `CredFile` or `TlsCert` for secure authentication

> **Security Note**: Avoid hardcoding sensitive information (passwords, tokens) directly in `appsettings.json`. Recommended to use environment variables or secure key management services.
>
> Example using environment variables:
> ```json
> {
>   "WedaNode": {
>     "Password": "${NATS_PASSWORD}"
>   }
> }
> ```

---

## Device Configuration (DeviceConfigs)

`DeviceConfigs` is a dictionary where the key is the configuration name (used as SubNodeTypeName), and the value is the device configuration object.

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      // Device configuration...
    },
    "MySecondDevice": {
      // Another device configuration...
    }
  }
}
```

### Basic Device Fields

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceName": "MyWiseDevice4012",
      "SubNodeType": "adamEthernet",
      "DtdlPath": "assets/dtdl/dtmi/advantech/edgesync/wise-4012.json",
      "Periods": {
        "ReadTelemetry": 5000,
        "SendTelemetry": 0
      }
    }
  }
}
```

| Field | Type | Required | Description | Default |
|-------|------|----------|-------------|---------|
| `Enabled` | boolean | No | Whether to enable this device (only effective with auto-scan) | true |
| `DeviceName` | string | Yes | Device name (for identification), registered with Weda.Core | - |
| `SubNodeType` | string | Yes | Device type (AdamEthernet/SerialDevice/DaqDevice/SystemMonitor/CustomDevice) | - |
| `DtdlPath` | string | Yes | DTDL file path (relative to project root) | - |
| `Periods` | object | No | Background task period settings (see below) | See defaults |

#### Important Notes

- **DeviceId**: Should NOT be set in appsettings.json; assigned by cloud or retrieved from localStorage
- **SubNodeTypeName**: Should NOT be set in appsettings.json; automatically assigned from Config Key (e.g., "MyFirstDevice")
- **Enabled**: Only effective when using auto-scan (see below)

---

### Background Task Periods

The `Periods` configuration controls the execution intervals for device background tasks, including telemetry reading, uploading, and health reporting.

```json
{
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Periods": {
        "ReadTelemetry": 5000,
        "SendTelemetry": 0,
        "ReportHealth": 60000,
        "PollCommands": 5000,
        "ReportConfiguration": 300000
      }
    }
  }
}
```

#### Periods Field Descriptions

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `ReadTelemetry` | number | Telemetry sampling period (milliseconds), used as default when Sensor's `Interval` is not set | 5000 |
| `SendTelemetry` | number | Telemetry upload mode control (see below) | 0 |
| `ReportHealth` | number | Health status reporting period (milliseconds) | 60000 |
| `PollCommands` | number | Command polling period (milliseconds) | 5000 |
| `ReportConfiguration` | number | Configuration reporting period (milliseconds) | 300000 |

#### ReadTelemetry Sampling Period

`ReadTelemetry` defines the device-level default sampling period. When a Sensor's `Config.Interval` is not set (value is 0 or negative), this value is used as a fallback.

**Priority Order**:
1. `Sensor.Config.Interval` (if set and > 0)
2. `Device.Periods.ReadTelemetry` (fallback default)

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "Periods": {
        "ReadTelemetry": 5000  // Default: sample every 5 seconds
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Config": {
            "Interval": 1000  // Uses 1 second (overrides default)
          }
        },
        {
          "Name": "humidity",
          "Config": {
            "Interval": 0     // Uses 5 seconds (falls back to ReadTelemetry)
          }
        }
      ]
    }
  }
}
```

#### SendTelemetry Upload Mode

`SendTelemetry` controls the telemetry data upload behavior, providing two modes:

| Value | Mode | Description |
|-------|------|-------------|
| `0` | Realtime Upload | Upload data to cloud immediately after each sample |
| `> 0` | Batch Upload | Collect multiple samples and upload at specified interval |

##### Realtime Mode (SendTelemetry = 0)

```json
{
  "Periods": {
    "ReadTelemetry": 1000,
    "SendTelemetry": 0  // Realtime mode
  }
}
```

**Behavior**: Sample every 1 second, upload to cloud immediately after sampling.

**Applicable Scenarios**:
- Applications requiring real-time monitoring
- High data immediacy requirements
- Stable network, frequent uploads not a problem

##### Batch Mode (SendTelemetry > 0)

```json
{
  "Periods": {
    "ReadTelemetry": 1000,
    "SendTelemetry": 10000  // Batch mode: upload every 10 seconds
  }
}
```

**Behavior**: Sample every 1 second and store in internal buffer, batch upload all buffered data every 10 seconds.

**Applicable Scenarios**:
- Limited or unstable network resources
- Need to reduce network request frequency
- Reduce cloud service load
- Mobile device power saving requirements

#### Architecture Overview

The SDK uses a unified **SensorCache architecture**. Whether Pull mode (like Modbus) or Push mode (like MQTT, WebSocket), the data flow is the same:

```
┌─────────────────────────────────────────────────────────────────┐
│  Pull Mode (Modbus)           Push Mode (MQTT/WebSocket)        │
│  ┌──────────┐                 ┌──────────┐                      │
│  │ Device   │                 │ External │                      │
│  │ Request  │                 │ Message  │                      │
│  └────┬─────┘                 └────┬─────┘                      │
│       │                            │                            │
│       ▼                            ▼                            │
│  ┌──────────────────────────────────────────┐                   │
│  │            SensorCache                    │                   │
│  │  (Unified cache like Modbus registers)    │                   │
│  └────────────────────┬─────────────────────┘                   │
│                       │                                         │
│                       ▼  ReadTelemetry (Sampling)               │
│  ┌──────────────────────────────────────────┐                   │
│  │         Device Sampling Task             │                   │
│  │  (Reads at SensorReport.Interval period) │                   │
│  └────────────────────┬─────────────────────┘                   │
│                       │                                         │
│         ┌─────────────┴─────────────┐                           │
│         │                           │                           │
│         ▼                           ▼                           │
│   SendTelemetry = 0           SendTelemetry > 0                 │
│   ┌─────────────┐             ┌─────────────┐                   │
│   │ Realtime    │             │ Batch       │                   │
│   │ Upload      │             │ Upload      │                   │
│   └─────────────┘             └─────────────┘                   │
└─────────────────────────────────────────────────────────────────┘
```

**Key Concepts**:
- **SensorCache**: All data sources (Pull/Push) write to cache first, unified processing
- **Sampling Task**: Reads from cache at `SensorReport.Interval` or `Periods.ReadTelemetry` interval
- **Upload Task**: Realtime or batch upload based on `SendTelemetry` setting

#### Complete Examples

```json
{
  "DeviceConfigs": {
    "HighFrequencyDevice": {
      "Periods": {
        "ReadTelemetry": 100,     // 100ms high-frequency sampling
        "SendTelemetry": 5000,    // Batch upload every 5 seconds (avoid network congestion)
        "ReportHealth": 30000
      },
      "Sensors": [
        {
          "Name": "vibration",
          "Config": {
            "Interval": 50       // 50ms ultra-high-frequency sampling (overrides default)
          }
        }
      ]
    },
    "LowPowerDevice": {
      "Periods": {
        "ReadTelemetry": 60000,   // Sample once per minute
        "SendTelemetry": 300000,  // Batch upload every 5 minutes (power saving mode)
        "ReportHealth": 600000
      }
    },
    "RealtimeMonitor": {
      "Periods": {
        "ReadTelemetry": 1000,    // 1 second sampling
        "SendTelemetry": 0,       // Realtime upload (instant monitoring)
        "ReportHealth": 60000
      }
    }
  }
}
```

---

### Device Registration Methods

The SDK provides two ways to register devices: **Auto-scan** and **Manual addition**.

#### Method 1: Auto-scan Device Configuration

When using auto-scan, the SDK reads `DeviceConfigs` from `appsettings.json` and **determines whether to add devices based on the `Enabled` field**.

There are two ways to enable auto-scan:

**Option A: Using `CreateDefaultBuilder()` (Recommended for production)**

```csharp
var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();
// CreateDefaultBuilder internally calls ScanDevicesFromConfiguration()
```

**Option B: Manually calling `ScanDevicesFromConfiguration()`**

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .ScanDevicesFromConfiguration();  // Enable auto-scan
var app = builder.Build();
```

**Example Configuration and Results**:

```json
{
  "DeviceConfigs": {
    "ProductionDevice": {
      "Enabled": true,      // Will be added
      "DeviceName": "WISE-4012-A",
      "SubNodeType": "adamEthernet",
      "Communication": { ... }
    },
    "TestDevice": {
      "Enabled": false,     // Will NOT be added
      "DeviceName": "WISE-4012-B",
      "SubNodeType": "adamEthernet",
      "Communication": { ... }
    }
  }
}
```

```csharp
// Program.cs
var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
// Result: Only ProductionDevice will be added, TestDevice will be skipped
```

**Applicable Scenarios**:
- **Production environments** where dynamic control of multiple devices is needed
- **Development/testing environments** where quick device configuration switching is needed

---

#### Method 2: Manual Device Addition

Use `AddDevice<T>()` to manually specify devices to add. This method reads configuration from `appsettings.json` but **does NOT check the `Enabled` field**; devices are added directly.

**Basic Usage**:

```csharp
var builder = WedaApplication.CreateBuilder(args);

// Manually add device, reading configuration from DeviceConfigs["MyDevice"]
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
```

**Complete Example**:

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": false,     // Will NOT be checked
      "DeviceName": "WISE-4012",
      "SubNodeType": "adamEthernet",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [ ... ]
    }
  }
}
```

```csharp
// Program.cs
var builder = WedaApplication.CreateBuilder(args);

// Manually add MyDevice, will be added even with Enabled: false
builder.AddDevice<TcpModbusDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();

// Result: MyDevice will be added
// All configuration (DeviceName, Communication, Sensors) will be correctly read
// But Enabled: false is NOT checked
```

**Using Factory Functions (Advanced Usage)**:

When devices need custom initialization logic, use factory functions:

```csharp
var builder = WedaApplication.CreateBuilder(args);

// Create device using factory function
builder.AddDevice(context =>
{
    var config = new TcpModbusDeviceConfiguration
    {
        DeviceName = "CustomDevice",
        Host = "192.168.1.100",
        Port = 502,
        // ... other configuration
    };

    return new TcpModbusDevice(context, config.ToDeviceConfiguration());
});

var app = builder.Build();
```

**Applicable Scenarios**:
- Single device applications with full code control
- Complex initialization logic required
- Dynamic device configuration determined in code

---

#### Method Comparison

| Feature | Auto-scan | Manual Addition |
|---------|-----------|-----------------|
| **API** | `CreateDefaultBuilder()` or `ScanDevicesFromConfiguration()` | `AddDevice<T>()` |
| **Enabled Field** | Checked | NOT checked |
| **Configuration Source** | `appsettings.json` | `appsettings.json` or code |
| **Applicable Scenarios** | Multi-device dynamic configuration | Single device or custom initialization |
| **Flexibility** | Medium | High |
| **Code Complexity** | Low | Medium |

#### Usage Recommendations

| Scenario | Recommended Approach |
|----------|---------------------|
| Production, multi-device dynamic configuration | Use `CreateDefaultBuilder()` |
| Development/testing, quick device toggle | Use `ScanDevicesFromConfiguration()` |
| Single device, full code control | Use `AddDevice<T>("sectionName")` |
| Complex initialization logic | Use `AddDevice(factory)` |

---

### Device Capabilities

Describes basic device information and capabilities.

```json
{
  "DeviceCapabilities": {
    "Manufacturer": "Advantech",
    "Model": "WISE-4012",
    "SubNodeSwVersion": "0.0.1",
    "DeviceInfo": {
      "Version": "1.0.0",
      "Description": "WISE-4012 Ethernet I/O Module"
    }
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Manufacturer` | string | Yes | Manufacturer name |
| `Model` | string | Yes | Device model |
| `SubNodeSwVersion` | string | Yes | SubNode software version |
| `DeviceInfo` | object | No | Additional device information (custom fields) |

---

### Communication Settings

Different `SubNodeType` values require different communication parameters.

#### Modbus TCP (SubNodeType: "adamEthernet")

```json
{
  "Communication": {
    "Host": "172.16.8.122",
    "Port": 502,
    "SlaveId": 1
  }
}
```

| Field | Type | Required | Description | Default |
|-------|------|----------|-------------|---------|
| `Host` | string | Yes | Modbus TCP device IP address | - |
| `Port` | number | No | Modbus TCP communication port | 502 |
| `SlaveId` | number | No | Modbus Slave/Unit ID | 1 |

#### ISensing MQTT (SubNodeType: "adamEthernet")

```json
{
  "Communication": {
    "BrokerUrl": "mqtt://172.16.8.100:1883",
    "ClientId": "myClient",
    "Username": "user",
    "Password": "pass"
  }
}
```

---

## Sensor Configuration

`Sensors` is an array containing configurations for all sensors on the device.

```json
{
  "Sensors": [
    {
      "Name": "channel.0",
      "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
      "SensorGroup": "AI",
      "Parameters": { ... },
      "Config": { ... },
      "Metadata": { ... }
    }
  ]
}
```

### Basic Sensor Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | string | Yes | Sensor name/identifier (e.g., "channel.0", "ai.channel[0]") |
| `Dtmi` | string | Yes | Digital Twin Model Identifier (DTDL v2 specification) |
| `SensorGroup` | string | Yes | Sensor logical grouping (AI/DI/DO/AO/TEMP/PWR/SYS) |
| `ResourceId` | string | No | Sensor resource ID (auto-generated) |
| `Parameters` | object | Yes | Protocol-specific parameters (see next section) |
| `Config` | object | Yes | Sensor configuration (sampling, transforms, filters, etc.) |
| `Metadata` | object | No | Additional metadata |

---

### Sensor Parameters

Protocol-specific parameters; different device types have different parameter requirements.

#### Modbus Sensor Parameters

```json
{
  "Parameters": {
    "RegisterType": "HoldingRegister",
    "RegisterAddress": 0,
    "RegisterCount": 1,
    "DataType": "UInt16",
    "Batch": 1
  }
}
```

| Field | Type | Required | Description | Options | Default |
|-------|------|----------|-------------|---------|---------|
| `RegisterType` | string | Yes | Modbus register type | HoldingRegister, InputRegister, Coil, DiscreteInput | - |
| `RegisterAddress` | number | Yes | Starting register address (0-based) | 0-65535 | - |
| `RegisterCount` | number | No | Number of registers to read | 1-125 | 1 |
| `DataType` | string | Yes | Data parsing type | UInt16, Int16, UInt32, Int32, Float, Double, Boolean | UInt16 |
| `Batch` | number | No | Batch optimization ID (usually not needed) | Any integer | - |

> **Note**: The SDK has a built-in **automatic batch optimization algorithm** that automatically merges sensors with adjacent RegisterAddress values into batch reads based on their RegisterAddress and RegisterCount, so **manual `Batch` parameter configuration is usually not needed**. Only in special cases (such as forcing specific sensors to read separately) should you manually set it.

#### DataType Mapping Table

| DataType | Register Count | Bytes | Description |
|----------|----------------|-------|-------------|
| `Boolean` | 1 | 2 | Boolean value (0 or 1) |
| `UInt16` | 1 | 2 | Unsigned 16-bit integer (0-65535) |
| `Int16` | 1 | 2 | Signed 16-bit integer (-32768 to 32767) |
| `UInt32` | 2 | 4 | Unsigned 32-bit integer |
| `Int32` | 2 | 4 | Signed 32-bit integer |
| `Float` | 2 | 4 | 32-bit floating point |
| `Double` | 4 | 8 | 64-bit floating point |

#### Automatic Batch Optimization

The SDK has a built-in **automatic batch optimization algorithm** that automatically merges consecutive or adjacent registers into single reads, **no manual `Batch` parameter configuration needed**.

**Auto-optimization Example**:

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0, "RegisterCount": 1 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1, "RegisterCount": 1 } },
    { "Name": "ch2", "Parameters": { "RegisterAddress": 2, "RegisterCount": 1 } }
  ]
}
```

**SDK Automatic Processing**:
- SDK automatically detects that these three sensors use consecutive register addresses (0, 1, 2)
- Automatically merges into single batch read: `ReadBatch(0, count=3)`
- **Result**: 3 sensors require only 1 Modbus request (instead of 3)

**Optimization Algorithm**:

The SDK automatically merges sensors based on these rules:

1. **Group by RegisterType**: Only same-type registers (e.g., HoldingRegister) are merged
2. **Sort by RegisterAddress**: Automatically sort sensor addresses
3. **Calculate Gap**: Calculate interval between sensors
4. **Smart Merge**:
   - If gap <= `MaxGapSize` (default 2) AND batch size <= `MaxBatchSize` (default 125)
   - Then automatically merge into same batch

> **Why MaxBatchSize = 125?**
>
> This is a **Modbus TCP/RTU protocol limitation**:
> - Modbus protocol specifies maximum **125 holding/input registers** per single request
> - Determined by Modbus PDU (Protocol Data Unit) maximum 253 bytes limit
> - Each register is 2 bytes; after protocol overhead, approximately 125 registers can be read
>
> **MaxGapSize and MaxBatchSize Relationship**:
> - `MaxGapSize = 2`: If gap between two sensors is <= 2 registers, merge is attempted
> - `MaxBatchSize = 125`: Even with small gaps, if merged count exceeds 125 registers, will still split into batches

**Advanced Example (Sensors with Gaps)**:

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1 } },
    { "Name": "ch2", "Parameters": { "RegisterAddress": 2 } },
    { "Name": "ch10", "Parameters": { "RegisterAddress": 10 } }  // 8 register gap
  ]
}
```

**SDK Automatic Processing**:
- Batch 1: `ReadBatch(0, count=3)` -> reads ch0, ch1, ch2
- Batch 2: `ReadBatch(10, count=1)` -> reads ch10
- **Reason**: Gap between ch2 and ch10 = 8, less than MaxGapSize(10), but to avoid reading unused registers, SDK may split into two batches

#### Manual Batch Parameter (Advanced Usage)

In rare cases, if you need to **force specific sensors to read separately**, you can manually set the `Batch` parameter:

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0, "Batch": 1 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1, "Batch": 2 } }  // Force separate
  ]
}
```

**When Manual Configuration is Needed**:
- Some Modbus devices have issues with batch reads
- Different read frequencies needed (but recommend using different `Interval` settings instead)
- **In most cases, manual configuration is NOT needed**

---

### Sensor Config

Controls sensor sampling, transforms, filters, and threshold settings.

```json
{
  "Config": {
    "Enabled": true,
    "Interval": 1000,
    "Unit": "celsius",
    "TransformPipeline": [ ... ],
    "DspPipeline": [ ... ],
    "Thresholds": { ... }
  }
}
```

| Field | Type | Required | Description | Default |
|-------|------|----------|-------------|---------|
| `Enabled` | boolean | No | Enable/disable sensor | true |
| `Interval` | number | No | Sampling interval (milliseconds) | 1000 |
| `Unit` | string | No | Measurement unit (usually defined in DTDL) | "" |
| `TransformPipeline` | array | No | Data transform pipeline (before DSP) | [] |
| `DspPipeline` | array | No | DSP filter pipeline (after transforms) | [] |
| `Thresholds` | object | No | Threshold alarm settings | null |

---

### Transform Pipeline

Data transform pipeline executes before DSP filters, used for calibration, unit conversion, etc.

```json
{
  "TransformPipeline": [
    {
      "Type": "Calibration",
      "Enabled": true,
      "Parameters": {
        "Scale": 1.0,
        "Offset": 0.0
      }
    },
    {
      "Type": "UnitConversion",
      "Enabled": true,
      "Parameters": {
        "FromUnit": "celsius",
        "ToUnit": "fahrenheit"
      }
    }
  ]
}
```

> **Execution Order**: Transform execution order is determined by JSON array index (index 0 executes first, index 1 second). No `Order` parameter needed.

#### TransformConfig Fields

| Field | Type | Required | Description | Default |
|-------|------|----------|-------------|---------|
| `Type` | string | Yes | Transform type (Calibration, UnitConversion, Custom) | - |
| `Enabled` | boolean | No | Enable/disable this transform | true |
| `Parameters` | object | No | Transform-specific parameters | {} |

#### Common Transform Types

##### 1. Calibration

```json
{
  "Type": "Calibration",
  "Enabled": true,
  "Parameters": {
    "Scale": 0.1,
    "Offset": -5.0
  }
}
```

**Formula**: `output = (input * Scale) + Offset`

**Use Cases**:
- Sensor calibration
- Linear adjustment (y = mx + b)

##### 2. UnitConversion

```json
{
  "Type": "UnitConversion",
  "Enabled": true,
  "Parameters": {
    "FromUnit": "celsius",
    "ToUnit": "fahrenheit"
  }
}
```

**Supported Unit Conversions**:
- Temperature: celsius <-> fahrenheit <-> kelvin
- Pressure: pascal <-> bar <-> psi
- Length: meter <-> feet <-> inch
- etc...

---

### DSP Filter Pipeline

DSP filter pipeline executes after transforms, used for noise reduction and signal processing.

```json
{
  "DspPipeline": [
    {
      "Type": "movingAverage",
      "Enabled": true,
      "Parameters": {
        "WindowSize": 5
      }
    },
    {
      "Type": "kalman",
      "Enabled": true,
      "Parameters": {
        "ProcessNoise": 0.01,
        "MeasurementNoise": 0.1
      }
    }
  ]
}
```

> **Execution Order**: DSP Filter execution order is determined by JSON array index (index 0 executes first, index 1 second). No `Order` parameter needed.

#### DspFilterConfig Fields

| Field | Type | Required | Description | Default |
|-------|------|----------|-------------|---------|
| `Type` | string | Yes | Filter type (movingAverage, kalman, lowpass, highpass) | - |
| `Enabled` | boolean | No | Enable/disable this filter | true |
| `Parameters` | object | No | Filter-specific parameters | {} |

#### Common Filter Types

##### 1. movingAverage (Moving Average)

```json
{
  "Type": "movingAverage",
  "Enabled": true,
  "Parameters": {
    "WindowSize": 5
  }
}
```

**Parameters**:
- `WindowSize`: Window size (number of samples)

**Use Cases**:
- Smoothing noisy data
- Simple low-pass filtering

##### 2. kalman (Kalman Filter)

```json
{
  "Type": "kalman",
  "Enabled": true,
  "Parameters": {
    "ProcessNoise": 0.01,
    "MeasurementNoise": 0.1
  }
}
```

**Parameters**:
- `ProcessNoise`: Process noise (lower values trust the model more)
- `MeasurementNoise`: Measurement noise (lower values trust measurements more)

**Use Cases**:
- High-precision sensor data processing
- Prediction and smoothing

##### 3. lowpass / highpass (Low-pass/High-pass Filter)

```json
{
  "Type": "lowpass",
  "Enabled": true,
  "Parameters": {
    "CutoffFrequency": 10.0,
    "SamplingRate": 100.0
  }
}
```

**Parameters**:
- `CutoffFrequency`: Cutoff frequency (Hz)
- `SamplingRate`: Sampling rate (Hz)

---

### Thresholds Settings

Set alarm thresholds for sensor values.

```json
{
  "Thresholds": {
    "UpperCritical": 100.0,
    "UpperWarning": 80.0,
    "LowerWarning": 20.0,
    "LowerCritical": 0.0
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `UpperCritical` | number | Upper critical threshold |
| `UpperWarning` | number | Upper warning threshold |
| `LowerWarning` | number | Lower warning threshold |
| `LowerCritical` | number | Lower critical threshold |

#### Threshold Levels

The SDK automatically checks values and returns threshold levels:

| Threshold Level | Condition |
|-----------------|-----------|
| `Normal` | Value within warning range |
| `LowerWarning` | Value <= LowerWarning |
| `UpperWarning` | Value >= UpperWarning |
| `LowerCritical` | Value <= LowerCritical |
| `UpperCritical` | Value >= UpperCritical |

---

## Complete Example

Here is a complete `appsettings.json` example demonstrating all configurable fields:

```json
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console",
      "Serilog.Sinks.File",
      "Serilog.Sinks.Debug"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Weda.SubNode": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "WedaNode": {
    "Url": "nats://localhost:4222",
    "Name": "default",
    "SerializerType": "json",
    "AuthStrategy": "None"
  },
  "DeviceConfigs": {
    "MyWiseDevice": {
      "Enabled": true,
      "DeviceName": "WISE-4012-Factory-Floor",
      "SubNodeType": "adamEthernet",
      "DtdlPath": "assets/dtdl/dtmi/advantech/edgesync/wise-4012.json",
      "DeviceCapabilities": {
        "Manufacturer": "Advantech",
        "Model": "WISE-4012",
        "SubNodeSwVersion": "1.0.0",
        "DeviceInfo": {
          "Version": "1.0.0",
          "Description": "WISE-4012 Ethernet I/O Module",
          "Location": "Factory Floor A"
        }
      },
      "Communication": {
        "Host": "172.16.8.122",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 1,
            "DataType": "UInt16"
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "Unit": "celsius",
            "TransformPipeline": [
              {
                "Type": "Calibration",
                "Enabled": true,

                "Parameters": {
                  "Scale": 0.1,
                  "Offset": -5.0
                }
              }
            ],
            "DspPipeline": [
              {
                "Type": "movingAverage",
                "Enabled": true,

                "Parameters": {
                  "WindowSize": 5
                }
              }
            ],
            "Thresholds": {
              "UpperCritical": 80.0,
              "UpperWarning": 70.0,
              "LowerWarning": 10.0,
              "LowerCritical": 0.0
            }
          },
          "Metadata": {
            "Description": "Temperature sensor for monitoring factory floor",
            "Location": "Zone A"
          }
        },
        {
          "Name": "humidity",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 1,
            "RegisterCount": 1,
            "DataType": "UInt16",
            "Batch": 1
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "Unit": "percent",
            "TransformPipeline": [
              {
                "Type": "Calibration",
                "Enabled": true,

                "Parameters": {
                  "Scale": 0.01,
                  "Offset": 0.0
                }
              }
            ],
            "Thresholds": {
              "UpperWarning": 80.0,
              "LowerWarning": 30.0
            }
          }
        },
        {
          "Name": "pressure",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 2,
            "RegisterCount": 2,
            "DataType": "Float",
            "Batch": 1
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "Unit": "pascal",
            "DspPipeline": [
              {
                "Type": "kalman",
                "Enabled": true,

                "Parameters": {
                  "ProcessNoise": 0.01,
                  "MeasurementNoise": 0.5
                }
              }
            ]
          }
        }
      ]
    }
  }
}
```

---

## Best Practices

### 1. Fields NOT to Set in appsettings.json

The following fields should **NOT** appear in appsettings.json:

- `DeviceId` - Assigned by cloud or retrieved from localStorage
- `SubNodeTypeName` - Automatically assigned from DeviceConfigs key

### 2. DtdlPath Paths

- Use relative paths: `"assets/dtdl/dtmi/advantech/edgesync/wise-4012.json"`
- Avoid absolute paths: `"/Users/user/project/assets/..."`
- SDK automatically resolves paths from project root or `DTDL_BASE_PATH` environment variable

### 3. Batch Optimization

For sensors with consecutive addresses, using the same `Batch` ID can significantly reduce Modbus requests:

```json
{
  "Sensors": [
    { "Name": "ch0", "Parameters": { "RegisterAddress": 0, "Batch": 1 } },
    { "Name": "ch1", "Parameters": { "RegisterAddress": 1, "Batch": 1 } },
    { "Name": "ch2", "Parameters": { "RegisterAddress": 2, "Batch": 1 } },
    { "Name": "ch10", "Parameters": { "RegisterAddress": 10, "Batch": 2 } }
  ]
}
```

### 4. Transform vs DSP Pipeline

- **Transform Pipeline**: For data calibration and unit conversion (deterministic transforms)
- **DSP Pipeline**: For noise reduction and signal processing (statistical filtering)
- Execution order: `Raw Data -> Transform -> DSP -> Output`

### 5. Log Levels

Adjust log levels based on environment:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Weda.SubNode": "Debug"     // Development environment
        // "Weda.SubNode": "Information"  // Production environment
      }
    }
  }
}
```

---

## Related Documentation

- [DTDL Specification](https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md)
- [Transformation Pipeline Use Cases](../02_use_cases/02_transformation.md)
- [DSP Filter Use Cases](../02_use_cases/03_dsp_filters.md)
- [Connecting Real Devices](../01_quick_start/05_connect_real_device.md)

---

**Version**: 1.0.0
**Last Updated**: 2025-11-17
**Maintainer**: Rain Hu

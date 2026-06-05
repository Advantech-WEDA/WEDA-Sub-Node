---
title: "WISE-4012 Builder Example"
description: "Production-ready WISE-4012 integration using Builder pattern"
version: "0.0.1"
author: "Rain Hu"
date: "2025-11-18"
lang: "en"
---

# WISE-4012 Builder Example

Production-ready example demonstrating Advantech WISE-4012 industrial I/O module integration using the **Builder pattern** (wedabuilder style).

## Overview

This example shows how to integrate the WISE-4012 (4AI + 2AO) module using `WedaApplication.CreateBuilder()` pattern, which is recommended for production environments with multiple devices.

- **Hardware**: Advantech WISE-4012
- **Pattern**: Builder pattern (similar to ASP.NET Core)
- **Protocol**: Modbus TCP
- **Cloud**: Weda EdgeSync Cloud
- **Template**: `wedabuilder`

## Hardware Specifications

**WISE-4012**:
- 4x Analog Input channels (16-bit)
- 2x Analog Output channels (12-bit)
- Modbus TCP/RTU protocol
- Industrial-grade (-25°C to +75°C)

## Prerequisites

1. .NET 10.0 SDK installed
2. WISE-4012 hardware module
3. Network connection to WISE-4012
4. Weda EdgeSync Cloud access (or use Mock Cloud)

## Quick Start

### Step 1: Configure Connection

Edit `appsettings.json` to match your WISE-4012 configuration:

```json
{
  "WedaNode": {
    "Url": "nats://your-cloud-server:4224"
  },
  "DeviceConfigs": {
    "MyFirstDeviceConfig": {
      "DeviceName": "FirstWiseDevice4012",
      "Communication": {
        "Host": "192.168.1.100",  // Your WISE-4012 IP address
        "Port": 502,
        "SlaveId": 1
      }
    }
  }
}
```

### Step 2: Run the Example

```bash
dotnet run

# Output:
# [16:45:12 INF] Device started successfully
# [16:45:13 INF] channel.0: 1024
# [16:45:13 INF] channel.1: 2048
# [16:45:13 INF] channel.2: 512
# [16:45:13 INF] channel.3: 3072
```

## Code Structure

### Program.cs - Builder Pattern

```csharp
var builder = WedaApplication.CreateDefaultBuilder(args);

// Load device configuration from appsettings.json
builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");

var app = builder.Build();
await app.RunAsync();
```

### appsettings.json - Configuration

The configuration file defines:
- **Serilog**: Logging configuration
- **Nats**: Cloud connection settings
- **DeviceConfigs**: Device and sensor configuration
  - Device metadata (name, model, DTDL path)
  - Communication settings (IP, port, slave ID)
  - Sensor definitions (4 analog input channels)

### DTDL Metadata

The example uses DTDL (Digital Twin Definition Language) for metadata:
- Located in `assets/dtdl/dtmi/advantech/edgesync/wise-4012.json`
- Defines device capabilities and telemetry schema
- Enables digital twin integration

## Features Demonstrated

### 1. Builder Pattern
- Fluent API configuration
- Dependency injection
- Automatic lifecycle management
- Hosted service pattern

### 2. Real Hardware Integration
- Modbus TCP communication
- Multi-channel analog input reading
- 1-second polling interval
- Production-ready error handling

### 3. Cloud Integration
- NATS-based cloud connection
- Telemetry upload (uplink)
- Health reporting (uplink)
- Command receiving (downlink)
- Configuration updates (downlink)

### 4. DTDL Support
- Digital twin metadata
- Structured telemetry schema
- Device capabilities definition

## Configuration Options

### Modbus Communication

```json
"Communication": {
  "Host": "192.168.1.100",   // WISE-4012 IP address
  "Port": 502,               // Modbus TCP port (default: 502)
  "SlaveId": 1               // Modbus slave ID (default: 1)
}
```

### Sensor Configuration

Each sensor (analog input channel) can be configured:

```json
{
  "Name": "channel.0",
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
    "Interval": 1000  // Polling interval in milliseconds
  }
}
```

## Pattern Comparison

### wise-4012 vs wise-4012-builder

| Feature | wise-4012 | wise-4012-builder |
|---------|-----------|-------------------|
| Pattern | Manual Context | Builder Pattern |
| DI Container | No | Yes |
| Hosted Services | Manual | Automatic |
| Configuration | Manual loading | Auto from appsettings.json |
| Best For | Learning, debugging | Production, multiple devices |

## Troubleshooting

### Cannot connect to WISE-4012

```bash
# Check network connectivity
ping 192.168.1.100

# Check Modbus TCP port is open
telnet 192.168.1.100 502
```

### No telemetry data

1. Verify sensor configuration in `appsettings.json`
2. Check register addresses match WISE-4012 documentation
3. Ensure `Enabled: true` for sensors
4. Check logs for communication errors

### Cloud connection issues

1. Verify NATS server URL is correct
2. Check network connectivity to cloud
3. Review credentials if authentication is enabled
4. Check logs for connection errors

## Related Examples

- **[wise-4012](../wise-4012/)** - Manual Context pattern version
- **[wise-4012-isensing](../wise-4012-isensing/)** - iSensing diagnostic features

## Documentation

- [Weda SubNode SDK Wiki](../../docs/wiki/en/README.md)
- [Builder Pattern Guide](../../docs/wiki/en/02_core_concepts/wedaapplication_builder.md)
- [DTDL Integration](../../docs/wiki/en/03_advanced/dtdl_integration.md)

## Next Steps

1. **Add Transform Pipeline**: See [Data Pipeline - Transformations](../../docs/wiki/en/05-data-pipeline/02-transformations.md)
2. **Add DSP Filters**: See [Data Pipeline - DSP Filters](../../docs/wiki/en/05-data-pipeline/03-dsp-filters.md)
3. **Multiple Devices**: Add more devices using `builder.AddDevice<T>()`
4. **Custom Processing**: Override `OnDataReceived` in `MyFirstDevice.cs`

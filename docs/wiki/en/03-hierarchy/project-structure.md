---
sidebar_position: 3
sidebar_label: 'Project Structure'
hide_title: true
title: 'Project Structure - SubNode SDK Organization'
keywords: ['SubNode', 'Project Structure', 'SDK', 'Architecture']
description: 'Understanding the SubNode SDK project structure and module organization'
---

# Project Structure

> Understanding the SubNode SDK project structure and module organization.

## Repository Overview

```
edge_subnode/
├── src/                    # SDK source code
│   ├── Weda.SubNode.Abstractions/    # Interfaces and contracts
│   ├── Weda.SubNode.Core/            # Core implementations
│   ├── Weda.SubNode.Devices/         # Pre-built device types
│   ├── Weda.SubNode.Host/            # Application hosting
│   ├── Weda.SubNode.Cloud/           # Cloud integration
│   ├── Weda.SubNode.Simulators/      # Test simulators
│   └── Weda.SubNode.WebApi/          # REST API support
├── examples/               # Production-ready examples
├── templates/              # Project templates
├── tutorials/              # Learning tutorials
├── tests/                  # Unit and integration tests
└── docs/                   # Documentation
```

## SDK Modules

### Weda.SubNode.Abstractions

Core interfaces and contracts that define the SDK API.

```
Abstractions/
├── Commands/           # Command interfaces
│   ├── ICommand.cs
│   └── ICommandHandler.cs
├── Communication/      # Transport interfaces
│   ├── ICommunication.cs
│   ├── IRequestResponseCommunication.cs
│   ├── IPubSubCommunication.cs
│   └── IStreamingCommunication.cs
├── Context/            # Application context
│   └── IWedaApplicationContext.cs
├── Devices/            # Device interfaces
│   ├── IDevice.cs
│   └── DeviceConfiguration.cs
├── Events/             # Event types
│   ├── DataReceivedEvent.cs
│   └── ConnectionStateChangedEvent.cs
├── Protocols/          # Protocol parser interfaces
│   ├── IProtocolParser.cs
│   └── IRequestResponseProtocolParser.cs
├── Telemetry/          # Telemetry types
│   ├── Sensor.cs
│   ├── TelemetryMeasure.cs
│   └── SensorGroup.cs
└── Transforms/         # Transform interfaces
    └── ITelemetryTransform.cs
```

**Key interfaces:**

| Interface | Purpose |
|-----------|---------|
| `IDevice` | Device lifecycle and telemetry |
| `ICommunication` | Transport abstraction |
| `IProtocolParser` | Data encoding/decoding |
| `ITelemetryTransform` | Data transformation |
| `ICommandHandler` | Command processing |

### Weda.SubNode.Core

Implementation of core SDK functionality.

```
Core/
├── Communication/      # Transport implementations
│   ├── TcpCommunication.cs
│   ├── MqttCommunication.cs
│   └── HttpCommunication.cs
├── Devices/            # Base device classes
│   ├── DeviceBase.cs
│   ├── DeviceOrchestrator.cs
│   └── DeviceInitializer.cs
├── Protocols/          # Protocol implementations
│   └── Modbus/
│       ├── ModbusProtocolParser.cs
│       └── ModbusBatchReader.cs
└── Transforms/         # Transform implementations
    ├── CalibrationTransform.cs
    └── UnitConversionTransform.cs
```

### Weda.SubNode.Devices

Pre-built device classes for common protocols.

```
Devices/
├── Generic/
│   ├── TcpModbusDevice.cs      # Modbus TCP device
│   ├── RtuModbusDevice.cs      # Modbus RTU device
│   └── MqttDevice.cs           # MQTT device
└── Aggregator/
    └── AggregatorDevice.cs     # Multi-source aggregator
```

**Device class hierarchy:**

```
IDevice
    └── DeviceBase
            ├── TcpModbusDevice
            ├── RtuModbusDevice
            ├── MqttDevice
            └── AggregatorDevice
```

### Weda.SubNode.Host

Application hosting and lifecycle management.

```
Host/
├── WedaApplication.cs          # Main application class
├── WedaApplicationBuilder.cs   # Fluent builder
├── Context/
│   └── WedaApplicationContext.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

### Weda.SubNode.Cloud

Cloud integration services.

```
Cloud/
├── CloudService.cs             # Real cloud service
├── MockCloudService.cs         # Mock for testing
└── Messages/
    ├── TelemetryMessage.cs
    └── CommandMessage.cs
```

### Weda.SubNode.Simulators

Test simulators for development without hardware.

```
Simulators/
├── Modbus/
│   ├── TcpModbusSimulator.cs
│   └── TcpModbusSimulatorConfiguration.cs
├── Mqtt/
│   └── MqttSimulator.cs
└── Image/
    └── ImageSimulator.cs
```

## Application Project Structure

A typical SubNode application has this structure:

```
my-subnode-app/
├── Program.cs              # Entry point
├── MyDevice.cs             # Custom device class
├── devicecfg.json          # Device configuration
├── systemcfg.json          # System configuration
├── appsettings.json        # Logging configuration
└── MySubNode.csproj        # Project file
```

### Program.cs

The entry point using builder pattern:

```csharp
using Weda.SubNode.Host;

var builder = WedaApplication.CreateDefaultBuilder(args);
builder.AddDevice<MyDevice>("MyDevice");

var app = builder.Build();
await app.RunAsync();
```

### MyDevice.cs

Custom device implementation:

```csharp
public class MyDevice : TcpModbusDevice
{
    public MyDevice(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
    }

    // Override lifecycle methods as needed
    protected override async Task OnAfterStartAsync()
    {
        // Custom initialization
    }
}
```

### devicecfg.json

Device and sensor configuration:

```json
{
  "SubNode": {
    "Name": "MySubNode",
    "SubNodeType": "CustomDevice"
  },
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": true,
      "DeviceCommunication": { ... },
      "Sensors": [ ... ]
    }
  }
}
```

### Project File (.csproj)

Package references:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Weda.SubNode.Host" Version="1.0.0" />
    <PackageReference Include="Weda.SubNode.Devices" Version="1.0.0" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
  </ItemGroup>
</Project>
```

## Configuration Files

### Configuration Loading Order

Configuration is loaded in this order (later overrides earlier):

1. `appsettings.json` - Base logging configuration
2. `appsettings.{Environment}.json` - Environment-specific
3. `systemcfg.json` - System settings (WedaNode, cloud)
4. `devicecfg.json` - Device definitions
5. Environment variables
6. Command-line arguments

### File Purposes

| File | Purpose | Cloud Synced |
|------|---------|--------------|
| `appsettings.json` | Logging (Serilog) | No |
| `systemcfg.json` | WedaNode connection | No |
| `devicecfg.json` | Device/sensor config | Yes |

## Examples Directory

Production-ready examples demonstrating various scenarios:

| Example | Description |
|---------|-------------|
| `wise-4012/` | Basic Modbus TCP device |
| `power-aggregation/` | Multi-device data aggregation |
| `stock-monitor/` | HTTP API integration |
| `image-sensor/` | MQTT image streaming |
| `air-quality-monitor/` | Sensor fusion |

Each example includes:
- `Program.cs` - Application entry
- `devicecfg.json` - Working configuration
- Device class implementation
- README with specific instructions

## See Also

- [Sensor Configuration](./04-sensor-configuration/configuration-reference.md) - Configuration details
- [Custom Device Development](./07-custom-device/device-base.md) - Creating devices
- [Examples](./02-getting-started/start-with-example.md) - Running examples

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

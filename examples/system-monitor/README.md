# System Monitor Device Example

This example demonstrates how to create a custom device following the **Device Layered Architecture** pattern used throughout the Weda SubNode framework.

## Architecture Overview

The framework follows a **Device -> Parser -> Communication** three-layer architecture with clear separation of concerns:

```
┌────────────────────────────────────────────────────────────────────────────┐
│                        Device Layered Architecture                         │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌─────────────────────────┐     ┌─────────────────────────┐               │
│  │  LocalSystemMonitor     │     │   TcpModbusDevice       │  ← Concrete   │
│  │  Device                 │     │                         │    Device     │
│  │  (Transport Layer)      │     │  (Transport Layer)      │               │
│  │  Creates Communication  │     │ Creates TcpCommunication│               │
│  └───────────┬─────────────┘     └───────────┬─────────────┘               │
│              │ extends                       │ extends                     │
│              ▼                               ▼                             │
│  ┌─────────────────────────┐     ┌─────────────────────────┐               │
│  │  SystemMonitorDevice    │     │   ModbusDevice          │  ← Protocol   │
│  │  (Protocol Layer)       │     │  (Protocol Layer)       │    Device     │
│  │  Creates Parser         │     │  Creates ModbusParser   │               │
│  └───────────┬─────────────┘     └───────────┬─────────────┘               │
│              │ extends                       │ extends                     │
│              ▼                               ▼                             │
│  ┌─────────────────────────────────────────────────────────┐               │
│  │            RequestResponseDeviceBase                    │  ← Framework  │
│  │            (Orchestration Layer)                        │    Base       │
│  │            Handles telemetry, health, reconnection      │               │
│  └───────────────────────────────────────────┬─────────────┘               │
│                                              │ extends                     │
│                                              ▼                             │
│  ┌─────────────────────────────────────────────────────────┐               │
│  │                     DeviceBase                          │  ← Core       │
│  │            (Lifecycle & Connection Management)          │    Base       │
│  └─────────────────────────────────────────────────────────┘               │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
```

## Layered Responsibility

| Layer | Class | Responsibility | Creates |
|-------|-------|----------------|---------|
| **Transport** | `LocalSystemMonitorDevice` | Defines HOW to connect | `LocalSystemCommunication` |
| **Protocol** | `SystemMonitorDevice` | Defines HOW to parse data | `SystemMetricsParser` |
| **Framework** | `RequestResponseDeviceBase` | Orchestrates telemetry/health | - |
| **Core** | `DeviceBase` | Manages lifecycle | - |

## Design Pattern Comparison

### Modbus Device (Reference Implementation)

```csharp
// Transport Layer: Creates TCP communication
public class TcpModbusDevice : ModbusDevice
{
    public TcpModbusDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config, CreateTcpCommunication(context, config)) { }

    private static IRequestResponseCommunication<byte[], byte[]> CreateTcpCommunication(...)
        => new TcpCommunication(host, port, settings, logger);
}

// Protocol Layer: Creates Modbus parser
public class ModbusDevice : RequestResponseDeviceBase
{
    public ModbusDevice(IWedaApplicationContext context, DeviceConfiguration config,
                        IRequestResponseCommunication<byte[], byte[]> communication)
        : base(context, config, CreateModbusParser(context, config, communication)) { }

    private static IRequestResponseProtocolParser CreateModbusParser(...)
        => new ModbusRequestResponseParser(config, communication, logger, ...);
}
```

### System Monitor Device (This Example)

```csharp
// Transport Layer: Creates local system communication
public class LocalSystemMonitorDevice : SystemMonitorDevice
{
    public LocalSystemMonitorDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config, CreateLocalCommunication(context, config)) { }

    private static ICommunication CreateLocalCommunication(...)
        => new LocalSystemCommunication(settings, logger);
}

// Protocol Layer: Creates system metrics parser
public class SystemMonitorDevice : RequestResponseDeviceBase
{
    public SystemMonitorDevice(IWedaApplicationContext context, DeviceConfiguration config,
                               ICommunication communication)
        : base(context, config, CreateParser(context, config, communication)) { }

    private static IRequestResponseProtocolParser CreateParser(...)
        => new SystemMetricsParser(config, communication, logger);
}
```

## Benefits of This Architecture

### 1. Separation of Concerns
Each layer has a single responsibility:
- **Communication**: HOW to connect (TCP, Serial, SSH, Local APIs)
- **Parser**: HOW to interpret data (Modbus, System Metrics, OPC-UA)
- **Device**: Orchestration and lifecycle management

### 2. Easy Extension
To add remote system monitoring via SSH, simply create a new transport layer:

```csharp
public class SshSystemMonitorDevice : SystemMonitorDevice
{
    public SshSystemMonitorDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config, CreateSshCommunication(context, config)) { }

    private static ICommunication CreateSshCommunication(...)
        => new SshCommunication(host, username, password, logger);
}
```

### 3. Testability
Each layer can be tested independently:
- Mock `ICommunication` to test `SystemMetricsParser`
- Mock `IRequestResponseProtocolParser` to test `SystemMonitorDevice`

### 4. Reusability
The same parser can work with different transports:
- `LocalSystemCommunication` for local monitoring
- `SshCommunication` for remote Linux servers
- `WmiCommunication` for remote Windows servers

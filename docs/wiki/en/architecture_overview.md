---
title: Architecture Overview
category: architecture
order: 1
parent: null
related:
  - path: device_lifecycle.md
    title: Device Lifecycle
  - path: telemetry_transform.md
    title: Transform Pipeline
  - path: modbus_scanner.md
    title: Modbus Scanner
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Weda SubNode SDK Architecture Overview - Understanding the layered architecture and design principles
tags: [Architecture, Design, Layers, Abstractions]
---

# Architecture Overview

This document explains the architectural design of Weda SubNode SDK, including the layered structure, design principles, and component relationships.

## Architecture Principles

The SDK follows these core principles:

### Separation of Concerns
Each layer has clear responsibilities and does not mix concerns. Communication logic is separated from protocol parsing, which is separated from business logic.

### Dependency Inversion
Higher layers depend on abstractions (interfaces) rather than concrete implementations. This allows for easy extension and testing.

### Open/Closed Principle
The SDK is open for extension but closed for modification. You can add new devices, protocols, and transforms without modifying existing code.

### Single Responsibility
Each class and component has a single, well-defined responsibility.

## Layered Architecture

The SDK consists of five distinct layers:

```
┌─────────────────────────────────────────────────┐
│                Host Layer                       │
│  - Application Framework                        │
│  - Configuration Management                     │
│  - Dependency Injection                         │
└────────────────┬────────────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────────────┐
│               Devices Layer                     │
│  - Concrete Device Implementations              │
│  - TcpModbusDevice, Adam6052, etc.             │
└────────────────┬────────────────────────────────┘
                 │ uses
┌────────────────▼────────────────────────────────┐
│                Core Layer                       │
│  - Base Implementations                         │
│  - DeviceBase, ModbusDevice                     │
│  - Transforms, Filters                          │
└────────────────┬────────────────────────────────┘
                 │ implements
┌────────────────▼────────────────────────────────┐
│           Abstractions Layer                    │
│  - Interfaces and Contracts                     │
│  - IDevice, ICommunication, etc.                │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│               Cloud Layer                       │
│  - Cloud Platform Integration                   │
│  - NATS-based Communication                     │
└────────────────┬────────────────────────────────┘
                 │ implements
┌────────────────▼────────────────────────────────┐
│           Abstractions Layer                    │
│  - IWedaCloudService                            │
└─────────────────────────────────────────────────┘
```

## Layer Details

### 1. Abstractions Layer

**Location**: `Weda.SubNode.Abstractions`

**Purpose**: Defines all interfaces and contracts without any implementation.

**Key Components**:
- **IDevice**: Core device interface
- **ICommunication**: Communication abstraction
- **IProtocolParser**: Protocol parsing interface
- **ITelemetryTransform**: Data transformation interface
- **IDspFilter**: Digital signal processing interface
- **IWedaCloudService**: Cloud service interface

**Characteristics**:
- No dependencies on other layers
- Contains only interfaces and data models
- Defines events and data structures

### 2. Core Layer

**Location**: `Weda.SubNode.Core`

**Purpose**: Provides base implementations and common functionality.

**Key Components**:
- **DeviceBase**: Base device implementation with lifecycle management
- **ModbusDevice**: Modbus protocol device implementation
- **CommunicationBase**: Base communication with retry and timeout
- **TelemetryTransformPipeline**: Composable data transformation
- **DSP Filters**: KalmanFilter, MovingAverageFilter, etc.

**Characteristics**:
- Implements interfaces from Abstractions
- Provides extensible base classes
- Contains common utilities and helpers

### 3. Devices Layer

**Location**: `Weda.SubNode.Devices`

**Purpose**: Concrete device implementations ready to use.

**Key Components**:
- **TcpModbusDevice**: Modbus TCP device
- **Adam6052**: Advantech ADAM-6052 specific implementation
- **Custom Devices**: User-defined devices

**Characteristics**:
- Extends Core layer classes
- Provides vendor-specific implementations
- Includes default configurations

### 4. Cloud Layer

**Location**: `Weda.SubNode.Cloud`

**Purpose**: Cloud platform integration.

**Key Components**:
- **WedaCloudService**: NATS-based cloud communication
- **DeviceAgentClient**: Device management client
- **TelemetryClient**: Telemetry upload client
- **FileDeviceIdRepository**: Local device ID storage

**Characteristics**:
- Independent of device implementations
- Handles device registration
- Manages telemetry upload and health reporting

### 5. Host Layer

**Location**: `Weda.SubNode.Host`

**Purpose**: Application framework and hosting.

**Key Components**:
- **WedaApplication**: Main application class
- **WedaApplicationBuilder**: Builder pattern for configuration
- **DeviceHostedService**: Background service for devices
- **LoggingConfigurator**: Logging setup

**Characteristics**:
- ASP.NET Core-style API
- Configuration-based device setup
- Dependency injection container

## Data Flow Architecture

### Device Initialization Flow

```
WedaApplication
    │
    ├─> Load Configuration (appsettings.json)
    │
    ├─> Create Device Instances
    │   └─> TcpModbusDevice
    │       ├─> ICommunication (TcpCommunication)
    │       ├─> IProtocolParser (ModbusProtocolParser)
    │       └─> IWedaCloudService (WedaCloudService)
    │
    ├─> Initialize Devices
    │   └─> IDevice.InitializeAsync()
    │       ├─> Connect to physical device
    │       ├─> Register with cloud
    │       └─> Subscribe to cloud events
    │
    └─> Start Devices
        └─> IDevice.StartAsync()
            ├─> Start telemetry reading loop
            └─> Start health reporting loop
```

### Telemetry Data Flow

```
Physical Device
    │
    ▼
ICommunication.ReadAsync()
    │ (returns byte[])
    ▼
IProtocolParser.Parse()
    │ (returns typed values)
    ▼
ITelemetryTransform.ExecuteAsync()
    │ (applies calibration, unit conversion)
    ▼
IDspFilter.ApplyAsync()
    │ (applies filtering)
    ▼
TelemetryData
    │
    ▼
IWedaCloudService.SendTelemetryAsync()
    │
    ▼
Weda Cloud
```

## Extension Points

The SDK provides multiple extension points:

### 1. Custom Device Implementation

```csharp
public class MyCustomDevice : ModbusDevice
{
    protected override async Task<TelemetryData> ReadTelemetryAsync(
        CancellationToken cancellationToken = default)
    {
        // Custom reading logic
        var data = await base.ReadTelemetryAsync(cancellationToken);

        // Post-processing
        return data;
    }
}
```

### 2. Custom Communication Protocol

```csharp
public class MyCustomCommunication : CommunicationBase
{
    protected override async Task<bool> ConnectCoreAsync(
        CancellationToken cancellationToken = default)
    {
        // Custom connection logic
    }

    protected override async Task<byte[]> ReadCoreAsync(
        CancellationToken cancellationToken = default)
    {
        // Custom read logic
    }
}
```

### 3. Custom Transform

```csharp
public class MyTransform : ITelemetryTransform
{
    public Task<List<TelemetryMeasure>> ExecuteAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Transform logic
    }
}
```

### 4. Custom DSP Filter

```csharp
public class MyFilter : IDspFilter
{
    public Task<object> ApplyAsync(
        object value,
        CancellationToken cancellationToken = default)
    {
        // Filter logic
    }
}
```

## Design Patterns Used

### Builder Pattern
Used in `WedaApplicationBuilder` for fluent configuration.

### Factory Pattern
Used in `WedaFactory` for creating instances.

### Strategy Pattern
Used in `ITelemetryTransform` and `IDspFilter` for pluggable algorithms.

### Observer Pattern
Used in event system for device lifecycle events.

### Template Method Pattern
Used in `DeviceBase` for defining device lifecycle.

### Repository Pattern
Used in `IDeviceIdRepository` for device ID storage.

## Thread Safety

### Concurrent Operations
- Each device runs in its own background task
- Multiple devices can operate in parallel
- Communication operations are thread-safe

### State Management
- Device state is protected with locks
- Event handlers are invoked asynchronously
- Cancellation tokens are used for graceful shutdown

## Performance Considerations

### Resource Usage
- Connection pooling for communication
- Efficient memory usage with object pooling
- Minimal allocations in hot paths

### Scalability
- Supports hundreds of devices in single process
- Configurable periods for telemetry and health
- Batch telemetry upload for efficiency

## Testing Strategy

### Unit Testing
- Test each layer independently
- Mock dependencies using interfaces
- Test business logic without I/O

### Integration Testing
- Test communication with real devices
- Test cloud integration with test environment
- Test complete data flow

### Example Test Structure

```csharp
public class DeviceBaseTests
{
    private readonly Mock<ICommunication> _communicationMock;
    private readonly Mock<IWedaCloudService> _cloudServiceMock;

    [Fact]
    public async Task StartAsync_ShouldStartTelemetryLoop()
    {
        // Arrange
        var device = new TestDevice(...);

        // Act
        await device.StartAsync();

        // Assert
        // Verify telemetry reading started
    }
}
```

## Summary

The Weda SubNode SDK architecture:
- Follows SOLID principles
- Uses layered architecture for separation of concerns
- Provides clear extension points
- Supports testing at all levels
- Scales to manage multiple devices efficiently


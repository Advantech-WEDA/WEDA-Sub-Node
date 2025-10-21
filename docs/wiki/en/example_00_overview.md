# Weda SubNode SDK - Getting Started Guide

Welcome to the Weda SubNode SDK! This guide will take you from zero to building production-ready IoT edge devices.

## What is Weda SubNode SDK?

Weda SubNode SDK is a powerful .NET framework for building industrial IoT edge applications that:
- **Connect** to devices via multiple protocols
- **Collect** telemetry data from sensors and equipment
- **Process** data with transformations, and DSP filters
- **Transmit** to cloud services via NATS messaging
- **Monitor** device health and handle errors gracefully

## Quick Overview

### SDK Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Your Application                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Device 1   │  │   Device 2   │  │   Device N   │  ...  │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
└─────────────────────────────────────────────────────────────┘
                            │ multiple protocols
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  Weda SubNode Host                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Device Lifecycle Manager                            │   │
│  │  - Initialization, Start/Stop, Error Handling        │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Telemetry Pipeline                                  │   │
│  │  - Data Collection → Transform → Filter → Send       │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Communication Layer                                 │   │
│  │  - Modbus TCP/RTU, TCP, Serial, etc.                 │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                            │ NATS
                            ▼
                    ┌───────────────┐
                    │     Cloud     │
                    └───────────────┘
```

### Key Features

**Configuration-Driven**
- Define devices in JSON, no coding required for basic scenarios
- Hot-reload configuration changes
- Support for multiple devices

**Protocol Support**
- Modbus TCP (built-in)
- Extensible for custom protocols
- Automatic connection management and retry

**Data Processing**
- Custom transformation pipeline (Calibration, Unit conversion, etc.)
- DSP filters (Kalman, Moving Average, etc.)

**Extensibility**
- Event-driven hooks for lifecycle events
- Custom business logic integration
- Database persistence support

**Production-Ready**
- Health monitoring and reporting
- Circuit breaker and retry patterns
- Comprehensive logging with Serilog
- State machine for device lifecycle

**Developer-Friendly**
- Project templates (`dotnet new`)
- Extensive examples
- Sandbox mode for function development

## Learning Path

This guide follows a structured learning path:

### Getting Started (15 minutes)
**[01. Quick Start](01_quick_start.md)** - Build your first device
- Install templates
- Create a project
- Configure and run

### Development & Testing (20 minutes)
**[02. Sandbox Testing](02_sandbox_testing.md)** - Test without hardware
- Use Modbus simulators
- Mock cloud services
- Write unit tests

### Data Processing (30 minutes each)
**[03. Data Transformations](03_add_transformation.md)** - Process sensor data
- Built-in transforms (calibration, unit conversion)
- Custom transformations
- Pipeline configuration

**[04. DSP Filters](04_add_dsp_filters.md)** - Signal processing
- Noise reduction
- Kalman filtering
- Custom filters

### Advanced Features (30 minutes)
**[05. Hooks & Handlers](05_using_hooks.md)** - Extend functionality
- Lifecycle hooks
- Data pipeline hooks
- Database persistence
- Custom business logic

## Prerequisites

Before you begin, ensure you have:

- ** .NET 9.0 SDK** or later
  ```bash
  dotnet --version  # Should be 9.0.x or higher
  ```

- ** Code Editor** (VS Code, Visual Studio, or Rider)

- ** Basic C# Knowledge** - Understanding of classes, async/await, events

- **Optional: Modbus Device or Simulator**
  - Physical device (e.g., Advantech WISE-4012, PLCs)
  - Software simulator (ModbusPal, pyModSlave)
  - Our built-in simulator example

- **Optional: NATS Server** (for cloud integration)
  ```bash
  # Install NATS server
  # macOS: brew install nats-server
  # Linux: See https://docs.nats.io/running-a-nats-service/introduction/installation

  # Start NATS with JetStream
  nats-server -js
  ```

## Installation

### Step 1: Clone the Repository (for SDK source)

```bash
git clone https://github.com/your-org/weda-subnode-sdk.git
cd weda-subnode-sdk
```

### Step 2: Install Templates

Run the installation script:

```bash
bash scripts/install-templates.sh
```

This installs three project templates:
- `subnode` - Custom device (inherits from ModbusDevice)
- `wedaapi` - Simple application (auto-configuration)
- `wedaapi-c` - Advanced application (full control)

Verify installation:
```bash
dotnet new list | grep -i subnode
```

You should see:
```
Weda SubNode Custom Device    subnode    [C#]  Console/IoT/Weda/SubNode/Custom
Weda SubNode API (Simple)     wedaapi    [C#]  Weda/SubNode/IoT
Weda SubNode API (Advanced)   wedaapi-c  [C#]  Weda/SubNode/IoT/Advanced
```

### Step 3: Verify Installation

Create a test project:
```bash
cd /tmp
dotnet new subnode -n TestDevice
cd TestDevice
dotnet build
```

If it builds successfully, you're all set! [SUCCESS]

## Your First Device (3 Minutes)

Let's create a simple device that reads temperature from a Modbus sensor:

```bash
# Create project
dotnet new subnode -n MyFirstDevice
cd MyFirstDevice

# Edit appsettings.json (update IP address)
# Edit MyFirstDevice.cs (customize if needed)

# Run
dotnet run
```

That's it! See **[01. Quick Start](01_quick_start.md)** for detailed walkthrough.

## Project Structure

When you create a project with `dotnet new subnode`, you get:

```
MyFirstDevice/
├── MyFirstDevice.cs          # Your device implementation
├── Program.cs                 # Application entry point
├── appsettings.json          # Device configuration
├── MyFirstDevice.csproj      # Project file
└── .gitignore
```

**Key Files:**

- **`MyFirstDevice.cs`** - Inherits from `TcpModbusDevice`, handles data events
- **`Program.cs`** - Bootstraps the application, minimal code needed
- **`appsettings.json`** - Configure connection, sensors, polling intervals

## Common Use Cases

### Use Case 1: Simple Data Collector
**Goal**: Read sensors and send to cloud
**Template**: `dotnet new wedaapi`
**Time**: 5 minutes
**Guide**: [01. Quick Start](01_quick_start.md)

### Use Case 2: Data Processing Edge Device
**Goal**: Filter, transform, and process data locally
**Template**: `dotnet new subnode`
**Time**: 30 minutes
**Guides**:
- [03. Transformations](03_add_transformation.md)
- [04. DSP Filters](04_add_dsp_filters.md)

### Use Case 3: Custom Logic & Persistence
**Goal**: Add business logic, save to database
**Template**: `dotnet new wedaapi-c`
**Time**: 1 hour
**Guide**: [05. Hooks](05_using_hooks.md)

### Use Case 4: Multi-Device Gateway
**Goal**: Manage multiple devices
**Template**: `dotnet new wedaapi`
**Time**: 15 minutes
**Guide**: [01. Quick Start](01_quick_start.md) - Multiple Devices section

## Three Ways to Use the SDK

### 1. Simple API (Recommended for Beginners)

```csharp
using Weda.SubNode.Host;

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
```

- Everything configured in `appsettings.json`
- Automatic device discovery and initialization
- Built-in logging and error handling

### 2. Custom Device API (Recommended for Custom Logic)

```csharp
public class MyDevice : TcpModbusDevice
{
    protected override void OnDataReceived(DataReceivedEvent e)
    {
        // Your custom logic here
    }
}
```

- Inherit from base device classes
- Override specific methods
- Full control over device behavior

### 3. Advanced Builder API (Full Control)

```csharp
var builder = WedaApplication.CreateBuilder(args);

// Configure services
builder.Services.AddSingleton<IMyService, MyService>();

// Customize logging
builder.Logging.AddFilter("Weda.SubNode", LogLevel.Debug);

var app = builder.Build();
await app.RunAsync();
```

- Manual service registration
- Custom logging configuration
- Full dependency injection control

## Example Scenarios

### Read Temperature Every Second

**appsettings.json:**
```json
{
  "DeviceConfigs": {
    "TempSensor": {
      "ConnectionSettings": { "Host": "192.168.1.100", "Port": 502 },
      "PollingInterval": 1000,
      "Sensors": [
        {
          "ResourceId": "temp",
          "Name": "Temperature",
          "RegisterAddress": 0,
          "DataType": "Float32"
        }
      ]
    }
  }
}
```

### Convert Celsius to Fahrenheit

**appsettings.json:**
```json
{
  "Sensors": [
    {
      "ResourceId": "temp",
      "Transforms": [
        {
          "Type": "UnitConversion",
          "FromUnit": "celsius",
          "ToUnit": "fahrenheit"
        }
      ]
    }
  ]
}
```

### Apply Kalman Filter for Noise Reduction

**appsettings.json:**
```json
{
  "Sensors": [
    {
      "ResourceId": "vibration",
      "DspFilters": [
        {
          "Type": "Kalman",
          "ProcessNoise": 0.01,
          "MeasurementNoise": 0.1
        }
      ]
    }
  ]
}
```

## What's Next?

Now that you understand the basics, dive into the guides:

1. **[Quick Start](01_quick_start.md)** - Create your first device (15 min)
2. **[Sandbox Testing](02_sandbox_testing.md)** - Test without hardware (20 min)
3. **[Transformations](03_add_transformation.md)** - Process data (30 min)
4. **[DSP Filters](04_add_dsp_filters.md)** - Signal processing (30 min)
5. **[Hooks](05_using_hooks.md)** - Extend functionality (30 min)

## Getting Help

- **Documentation**: You're reading it!
- **Examples**: Check `examples/` folder in the repository

## Summary

**Weda SubNode SDK makes IoT edge development:**
- **Fast** - Templates get you started in minutes
- **Simple** - Configuration-driven for common scenarios
- **Flexible** - Extensible for custom requirements
- **Production-Ready** - Built-in resilience and monitoring
- **Testable** - Easy to test without hardware

**Ready to build?** Start with **[Quick Start Guide →](01_quick_start.md)**

---
**Version**: 1.0.0
**Last Updated**: 2025-10-20
**Next**: [Quick Start →](01_quick_start.md)

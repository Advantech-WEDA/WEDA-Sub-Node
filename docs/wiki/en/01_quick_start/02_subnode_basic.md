---
title: "Build Custom Device with subnode Template"
description: "Create a console-style SubNode application for single device development"
author: "Rain Hu"
date: "2025-11-12"
lang: "en"
parent: "README"
prev: "01_install_templates"
next: "03_wedaapi_basic"
translations:
  - lang: "zh"
    path: "../../zh/01_quick_start/02_subnode_basic.md"
examples:
  - "examples/basic-modbus"
templates:
  - "subnode"
---

# Build Custom Device with subnode Template

Learn how to create an IoT device application using the `subnode` template. `subnode` is a **console-style template**, ideal for single device development and debugging.

## Template Analogy

- **subnode** ↔ **Console App**: Ideal for single device development and debugging
- **wedaapi** ↔ **Web API**: Production-ready for managing multiple devices at once

**Time Required**: 15 minutes
**Difficulty**: Intermediate
**Target Audience**: Developers familiar with OOP

---

## Prerequisites

Before starting, ensure you have:

- ✅ .NET 9.0 SDK installed
- ✅ Weda SubNode templates installed (see [Install Templates](01_install_templates.md))
- ✅ Basic understanding of C# object-oriented programming

---

## Step 1: Create Project

Create a new project using the `subnode` template:

```bash
# Create project directory
mkdir -p devices/HelloFirstDevice
cd devices/HelloFirstDevice

# Create project from template
dotnet new subnode -n HelloFirstDevice
```

**Expected Output**:
```
The template "Weda SubNode Custom Device" was created successfully.
```

### Add to Solution and Restore (Recommended)

If you're working within a larger solution:

```bash
# Navigate to solution root
cd ../../

# Add project to solution
dotnet sln add devices/HelloFirstDevice/HelloFirstDevice.csproj

# Restore dependencies
dotnet restore

# Navigate back to project directory
cd devices/HelloFirstDevice
```

This ensures all dependencies are properly resolved and the project is integrated with your solution.

### Project Structure

```
HelloFirstDevice/
├── MyFirstDevice.cs          # Custom device class
├── Program.cs                # Application entry point
├── appsettings.json          # Serilog logging configuration
└── HelloFirstDevice.csproj   # Project file
```

---

## Step 2: Understand Core Concepts

This step provides an in-depth explanation of the subnode template architecture. Understanding these concepts will enable you to easily extend and customize your devices.

### 2.1 MyFirstDevice.cs - Custom Device Class (Complete Analysis)

Open `MyFirstDevice.cs`, let's explain line by line:

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to DataReceived event to process telemetry
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogInformation("📊 Telemetry received:");

        foreach (var measure in e.Data)
        {
            _logger.LogInformation("  - {Name}: {Value}",
                measure.Name,
                measure.Value);
        }
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}
```

#### 🔑 Key Concepts Explained

##### 1. **Inheriting TcpModbusDevice - Code You Don't Need to Write**

When you inherit from `TcpModbusDevice`, the SDK automatically handles:

| Feature | SDK Auto-Handles | You Don't Need To |
|---------|-----------------|-------------------|
| TCP Connection Management | ✅ Auto-connect, reconnect, disconnect detection | ❌ Don't manage connections manually |
| Modbus Protocol | ✅ Read/write holding registers, coils, etc. | ❌ Don't implement Modbus protocol |
| Data Parsing | ✅ Parse raw bytes to float, int, etc. | ❌ Don't manually convert bytes |
| Error Retry | ✅ Auto-retry with circuit breaker | ❌ Don't handle transient errors |
| Lifecycle | ✅ Initialize → Start → Stop → Dispose | ❌ Don't manage state machine |

##### 2. **Event Subscription Pattern - Code You Need to Write**

The SDK provides these events that you can subscribe to:

```csharp
public MyFirstDevice(IWedaApplicationContext context, DeviceConfiguration config)
    : base(context, config)
{
    // ★ These are SDK-provided events you can subscribe to ★

    DataReceived += OnDataReceived;                              // Telemetry data received
    CommandReceived += OnCommandReceived;                        // Cloud command received
    ConnectionStateChanged += OnConnectionStateChanged;          // Connection state changed
    DeviceStatusChanged += OnDeviceStatusChanged;                // Device status changed
    ConfigurationUpdateReceived += OnConfigurationUpdateReceived; // Configuration update received
}
```

**When to subscribe? When not to?**

| Event | When to Subscribe | Usage Examples |
|-------|------------------|----------------|
| `DataReceived` | If you need to log telemetry locally | Process data, check thresholds, local storage |
| `CommandReceived` | If you need cloud control | Receive remote commands, execute actions |
| `ConnectionStateChanged` | If you need to monitor connection | Log disconnections, send notifications |
| `DeviceStatusChanged` | If you need status tracking | Monitor device health |
| `ConfigurationUpdateReceived` | If you support dynamic config | Hot-update device parameters |

##### 3. **Constructor Parameters - SDK vs Your Responsibilities**

```csharp
public MyFirstDevice(
    IWedaApplicationContext context,      // Provided by SDK
    DeviceConfiguration configuration)    // Provided by you
```

**IWedaApplicationContext - SDK's Runtime Environment**
- ✅ Logger Factory: For logging
- ✅ Cloud Service: Connect to Weda.Core or Mock
- ✅ Configuration: Read appsettings.json

**DeviceConfiguration - Your Device Settings**
- 📝 DeviceName: Device identification (required)
- 📝 Host/Port: Modbus server address (required)
- 📝 Sensors: Sensor definitions (required)
- 📝 Properties: Custom properties (optional)

#### 🎯 Future Extension Guide

##### Scenario 1: I want to connect to a real Modbus device

**Code to modify:** `ConfigureDeviceConfiguration()` in `Program.cs`

```csharp
var modbusDeviceConfig = new TcpModbusDeviceConfiguration
{
    Host = "192.168.1.100",  // ← Change to real device IP
    Port = 502,              // ← Change to real device Port
    SlaveId = 1              // ← Change to real device Slave ID
};
```

**Code you don't need to modify:** `MyFirstDevice.cs` (completely untouched!)

##### Scenario 2: I want to add custom business logic

**Code to modify:** Event handlers in `MyFirstDevice.cs`

```csharp
private void OnDataReceived(object? sender, DataReceivedEvent e)
{
    // ★ Add your logic here ★

    // Example 1: Alert when temperature exceeds threshold
    var temp = e.Data.FirstOrDefault(m => m.Name == "temperature.sensor");
    if (temp?.Value is double t && t > 30.0)
    {
        SendAlert($"Temperature too high: {t}°C");
    }

    // Example 2: Write to local database
    await _database.SaveTelemetryAsync(e.Data);

    // Example 3: Trigger other systems
    await _notificationService.NotifyAsync(e.Data);
}
```

**Code you don't need to modify:** SDK's communication, parsing, lifecycle management

##### Scenario 3: I want to connect to real Weda.Core cloud service

**Code to modify:** CloudService configuration in `Program.cs`

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ✅ Method 1: Remove Mock (Simplest!)
    // SDK will auto-connect to NATS (default: nats://localhost:4222)
    // ❌ Remove this line
    // options.CloudService = WedaFactory.Cloud.Mock;

    // 🟡 Method 2: Configure via NatsConnectionSettings (Recommended)
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://172.22.160.197:4224",
        CredFile = "",  // If authentication needed, provide .creds file path
    };
});
```

**💡 SDK Default Behavior:**
- If **CloudService is not set**, SDK auto-creates real NATS connection
- Default connects to `nats://localhost:4222`
- Can adjust connection settings via `options.NatsConnectionSettings`

**📝 Method 3: Read from appsettings.json (Recommended for production)**

Add the `Nats` configuration section to `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      }
    ]
  },
  "Nats": {
    "Url": "nats://172.22.160.197:4224",
    "CredFile": "",
    "Name": "default",
    "SerializerType": "json"
  }
}
```

Then in `Program.cs`, **just remove Mock - SDK auto-loads appsettings.json**:

```csharp
using var context = new WedaApplicationContext(options =>
{
    // ✅ SDK automatically loads appsettings.json
    // - Automatically reads Serilog section to create LoggerFactory
    // - Automatically reads Nats section to create CloudService
    // No need to manually create anything!

    // ❌ Remove Mock
    // options.CloudService = WedaFactory.Cloud.Mock;
});
```

**💡 SDK Auto-Load Behavior (Significantly Improved):**
- SDK **automatically** loads `appsettings.json` from the current directory
- Automatically reads `Serilog` section to create LoggerFactory
- Automatically reads NATS connection settings from the `Nats` section
- **No need** to manually create `ConfigurationBuilder`, `IConfiguration`, or `LoggerFactory`
- **Optional Override**: Manually set `options.LoggerFactory` or `options.NatsConnectionSettings` will take precedence

**Code you don't need to modify:** `MyFirstDevice.cs`, data collection logic

##### Scenario 4: I want to change communication protocol (not Modbus)

**Code to modify:** Inherit from different base class

```csharp
// Original: Modbus TCP
public class MyFirstDevice : TcpModbusDevice { }

// Change to: MQTT
public class MyFirstDevice : MqttDevice { }

// Change to: OPC UA
public class MyFirstDevice : OpcUaDevice { }

// Change to: Fully custom
public class MyFirstDevice : DeviceBase { }  // Requires more implementation
```

#### 📊 Code Classification Summary

| Code Area | Owner | Modification Frequency | Purpose |
|-----------|-------|----------------------|---------|
| `TcpModbusDevice` base class | SDK | ❌ Never modify | Protocol implementation |
| `MyFirstDevice` constructor | You | 🟡 Occasionally (add event subscriptions) | Event registration |
| Event Handlers (OnXxx) | You | ✅ Frequently | Business logic |
| `ConfigureDeviceConfiguration()` | You | ✅ Frequently | Device configuration |
| `Program.cs` lifecycle management | SDK Template | 🟡 Occasionally (switch cloud) | Application startup |

### 2.2 Program.cs - Application Configuration (Complete Analysis)

`Program.cs` is the application entry point. Let's break down each part:

```csharp
// ===== Part 1: Configuration & Logging Initialization =====
// Standard .NET configuration pattern, usually no modification needed
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())       // Set base path
    .AddJsonFile("appsettings.json", optional: false)   // Load appsettings.json
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)              // Read from appsettings.json
    .CreateLogger();

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);
```

#### 📝 Part 1 Explanation

| Code | Purpose | Needs Modification? |
|------|---------|---------------------|
| `ConfigurationBuilder` | Read appsettings.json | ❌ No |
| `LoggerConfiguration` | Configure Serilog logging | ❌ No (adjust in appsettings.json) |
| `loggerFactory` | Logger factory for SDK | ❌ No |

```csharp
try
{
    // ===== Part 2: Simulator Configuration (Testing) =====
    // ★ For local testing only, remove when connecting real devices ★
    var simulator = await ConfigureTcpModbusSimulator();

    // ===== Part 3: Device Configuration =====
    // ★ Define your device parameters (Host, Port, Sensors) ★
    var deviceConfig = ConfigureDeviceConfiguration();

    // ===== Part 4: ApplicationContext Creation =====
    // ★ Configure SDK runtime environment ★
    using var context = new WedaApplicationContext(options =>
    {
        options.LoggerFactory = loggerFactory;           // Provide logger
        options.CloudService = WedaFactory.Cloud.Mock;   // ★ Cloud service switch ★
    });
```

#### 📝 Parts 2-4 Explanation

**Part 2: Simulator (Test Environment)**

```csharp
var simulator = await ConfigureTcpModbusSimulator();
```

| Purpose | When to Use | When to Remove |
|---------|-------------|----------------|
| Simulate Modbus device | ✅ Local development | ❌ Production deployment |
| Generate fake data | ✅ No real hardware | ❌ Connecting real devices |

**Part 3: Device Configuration**

```csharp
var deviceConfig = ConfigureDeviceConfiguration();
```

Defined at file bottom, contains:
- ✅ **Must modify**: When connecting real devices
- ✅ **Frequently modified**: Adjust Host/Port, add Sensors

**Part 4: ApplicationContext (Runtime Environment)**

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;        // ❌ No modification
    options.CloudService = WedaFactory.Cloud.Mock; // ✅ Modify when switching cloud
});
```

| Option | Purpose | When to Modify |
|--------|---------|----------------|
| `LoggerFactory` | Provide logging | ❌ Never |
| `CloudService` | Cloud connection | ✅ Mock → Real Cloud |

```csharp
    // ===== Part 5: Device Lifecycle Management =====
    var device = new MyFirstDevice(context, deviceConfig);  // Create instance
    await device.InitializeAsync();                         // Initialize (connect, register)
    await device.StartAsync();                              // Start (begin reading data)

    Log.Information("MyFirstDevice started. Press Ctrl+C to stop...");

    // ===== Part 6: Wait & Graceful Shutdown =====
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) =>
    {
        e.Cancel = true;   // Prevent immediate termination
        cts.Cancel();      // Trigger cancellation
    };
    await Task.Delay(Timeout.Infinite, cts.Token);  // Run until Ctrl+C

    // ===== Part 7: Graceful Shutdown =====
    await device.StopAsync();       // Stop reading data
    device.Dispose();               // Release resources
    await simulator.StopAsync();    // Stop Simulator (if any)
}
catch (OperationCanceledException)
{
    Log.Information("Application stopped");
}
finally
{
    await Log.CloseAndFlushAsync();  // Ensure all logs are written
}
```

#### 📝 Parts 5-7 Explanation

**Part 5: Device Lifecycle (Manual Management)**

```csharp
var device = new MyFirstDevice(context, deviceConfig);
await device.InitializeAsync();  // ← Step 1
await device.StartAsync();       // ← Step 2
```

| Method | Purpose | SDK Internal Behavior |
|--------|---------|----------------------|
| `new MyFirstDevice()` | Create instance | Subscribe events, prepare resources |
| `InitializeAsync()` | Initialize | Connect Modbus, register with cloud |
| `StartAsync()` | Start | Begin periodic data reading |

**Part 6: Wait Signal (Why this pattern?)**

```csharp
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;   // ← Critical! Prevents immediate termination
    cts.Cancel();
};
await Task.Delay(Timeout.Infinite, cts.Token);
```

| Code | Purpose | Why Needed |
|------|---------|------------|
| `e.Cancel = true` | Prevent default termination | Gives us time for graceful shutdown |
| `cts.Cancel()` | Trigger cancellation | Exit `Task.Delay` |
| `Task.Delay(Infinite)` | Wait indefinitely | Keep program running |

**Part 7: Graceful Shutdown**

```csharp
await device.StopAsync();       // ← Step 1: Stop background work
device.Dispose();               // ← Step 2: Release resources
await simulator.StopAsync();    // ← Step 3: Stop Simulator
```

**Why order matters?**
1. Stop reading data first (prevent new operations)
2. Release resources (close connections, unsubscribe events)
3. Stop Simulator last (ensure device fully stopped)

#### 🎯 Program.cs Extension Guide

##### Scenario 1: Remove Simulator, connect to real device

**Code to modify:**

```csharp
// ❌ Remove this line
// var simulator = await ConfigureTcpModbusSimulator();

// Modify DeviceConfiguration
var deviceConfig = ConfigureDeviceConfiguration();  // Change Host/Port inside

// Create device (unchanged)
var device = new MyFirstDevice(context, deviceConfig);
await device.InitializeAsync();
await device.StartAsync();

// ❌ Remove simulator shutdown
// await simulator.StopAsync();
```

##### Scenario 2: Switch to real cloud service

**Code to modify:**

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ❌ Remove or comment out Mock
    // options.CloudService = WedaFactory.Cloud.Mock;

    // ✅ Configure real NATS connection
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://your-nats-server:4222",
        CredFile = ""
    };
});
```

##### Scenario 3: Switch to HostedService pattern (like wedaapi)

If you want more automated lifecycle management, switch to `wedaapi` or `wedaapi-c` templates.

**subnode vs wedaapi comparison:**

| Feature | subnode | wedaapi |
|---------|---------|---------|
| Lifecycle | Manual management | Auto-managed (HostedService) |
| Control | ✅ Full control over each step | 🟡 Less control |
| Code Size | 🟡 More (~80 lines) | ✅ Minimal (3 lines) |
| Use Case | Learning, prototyping, full customization | Quick deployment, standard scenarios |

#### 📊 Program.cs Code Classification

| Code Section | Needs Modification | When | What to Change |
|--------------|-------------------|------|----------------|
| Configuration & Logging | ❌ Never | - | - |
| Simulator configuration | ✅ Yes | Connect real device | Remove or comment |
| DeviceConfiguration | ✅ Frequently | Every project | Host/Port/Sensors |
| CloudService | ✅ Yes | Deploy to production | Mock → NATS |
| Lifecycle management | ❌ Rarely | - | Unless changing architecture |
| Graceful Shutdown | ❌ Never | - | - |

---

### 2.3 Helper Methods - Configuration Helpers

At the bottom of `Program.cs`, there are two helper methods:

#### ConfigureDeviceConfiguration() - Device Configuration

```csharp
DeviceConfiguration ConfigureDeviceConfiguration()
{
    // ★ This is where you'll most frequently modify ★
    var modbusDeviceConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",     // ← Device name (shown in cloud)
        Manufacturer = "Advantech",    // ← Manufacturer
        Model = "CustomDevice-v1",     // ← Model
        Host = "127.0.0.1",           // ← ★ Change to real IP ★
        Port = 5020,                  // ← ★ Change to real Port ★
        SlaveId = 1                   // ← ★ Modbus Slave ID ★
    };

    // ★ Define sensors ★
    var tempSensor = new ModbusSensorConfiguration
    {
        Name = "temperature.sensor",                   // ← Sensor identifier
        Dtmi = "dtmi:advantech:EdgeSync:Temperature;1", // ← Digital Twin definition
        RegisterAddress = 0,                           // ← Modbus start address
        RegisterCount = 2,                             // ← Read 2 registers (float32)
        DataType = ModbusDataType.Float32,            // ← Data type
        RegisterType = ModbusRegisterType.HoldingRegister, // ← Register type
        SensorGroup = SensorGroup.TEMP                // ← Sensor group
    };

    modbusDeviceConfig.AddSensor(tempSensor);
    return modbusDeviceConfig.ToDeviceConfiguration();
}
```

**Modification Guide:**

| Field | Purpose | Example | When to Modify |
|-------|---------|---------|----------------|
| `Host` | Modbus device IP | "192.168.1.100" | ✅ Connect real device |
| `Port` | Modbus Port | 502 | ✅ If non-standard port |
| `SlaveId` | Modbus Slave ID | 1 | ✅ Per device configuration |
| `RegisterAddress` | Start address | 0, 100, 400 | ✅ Per device manual |
| `DataType` | Data type | Float32, Int16 | ✅ Per device manual |
| `DeviceName` | Identifier | "MyDevice" | 🟡 Each device different |

#### ConfigureTcpModbusSimulator() - Simulator Configuration

```csharp
async Task<TcpModbusSimulator> ConfigureTcpModbusSimulator()
{
    // ★ Only for local testing, remove when connecting real devices ★
    var simulatorConfig = new TcpModbusSimulatorConfiguration
    {
        TcpConnection = new TcpConnectionSettings
        {
            IpAddress = "127.0.0.1",  // ← Simulator listening address
            Port = 5020               // ← Must match Device configuration
        },
        Sensors = new List<SimulatedSensor>
        {
            new SimulatedSensor
            {
                Name = "TemperatureSensor",
                StartAddress = 0,              // ← Must match Device configuration
                DataType = SimulatedDataType.Float32,
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 18.0,           // ← Minimum value
                    MaxValue = 32.0,           // ← Maximum value
                    InitialValue = 25.0,       // ← Initial value
                    ChangeRate = 0.2,          // ← Change rate per cycle
                    NoiseLevel = 0.1           // ← Random noise
                }
            }
        }
    };

    var simulator = new TcpModbusSimulator(simulatorConfig, loggerFactory.CreateLogger<TcpModbusSimulator>());
    await simulator.StartAsync();
    return simulator;
}
```

**Simulator Purpose:**
- ✅ **Development**: Test code without real hardware
- ✅ **CI/CD**: Automated tests don't depend on real hardware
- ❌ **Production**: Not needed

---

### ✅ Step 2 Summary

Now you should understand:

1. **MyFirstDevice.cs** - Your business logic goes here
   - ✅ Inherit `TcpModbusDevice` to get communication capabilities
   - ✅ Subscribe to Events to handle data
   - ✅ SDK handles low-level, you handle business logic

2. **Program.cs** - Application startup and configuration
   - ✅ Manual lifecycle control (for learning)
   - ✅ Modify CloudService to switch test/production environment
   - ✅ Graceful Shutdown ensures data safety

3. **Helper Methods** - Device and Simulator configuration
   - ✅ `ConfigureDeviceConfiguration()` - Modify when connecting real devices
   - ✅ `ConfigureTcpModbusSimulator()` - For testing, can be removed

**Next:** Understanding ApplicationContext advanced configuration

---

### 2.4 IWedaApplicationContext Advanced Configuration

`IWedaApplicationContext` is the SDK's core, managing all framework-level services. Let's dive into its configuration options.

#### What is ApplicationContext?

ApplicationContext is the SDK's **runtime environment**, similar to ASP.NET Core's `IServiceProvider`, but designed specifically for IoT devices.

```csharp
using var context = new WedaApplicationContext(options =>
{
    // Configure all SDK behavior here
});
```

#### WedaContextOptions Complete Reference

`WedaContextOptions` contains the following configuration items:

##### 1. **CloudService** - Cloud Service Configuration

```csharp
options.CloudService = WedaFactory.Cloud.Mock;  // or
options.NatsConnectionSettings = new NatsConnectionSettings { ... };  // or
options.CloudService = new MyCustomCloudService();
```

| Option | Purpose | When to Use |
|--------|---------|-------------|
| `Mock` | Mock cloud service | ✅ Local development, testing |
| `NatsConnectionSettings` | Configure NATS connection parameters | ✅ Production |
| Custom `IWedaCloudService` | Fully custom implementation | 🟡 Special requirements |

**Example: Switching cloud services**

```csharp
// Development environment - Use Mock
options.CloudService = WedaFactory.Cloud.Mock;

// Production environment - Configure real NATS connection
options.NatsConnectionSettings = new NatsConnectionSettings
{
    Url = "nats://production-server:4222",
    CredFile = "/path/to/nats.creds"  // If authentication needed
};

// Advanced - Custom cloud service
options.CloudService = new MyCustomCloudService();
```

##### 2. **LoggerFactory** - Logger Factory

```csharp
options.LoggerFactory = loggerFactory;
```

| Purpose | Description |
|---------|-------------|
| SDK internal logging | All SDK components use this factory for logging |
| Device logging | Your `_logger` also comes from this factory |
| Unified management | Centrally control log level, output targets |

**❌ Usually no need to modify** unless you're replacing the logging framework.

##### 3. **DeviceOptions** - Device Feature Control (Application Layer)

```csharp
options.DeviceOptions = new DeviceOptions
{
    DefaultPollingIntervalMs = 1000,      // Telemetry polling interval
    EnableTelemetry = true,               // Enable telemetry upload
    EnableHealthReporting = true,         // Enable health reporting
    EnableCommands = true,                // Enable command receiving
    EnableConfigUpdates = true            // Enable configuration updates
};
```

##### 📊 DeviceOptions Detailed Explanation

| Option | Default | Purpose | When to Enable |
|--------|---------|---------|----------------|
| `DefaultPollingIntervalMs` | 1000ms | How often to read device data | Always set |
| `EnableTelemetry` | `false` | Send telemetry to cloud (uplink) | ✅ When monitoring data needed |
| `EnableHealthReporting` | `false` | Send health status (uplink) | ✅ When device monitoring needed |
| `EnableCommands` | `false` | Receive cloud commands (downlink) | ✅ When remote control needed |
| `EnableConfigUpdates` | `false` | Receive config updates (downlink) | ✅ When dynamic adjustment needed |

**Real-world examples: Different scenario configurations**

```csharp
// Scenario 1: Monitoring only, no control
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = true,           // ✅ Upload data
    EnableHealthReporting = true,     // ✅ Report health
    EnableCommands = false,           // ❌ Don't accept commands
    EnableConfigUpdates = false       // ❌ No dynamic adjustment
};

// Scenario 2: Fully offline (local testing)
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = false,          // ❌ Don't upload
    EnableHealthReporting = false,    // ❌ Don't report
    EnableCommands = false,           // ❌ Don't accept commands
    EnableConfigUpdates = false       // ❌ No updates
};

// Scenario 3: Full features (production)
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = true,           // ✅ Upload data
    EnableHealthReporting = true,     // ✅ Report health
    EnableCommands = true,            // ✅ Accept commands
    EnableConfigUpdates = true        // ✅ Dynamic adjustment
};
```

**⚠️ Notes:**
- These options **only take effect when connected to real cloud**
- When using `Mock` cloud, these switches don't work (Mock doesn't actually send data)
- Can be dynamically adjusted at runtime (advanced usage)

##### 4. **ConnectionOptions** - Connection Retry Configuration (Communication Layer)

```csharp
options.ConnectionOptions = new ConnectionOptions
{
    MaxRetryAttempts = 3,           // Maximum retry attempts
    RetryDelayMs = 1000,            // Initial retry delay (exponential backoff)
    ConnectionTimeoutMs = 30000     // Connection timeout (30 seconds)
};
```

##### 📊 ConnectionOptions Detailed Explanation

| Option | Default | Purpose | When to Adjust |
|--------|---------|---------|----------------|
| `MaxRetryAttempts` | 3 | Retry attempts after connection failure | 🟡 Increase for unstable networks |
| `RetryDelayMs` | 1000ms | First retry delay (exponentially grows) | 🟡 Adjust based on network conditions |
| `ConnectionTimeoutMs` | 30000ms | Connection timeout | 🟡 Increase for slow devices |

**Exponential Backoff Explanation:**

```
1st retry: wait 1000ms
2nd retry: wait 2000ms (1000 × 2)
3rd retry: wait 4000ms (2000 × 2)
```

**Real-world examples: Different scenario configurations**

```csharp
// Scenario 1: Stable LAN (default is fine)
options.ConnectionOptions = ConnectionOptions.Default;

// Scenario 2: Unstable Wi-Fi environment
options.ConnectionOptions = new ConnectionOptions
{
    MaxRetryAttempts = 5,           // Increase retries
    RetryDelayMs = 2000,            // Increase initial delay
    ConnectionTimeoutMs = 60000     // Increase timeout (1 minute)
};

// Scenario 3: Fail fast (testing)
options.ConnectionOptions = new ConnectionOptions
{
    MaxRetryAttempts = 1,           // Only one attempt
    RetryDelayMs = 100,             // Short delay
    ConnectionTimeoutMs = 5000      // 5 second timeout
};
```

##### 5. **Configuration** - Application Configuration

```csharp
options.Configuration = configuration;  // IConfiguration instance
```

| Purpose | Description |
|---------|-------------|
| Read appsettings.json | Access application settings |
| Environment variables | Read environment variable configuration |
| Custom configuration | Your application-specific settings |

**Example: Read custom configuration from Configuration**

```csharp
// appsettings.json
{
  "MyApp": {
    "AlertThreshold": 30.0,
    "NotificationEmail": "admin@example.com"
  }
}

// Use in your device
var threshold = context.Configuration["MyApp:AlertThreshold"];
var email = context.Configuration["MyApp:NotificationEmail"];
```

##### 6. **Other Advanced Options**

```csharp
options.NatsConnectionSettings = new NatsConnectionSettings
{
    Url = "nats://localhost:4222",
    CredFile = "/path/to/nats.creds"
};

options.AutoLoadDtdl = true;                    // Auto-load DTDL definitions
options.DisposeServices = true;                 // Auto-cleanup services on context disposal
options.DeviceConfigurationKey = "Device1";     // Select which device from DeviceConfigs
```

#### 🎯 Real-world Application Examples

##### Example 1: Development Environment Configuration

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;
    options.CloudService = WedaFactory.Cloud.Mock;  // Use Mock cloud

    // Disable all cloud features (local testing)
    options.DeviceOptions = new DeviceOptions
    {
        DefaultPollingIntervalMs = 500,  // Fast polling for testing
        EnableTelemetry = false,
        EnableHealthReporting = false,
        EnableCommands = false,
        EnableConfigUpdates = false
    };

    // Fail fast configuration (detect issues immediately during development)
    options.ConnectionOptions = new ConnectionOptions
    {
        MaxRetryAttempts = 1,
        RetryDelayMs = 100,
        ConnectionTimeoutMs = 5000
    };
});
```

##### Example 2: Production Environment Configuration (Using appsettings.json)

Configure in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/production-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  },
  "Nats": {
    "Url": "nats://production:4222",
    "CredFile": "/path/to/production.creds",
    "Name": "production",
    "SerializerType": "json"
  }
}
```

```csharp
using var context = new WedaApplicationContext(options =>
{
    // ✅ SDK automatically loads appsettings.json
    // - Serilog configured to output to Console and File
    // - NATS automatically connects to production server

    // Enable all cloud features
    options.DeviceOptions = new DeviceOptions
    {
        DefaultPollingIntervalMs = 1000,
        EnableTelemetry = true,
        EnableHealthReporting = true,
        EnableCommands = true,
        EnableConfigUpdates = true
    };

    // Robust retry configuration
    options.ConnectionOptions = new ConnectionOptions
    {
        MaxRetryAttempts = 5,
        RetryDelayMs = 2000,
        ConnectionTimeoutMs = 60000
    };
});
```

##### Example 3: Edge Scenario (Unstable Network)

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // Connect to edge NATS server
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://edge-gateway:4222",
        CredFile = ""
    };

    // Upload only, no control (security considerations)
    options.DeviceOptions = new DeviceOptions
    {
        DefaultPollingIntervalMs = 5000,  // Reduce frequency to save bandwidth
        EnableTelemetry = true,
        EnableHealthReporting = true,
        EnableCommands = false,           // Don't accept commands
        EnableConfigUpdates = false       // No dynamic updates
    };

    // Enhanced retry mechanism
    options.ConnectionOptions = new ConnectionOptions
    {
        MaxRetryAttempts = 10,            // More retries
        RetryDelayMs = 5000,              // Longer delay
        ConnectionTimeoutMs = 120000      // 2 minute timeout
    };
});
```

#### 📊 Configuration Decision Tree

```
Need to connect to cloud?
├─ No → CloudService = Mock, all DeviceOptions = false
└─ Yes → Remove Mock, configure NatsConnectionSettings
    ├─ Need to upload data?
    │   ├─ Yes → EnableTelemetry = true
    │   └─ No → EnableTelemetry = false
    ├─ Need health monitoring?
    │   ├─ Yes → EnableHealthReporting = true
    │   └─ No → EnableHealthReporting = false
    ├─ Need remote control?
    │   ├─ Yes → EnableCommands = true
    │   └─ No → EnableCommands = false
    └─ Need dynamic adjustment?
        ├─ Yes → EnableConfigUpdates = true
        └─ No → EnableConfigUpdates = false
```

#### ✅ Context Options Summary

| Option | Level | Modification Frequency | Purpose |
|--------|-------|----------------------|---------|
| `CloudService` | Framework | 🟡 When switching environments | Select cloud service |
| `LoggerFactory` | Framework | ❌ Almost never | Logging system |
| `DeviceOptions` | Application | ✅ Frequently adjusted | Control device features |
| `ConnectionOptions` | Communication | 🟡 When network changes | Connection retry strategy |
| `Configuration` | Application | ❌ Usually provided by DI | Access configuration files |

**Important Reminders:**
- `DeviceOptions` and `ConnectionOptions` are **two different layers** of configuration
  - `DeviceOptions`: Application layer (controls business features)
  - `ConnectionOptions`: Communication layer (controls low-level connections)
- In the `wedaapi` template, these can be configured via Builder:
  ```csharp
  builder.AddTelemetry();           // Equivalent to EnableTelemetry = true
  builder.AddCommands();            // Equivalent to EnableCommands = true
  builder.ConfigurePollingInterval(1000);  // Equivalent to DefaultPollingIntervalMs = 1000
  ```

---

## Step 3: Configure Device

In `Program.cs`, find the `ConfigureDeviceConfiguration()` method:

```csharp
static DeviceConfiguration ConfigureDeviceConfiguration()
{
    var modbusDeviceConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",
        Manufacturer = "Advantech",
        Model = "CustomDevice-v1",
        Host = "127.0.0.1",     // Simulator address
        Port = 5020,            // Simulator port
        SlaveId = 1
    };

    // Define sensors
    var tempSensor = new ModbusSensorConfiguration
    {
        Name = "temperature.sensor",
        Dtmi = "dtmi:advantech:EdgeSync:Temperature;1",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };

    modbusDeviceConfig.AddSensor(tempSensor);
    return modbusDeviceConfig.ToDeviceConfiguration();
}
```

### Customize Configuration

You can modify:
- **DeviceName**: Device identification name
- **Host/Port**: Modbus TCP server address
- **Sensors**: Add more sensors

---

## Step 4: Build and Run

### Build Project

```bash
dotnet build
```

**Expected Output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Run Application

```bash
dotnet run
```

**Expected Output**:
```
[15:30:21 INF] Starting SubNode application...
[15:30:21 INF] Modbus Simulator started on 127.0.0.1:5020
[15:30:22 INF] Device 'MySubNode' initialized successfully
[15:30:22 INF] 📊 Telemetry received:
[15:30:22 INF]   - temperature.sensor: 25.3 (DTMI: dtmi:advantech:EdgeSync:Temperature;1)
[15:30:27 INF] 📊 Telemetry received:
[15:30:27 INF]   - temperature.sensor: 25.5 (DTMI: dtmi:advantech:EdgeSync:Temperature;1)
```

**Success!** Your device is reading temperature data from the Simulator 🎉

Press `Ctrl+C` to stop the application.

---

## Step 5: Add Custom Business Logic

Let's add custom logic: alert when temperature is too high.

### Modify MyFirstDevice.cs

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    private const double TemperatureThreshold = 30.0;

    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to events
        DataReceived += OnDataReceived;
        CommandReceived += OnCommandReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogInformation("📊 Telemetry received:");

        foreach (var measure in e.Data)
        {
            _logger.LogInformation("  - {Name}: {Value}",
                measure.Name,
                measure.Value);

            // Custom logic: Check temperature threshold
            if (measure.Name == "temperature.sensor" &&
                measure.Value is double temp &&
                temp > TemperatureThreshold)
            {
                _logger.LogWarning("🔥 HIGH TEMPERATURE ALERT! {Temp}°C exceeds threshold {Threshold}°C",
                    temp,
                    TemperatureThreshold);
            }
        }
    }

    private void OnCommandReceived(object? sender, ExecuteCommandEvent e)
    {
        _logger.LogInformation("🎮 Command received: {CommandName}", e.Command.Name);

        // Add command validation logic here
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
        CommandReceived -= OnCommandReceived;
    }
}
```

### Test Custom Logic

Modify Simulator configuration to exceed 30°C:

```csharp
static TcpModbusSimulatorConfiguration ConfigureTcpModbusSimulator()
{
    return new TcpModbusSimulatorConfiguration
    {
        // ...other settings...
        Sensors = new List<SimulatedSensor>
        {
            new SimulatedSensor
            {
                Name = "TemperatureSensor",
                Type = SensorType.Temperature,
                StartAddress = 0,
                RegisterCount = 2,
                DataType = SimulatedDataType.Float32,
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 28.0,    // Raise minimum
                    MaxValue = 35.0,    // Raise maximum
                    InitialValue = 32.0, // Start above threshold
                    ChangeRate = 0.5,
                    NoiseLevel = 0.2
                }
            }
        }
    };
}
```

Run again:

```bash
dotnet run
```

**Expected Output**:
```
[15:35:22 INF] 📊 Telemetry received:
[15:35:22 INF]   - temperature.sensor: 32.1
[15:35:22 WRN] 🔥 HIGH TEMPERATURE ALERT! 32.1°C exceeds threshold 30°C
```

---

## Step 6: Connect to Real Cloud Service

Default uses Mock Cloud Service. Switch to real NATS cloud:

### Modify Program.cs

Find this line in `Program.cs`:

```csharp
options.CloudService = WedaFactory.Cloud.Mock;
```

**✅ Method 1: Remove Mock (Simplest!)**

The SDK will automatically connect to NATS when `CloudService` is not set:

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ❌ Remove this line (or comment it out)
    // options.CloudService = WedaFactory.Cloud.Mock;

    // SDK will auto-connect to nats://localhost:4222
});
```

**💡 SDK Default Behavior:**
- If `CloudService` is not set, SDK auto-creates real NATS connection
- Default connects to `nats://localhost:4222`
- Can adjust connection settings via `NatsConnectionSettings` (see Section 2.4)

**🟡 Method 2: Configure via NatsConnectionSettings (Recommended)**

For custom NATS server address, use programmatic configuration:

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;

    // ❌ Remove Mock
    // options.CloudService = WedaFactory.Cloud.Mock;

    // ✅ Configure NATS connection
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://172.22.160.197:4224",
        CredFile = ""  // If authentication needed, provide .creds file path
    };
});
```

**📝 Method 3: Read from appsettings.json (Production Recommended)**

Add the `Nats` configuration section to `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "Nats": {
    "Url": "nats://172.22.160.197:4224",
    "CredFile": "",
    "Name": "default",
    "SerializerType": "json"
  }
}
```

Then in `Program.cs`, **SDK automatically reads appsettings.json (Most Simplified Version)**:

```csharp
using var context = new WedaApplicationContext(options =>
{
    // ✅ SDK automatically loads appsettings.json
    // - Automatically reads Serilog section to create LoggerFactory
    // - Automatically reads Nats section to create CloudService
    // No need to manually create anything!

    // ❌ Remove Mock
    // options.CloudService = WedaFactory.Cloud.Mock;
});
```

**If you need custom Logger, you can still manually provide it:**

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/myapp.log")  // Custom output location
    .CreateLogger();

using var loggerFactory = new SerilogLoggerFactory(Log.Logger);

using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;  // Manual setting overrides auto-load
});
```

**💡 SDK Auto-Load Behavior (Significantly Improved):**
- SDK **automatically** loads `appsettings.json` from the current directory
- Automatically reads `Serilog` section to create LoggerFactory (using Serilog)
- Automatically reads NATS connection settings from the `Nats` section
- If `appsettings.json` doesn't exist, uses default values:
  - LoggerFactory: `NullLoggerFactory` (no log output)
  - NATS URL: `nats://localhost:4222`
- **No need** to manually create `ConfigurationBuilder`, `IConfiguration`, or `LoggerFactory`
- **Optional Override**: Manually set `options.LoggerFactory` or `options.NatsConnectionSettings` will take precedence
- This makes usage more concise and consistent with wedaapi template behavior

### Set Environment Variables (Optional)

If using environment variables with Method 2:

```bash
export NATS_URL="nats://your-nats-server:4222"
export NATS_CREDS="/path/to/nats.creds"
dotnet run
```

Then in code:

```csharp
options.NatsConnectionSettings = new NatsConnectionSettings
{
    Url = Environment.GetEnvironmentVariable("NATS_URL") ?? "nats://localhost:4222",
    CredFile = Environment.GetEnvironmentVariable("NATS_CREDS") ?? ""
};
```

---

## Advanced Features

### 1. Subscribe to More Events

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to all events
        DataReceived += OnDataReceived;
        CommandReceived += OnCommandReceived;
        ConnectionStateChanged += OnConnectionStateChanged;
        DeviceStatusChanged += OnDeviceStatusChanged;
        ConfigurationUpdateReceived += OnConfigurationUpdateReceived;
    }

    // Connection state changed
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEvent e)
    {
        _logger.LogInformation("Connection state: {OldState} → {NewState}",
            e.OldState,
            e.NewState);
    }

    // Device status changed
    private void OnDeviceStatusChanged(object? sender, DeviceStatusChangedEvent e)
    {
        _logger.LogInformation("Device status: {OldStatus} → {NewStatus}",
            e.OldStatus,
            e.NewStatus);
    }

    // Configuration update received
    private void OnConfigurationUpdateReceived(object? sender, UpdateConfigurationEvent e)
    {
        _logger.LogInformation("Configuration update received");
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from all events
        DataReceived -= OnDataReceived;
        CommandReceived -= OnCommandReceived;
        ConnectionStateChanged -= OnConnectionStateChanged;
        DeviceStatusChanged -= OnDeviceStatusChanged;
        ConfigurationUpdateReceived -= OnConfigurationUpdateReceived;
    }
}
```

### 2. Add Custom Properties and State Tracking

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    private int _telemetryCount = 0;
    private DateTime _lastAlertTime = DateTime.MinValue;

    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _telemetryCount++;
        _logger.LogInformation("Telemetry count: {Count}", _telemetryCount);

        // Rate-limit alerts (max once per minute)
        if (DateTime.Now - _lastAlertTime > TimeSpan.FromMinutes(1))
        {
            // Send alert logic
            _lastAlertTime = DateTime.Now;
        }
    }

    ~MyFirstDevice()
    {
        DataReceived -= OnDataReceived;
    }
}
```

---

## Troubleshooting

### Issue 1: Connection Failed

**Symptom**:
```
Failed to connect to Modbus server at 127.0.0.1:5020
```

**Solution**:
1. Ensure Simulator is started (check for "Simulator started" in logs)
2. Verify Host/Port configuration is correct
3. Check firewall settings

### Issue 2: No Telemetry Data

**Symptom**: Device starts but no "Telemetry received" logs

**Solution**:
1. Check `DeviceOptions` enables telemetry:
```csharp
options.DeviceOptions = new DeviceOptions
{
    EnableTelemetry = true  // Ensure enabled
};
```
2. Check Simulator configuration has `EnableValueChanges = true`

### Issue 3: Compilation Errors

**Symptom**:
```
error CS0246: The type or namespace name 'TcpModbusDevice' could not be found
```

**Solution**:
```bash
dotnet restore
dotnet build
```

---

## Best Practices

### ✅ Recommended

1. **Use async/await**: All event handlers are async
2. **Log extensively**: Use `_logger` for key events
3. **Handle errors**: Use try-catch in custom logic
4. **Call base**: Remember `await base.OnXxx()` after override

### ❌ Avoid

1. **Don't block**: Avoid `Thread.Sleep()` in event handlers
2. **Don't ignore errors**: Catch and log all exceptions
3. **Don't modify base class**: Use overrides instead

---

## Next Steps

You've mastered the subnode template! Explore other options:

### Want production-ready multi-device management?
**[→ wedaapi - Web API Template](03_wedaapi_basic.md)**
- Hosted service pattern
- Multiple device management
- Production deployment

### Deep dive
**[→ Use Cases](../../02_use_cases/README.md)**
- Real-world scenarios
- Best practices
- Advanced patterns

---

## Summary

In this tutorial, you learned:

- ✅ Create project with `subnode` template
- ✅ Understand OOP device architecture
- ✅ Override event handlers
- ✅ Implement custom business logic
- ✅ Test locally and debug
- ✅ Connect to real cloud service

**Congratulations!** You've built your first custom IoT device 🎉

---

**Version**: 1.0.0
**Last Updated**: 2025-11-12
**Maintainer**: Rain Hu

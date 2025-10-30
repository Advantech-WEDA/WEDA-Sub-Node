---
title: Strongly-Typed Device Configuration
category: guide
order: 2
parent: null
related:
  - path: guide_custom_devices.md
    title: Creating Custom Devices
  - path: reference_device_configuration.md
    title: Device Configuration Reference
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Strongly-typed configuration pattern for device settings with IntelliSense and type safety
tags: [Configuration, Type Safety, Best Practices]
---

# Strongly-Typed Device Configuration

## Overview

This guide explains the strongly-typed configuration pattern used in Weda SubNode SDK, which provides IntelliSense support and compile-time type safety for device configuration.

## The Problem with Dictionary-Based Configuration

The base `DeviceConfiguration` class uses dictionaries for device-specific settings:

```csharp
public class DeviceConfiguration
{
    // Generic dictionary - no IntelliSense, no type safety
    public Dictionary<string, object> Communication { get; set; } = [];

    // Sensors also use dictionaries for parameters
    public List<Sensor> Sensors { get; set; } = [];
}
```

**Problems with this approach:**
- ❌ No IntelliSense support - developers must consult documentation
- ❌ No compile-time type checking - errors only discovered at runtime
- ❌ Hard to discover available options
- ❌ Prone to typos in property names
- ❌ No IDE refactoring support

## The Solution: Strongly-Typed Configuration Classes

For each device type, we create a strongly-typed configuration class that:
1. Provides IntelliSense-friendly properties
2. Enforces type safety at compile time
3. Makes configuration self-documenting
4. Converts to `DeviceConfiguration` for framework use

### Example: TcpModbusDeviceConfiguration

```csharp
public class TcpModbusDeviceConfiguration
{
    /// <summary>
    /// Device name (required)
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// TCP/IP host address (required)
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// TCP/IP port (default: 502)
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// Modbus slave/unit ID (default: 1)
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Sensors/registers to read
    /// </summary>
    public List<ModbusSensorConfiguration> Sensors { get; set; } = new();

    // ... more properties

    /// <summary>
    /// Converts to generic DeviceConfiguration
    /// </summary>
    public DeviceConfiguration ToDeviceConfiguration()
    {
        return new DeviceConfiguration
        {
            DeviceName = DeviceName,
            Communication = new Dictionary<string, object>
            {
                ["Host"] = Host,
                ["Port"] = Port,
                ["SlaveId"] = SlaveId
            },
            Sensors = Sensors.Select(s => s.ToSensor()).ToList()
            // ... map other properties
        };
    }
}
```

## Usage Comparison

### Before: Dictionary-Based (JSON or Code)

```csharp
// JSON configuration - no IntelliSense
{
  "Communication": {
    "Host": "192.168.1.100",  // Could have typo: "Hst"
    "Port": 502,               // Could be string: "502"
    "SlaveId": 1
  }
}

// Or programmatic - still no IntelliSense
var config = new DeviceConfiguration
{
    DeviceName = "device-1",
    Communication = new Dictionary<string, object>
    {
        ["Host"] = "192.168.1.100",   // Typo possible: "Hst"
        ["Port"] = 502,                // Type mismatch possible
        ["SlaveId"] = 1
    }
};
```

### After: Strongly-Typed Configuration

```csharp
var config = new TcpModbusDeviceConfiguration
{
    DeviceName = "device-1",
    Host = "192.168.1.100",  // ✓ IntelliSense autocomplete
    Port = 502,               // ✓ Type checked (must be int)
    SlaveId = 1              // ✓ Type checked (must be byte)
};

// Add sensors with full IntelliSense
config.AddSensor(
    name: "temperature",      // ✓ Parameter names shown
    dtmi: "dtmi:com:example:Temperature;1",
    registerAddress: 0,       // ✓ Type checked (ushort)
    dataType: ModbusDataType.Float32  // ✓ Enum autocomplete
);

// Convert to DeviceConfiguration for framework
var deviceConfig = config.ToDeviceConfiguration();
```

## Benefits

| Aspect | Dictionary-Based | Strongly-Typed |
|--------|------------------|----------------|
| IntelliSense | ❌ No | ✅ Yes |
| Type Safety | ❌ Runtime only | ✅ Compile-time |
| Discoverability | ❌ Need docs | ✅ Self-documenting |
| Refactoring | ❌ Manual | ✅ IDE support |
| Validation | ❌ Runtime errors | ✅ Compiler errors |
| Testability | ⚠️ Verbose | ✅ Easy |

## Implementation Pattern

### Step 1: Define the Configuration Class

```csharp
public class MyDeviceConfiguration
{
    // Required properties
    public required string DeviceName { get; set; }

    // Optional properties with defaults
    public string Manufacturer { get; set; } = "Advantech";

    // Device-specific settings
    public string ConnectionString { get; set; } = "";
    public int Timeout { get; set; } = 5000;

    // Sensors with strongly-typed config
    public List<MySensorConfiguration> Sensors { get; set; } = new();

    // Common properties
    public BackgroundTaskPeriods Periods { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}
```

### Step 2: Define Sensor Configuration

```csharp
public class MySensorConfiguration
{
    public required string Name { get; set; }
    public required string Dtmi { get; set; }

    // Device-specific sensor settings
    public string Channel { get; set; } = "CH1";
    public MySensorType Type { get; set; } = MySensorType.Analog;

    // Common sensor configuration
    public SensorConfig Config { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### Step 3: Implement Conversion Method

```csharp
public DeviceConfiguration ToDeviceConfiguration()
{
    return new DeviceConfiguration
    {
        DeviceName = DeviceName,
        DeviceType = DeviceType.Custom,
        DeviceCapabilities = new DeviceCapabilities
        {
            Manufacturer = Manufacturer,
            Model = "MyDevice",
            SubNodeSwVersion = "1.0",
            DeviceInfo = new Dictionary<string, object>()
        },
        Communication = new Dictionary<string, object>
        {
            ["ConnectionString"] = ConnectionString,
            ["Timeout"] = Timeout
        },
        Sensors = Sensors.Select(s => new Sensor
        {
            ResourceId = s.ResourceId ?? Guid.NewGuid().ToString(),
            Name = s.Name,
            Dtmi = s.Dtmi,
            SensorGroup = SensorGroup.AI,
            Parameters = new Dictionary<string, object>
            {
                ["Channel"] = s.Channel,
                ["Type"] = s.Type.ToString()
            },
            Config = s.Config,
            Metadata = s.Metadata
        }).ToList(),
        Periods = Periods,
        Properties = Properties
    };
}
```

### Step 4: Add Fluent API (Optional)

```csharp
public MyDeviceConfiguration AddSensor(
    string name,
    string dtmi,
    string channel,
    MySensorType type = MySensorType.Analog)
{
    Sensors.Add(new MySensorConfiguration
    {
        Name = name,
        Dtmi = dtmi,
        Channel = channel,
        Type = type
    });
    return this;
}
```

## Real-World Examples

### Example 1: Modbus TCP Device

```csharp
var config = new TcpModbusDeviceConfiguration
{
    DeviceName = "power-meter",
    Host = "192.168.1.100",
    Port = 502,
    SlaveId = 1,
    GroupId = "building-a"
}
.AddSensor("voltage", "dtmi:example:Voltage;1", 0, dataType: ModbusDataType.Float32)
.AddSensor("current", "dtmi:example:Current;1", 2, dataType: ModbusDataType.Float32)
.AddSensor("power", "dtmi:example:Power;1", 4, dataType: ModbusDataType.Float32);

// Convert and use
var deviceConfig = config.ToDeviceConfiguration();
var device = new ModbusDevice(context, deviceConfig, communication);
```

### Example 2: OPC UA Device (Hypothetical)

```csharp
public class OpcUaDeviceConfiguration
{
    public required string DeviceName { get; set; }
    public required string EndpointUrl { get; set; }
    public SecurityPolicy SecurityPolicy { get; set; } = SecurityPolicy.None;
    public OpcUaAuthentication Authentication { get; set; } = new();

    public List<OpcUaNodeConfiguration> Nodes { get; set; } = new();

    public DeviceConfiguration ToDeviceConfiguration() { /* ... */ }
}

// Usage
var config = new OpcUaDeviceConfiguration
{
    DeviceName = "plc-1",
    EndpointUrl = "opc.tcp://192.168.1.50:4840",
    SecurityPolicy = SecurityPolicy.Basic256Sha256,
    Authentication = new OpcUaAuthentication
    {
        Username = "admin",
        Password = "password"
    }
}
.AddNode("temperature", "dtmi:example:Temp;1", "ns=2;s=Temperature")
.AddNode("pressure", "dtmi:example:Press;1", "ns=2;s=Pressure");
```

### Example 3: Serial Device (Hypothetical)

```csharp
public class SerialDeviceConfiguration
{
    public required string DeviceName { get; set; }
    public required string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public Parity Parity { get; set; } = Parity.None;
    public int DataBits { get; set; } = 8;
    public StopBits StopBits { get; set; } = StopBits.One;

    public DeviceConfiguration ToDeviceConfiguration() { /* ... */ }
}

// Usage
var config = new SerialDeviceConfiguration
{
    DeviceName = "serial-sensor",
    PortName = "COM3",
    BaudRate = 115200,
    Parity = Parity.Even
};
```

## Best Practices

### 1. Use Required Properties
```csharp
public required string DeviceName { get; set; }  // ✅ Compiler enforces
```

### 2. Provide Sensible Defaults
```csharp
public int Port { get; set; } = 502;  // ✅ Default value
public byte SlaveId { get; set; } = 1;
```

### 3. Add XML Documentation
```csharp
/// <summary>
/// TCP/IP port (default: 502)
/// </summary>
public int Port { get; set; } = 502;  // ✅ Shows in IntelliSense
```

### 4. Use Enums for Fixed Options
```csharp
public ModbusDataType DataType { get; set; } = ModbusDataType.Float32;  // ✅ Autocomplete
```

### 5. Support Both Styles
```csharp
// Object initializer
var config = new TcpModbusDeviceConfiguration { /* ... */ };

// Fluent API
var config = new TcpModbusDeviceConfiguration()
    .WithHost("192.168.1.100")
    .WithPort(502)
    .AddSensor(/* ... */);
```

### 6. Keep Conversion Logic Simple
```csharp
public DeviceConfiguration ToDeviceConfiguration()
{
    // Direct property mapping, minimal logic
    return new DeviceConfiguration
    {
        DeviceName = DeviceName,
        Communication = new Dictionary<string, object>
        {
            ["Host"] = Host,
            ["Port"] = Port
        }
    };
}
```

## Testing

Strongly-typed configuration makes testing easier:

```csharp
[Fact]
public void Should_Create_Valid_Configuration()
{
    // Arrange
    var config = new TcpModbusDeviceConfiguration
    {
        DeviceName = "test-device",
        Host = "127.0.0.1"
    }
    .AddSensor("temp", "dtmi:test:Temp;1", 0);

    // Act
    var deviceConfig = config.ToDeviceConfiguration();

    // Assert
    Assert.Equal("test-device", deviceConfig.DeviceName);
    Assert.Equal("127.0.0.1", deviceConfig.Communication["Host"]);
    Assert.Single(deviceConfig.Sensors);
}
```

## Migration Guide

### From JSON Configuration

**Before (appsettings.json):**
```json
{
  "Devices": {
    "device-1": {
      "DeviceName": "device-1",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502
      }
    }
  }
}
```

**After (C# code):**
```csharp
var config = new TcpModbusDeviceConfiguration
{
    DeviceName = "device-1",
    Host = "192.168.1.100",
    Port = 502
};
```

### From Dictionary-Based Code

**Before:**
```csharp
var config = new DeviceConfiguration
{
    Communication = new Dictionary<string, object>
    {
        ["Host"] = "192.168.1.100",
        ["Port"] = 502,
        ["SlaveId"] = 1
    }
};
```

**After:**
```csharp
var config = new TcpModbusDeviceConfiguration
{
    Host = "192.168.1.100",
    Port = 502,
    SlaveId = 1
}.ToDeviceConfiguration();
```

## Conclusion

Strongly-typed configuration classes provide:
- ✅ Better developer experience with IntelliSense
- ✅ Compile-time type safety
- ✅ Self-documenting code
- ✅ Easier testing and refactoring
- ✅ Reduced runtime errors

Use this pattern for all device-specific configurations to improve code quality and developer productivity.


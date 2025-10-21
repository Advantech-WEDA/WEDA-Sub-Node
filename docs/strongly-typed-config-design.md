# Strongly-Typed Device Configuration Design

## Overview

This document describes the design of strongly-typed device configuration classes in Weda SubNode SDK.

## Design Goals

1. **Developer Experience**: Provide IntelliSense support for configuration
2. **Type Safety**: Catch configuration errors at compile-time
3. **Discoverability**: Make configuration options self-documenting
4. **Flexibility**: Support both JSON and programmatic configuration
5. **Maintainability**: Centralize device-specific configuration logic

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  TcpModbusDeviceConfiguration (Strongly-Typed)              │
│  - Provides IntelliSense and type safety                    │
│  - Device-specific properties (Host, Port, SlaveId)         │
│  - Fluent API for sensor configuration                      │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        │ ToDeviceConfiguration()
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  DeviceConfiguration (Generic)                              │
│  - Framework-level representation                           │
│  - Uses Dictionary<string, object> for flexibility          │
│  - Consumed by DeviceBase                                   │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        │ Constructor parameter
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│  ModbusDevice : DeviceBase                                  │
│  - Inherits from DeviceBase                                 │
│  - Uses DeviceConfiguration from parent                     │
│  - Extracts device-specific settings via extensions         │
└─────────────────────────────────────────────────────────────┘
```

## Key Components

### 1. Strongly-Typed Configuration Class

**Location**: `src/Weda.SubNode.Core/Protocols/Modbus/TcpModbusDeviceConfiguration.cs`

**Purpose**: Provides type-safe, IntelliSense-friendly configuration for TCP Modbus devices

**Key Features**:
- Strongly-typed properties (Host, Port, SlaveId)
- Required properties enforced by compiler
- Default values for optional properties
- Fluent API for sensor configuration
- Conversion method to generic DeviceConfiguration

**Example**:
```csharp
public class TcpModbusDeviceConfiguration
{
    public required string DeviceName { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; } = 502;
    public byte SlaveId { get; set; } = 1;
    public List<ModbusSensorConfiguration> Sensors { get; set; } = new();

    public DeviceConfiguration ToDeviceConfiguration() { /* ... */ }
}
```

### 2. Generic Device Configuration

**Location**: `src/Weda.SubNode.Abstractions/Devices/DeviceConfiguration.cs`

**Purpose**: Framework-level device configuration used by DeviceBase

**Key Features**:
- Generic enough to support any device type
- Dictionary-based for flexibility
- Used internally by the framework

**Example**:
```csharp
public class DeviceConfiguration
{
    public string DeviceName { get; set; }
    public Dictionary<string, object> Communication { get; set; } = [];
    public List<Sensor> Sensors { get; set; } = [];
    // ...
}
```

### 3. Extension Methods

**Location**: `src/Weda.SubNode.Core/Protocols/Modbus/ModbusDeviceConfiguration.cs`

**Purpose**: Extract device-specific settings from generic DeviceConfiguration

**Example**:
```csharp
public static class ModbusDeviceConfigurationExtensions
{
    public static byte GetModbusSlaveId(this DeviceConfiguration config)
    {
        return Convert.ToByte(config.Communication.GetValueOrDefault("SlaveId", 1));
    }
}
```

## Usage Patterns

### Pattern 1: Programmatic Configuration

```csharp
// Create strongly-typed configuration
var config = new TcpModbusDeviceConfiguration
{
    DeviceName = "power-meter",
    Host = "192.168.1.100",
    Port = 502,
    SlaveId = 1
}
.AddSensor("voltage", "dtmi:example:Voltage;1", 0, dataType: ModbusDataType.Float32)
.AddSensor("current", "dtmi:example:Current;1", 2, dataType: ModbusDataType.Float32);

// Convert to generic configuration
var deviceConfig = config.ToDeviceConfiguration();

// Create device
var device = new ModbusDevice(context, deviceConfig, communication);
```

### Pattern 2: JSON Configuration (Traditional)

```json
{
  "Devices": {
    "power-meter": {
      "DeviceName": "power-meter",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [...]
    }
  }
}
```

### Pattern 3: Hybrid Approach

```csharp
// Load base configuration from JSON
var jsonConfig = configuration.GetSection("Devices:power-meter")
    .Get<DeviceConfiguration>();

// Or override/extend with strongly-typed configuration
var typedConfig = new TcpModbusDeviceConfiguration
{
    DeviceName = "power-meter",
    Host = "192.168.1.100"
};
```

## Benefits

### For Developers

| Aspect | Before | After |
|--------|--------|-------|
| Configuration | Consult documentation | IntelliSense autocomplete |
| Type safety | Runtime errors | Compile-time errors |
| Discovery | Read docs/examples | Explore in IDE |
| Refactoring | Manual search/replace | IDE refactoring tools |
| Validation | Runtime checks | Compiler checks |

### For Maintenance

- **Centralized Logic**: Device-specific configuration in one place
- **Clear Contracts**: Explicit properties show what's required
- **Easy Testing**: Programmatic configuration easier to test
- **Better Refactoring**: Type-safe changes across codebase

## Implementation Guidelines

### For Each Device Type

1. **Create Configuration Class**
   - Location: `src/Weda.SubNode.Core/Protocols/{Protocol}/{Protocol}DeviceConfiguration.cs`
   - Name: `{Transport}{Protocol}DeviceConfiguration`
   - Examples: `TcpModbusDeviceConfiguration`, `SerialModbusDeviceConfiguration`, `OpcUaDeviceConfiguration`

2. **Define Properties**
   - Use `required` for mandatory properties
   - Provide defaults for optional properties
   - Add XML documentation for IntelliSense

3. **Create Sensor Configuration**
   - Match the pattern: `{Protocol}SensorConfiguration`
   - Include device-specific sensor settings
   - Support SensorConfig for DSP/transforms

4. **Implement Conversion**
   - Method: `ToDeviceConfiguration()`
   - Map properties to generic format
   - Keep logic simple and direct

5. **Add Fluent API** (optional)
   - `AddSensor()` method for convenience
   - Return `this` for chaining
   - Support common use cases

### Naming Conventions

- **Configuration Class**: `{Transport}{Protocol}DeviceConfiguration`
  - Examples: `TcpModbusDeviceConfiguration`, `SerialModbusDeviceConfiguration`

- **Sensor Configuration**: `{Protocol}SensorConfiguration`
  - Examples: `ModbusSensorConfiguration`, `OpcUaSensorConfiguration`

- **Extension Methods**: `{Protocol}DeviceConfigurationExtensions`
  - Examples: `ModbusDeviceConfigurationExtensions`

### File Organization

```
src/Weda.SubNode.Core/
  Protocols/
    Modbus/
      TcpModbusDeviceConfiguration.cs       # Strongly-typed config
      ModbusDeviceConfiguration.cs          # Extensions & enums
      ModbusDevice.cs                       # Device implementation
    OpcUa/
      OpcUaDeviceConfiguration.cs           # Future device type
    Serial/
      SerialDeviceConfiguration.cs          # Future device type
```

## Migration Path

### Phase 1: Add Strongly-Typed Classes (✅ Complete)
- Created `TcpModbusDeviceConfiguration`
- Created `ModbusSensorConfiguration`
- Removed obsolete `ModbusConfigurationBuilder`

### Phase 2: Update Examples
- [x] Create `ModbusDeviceConfiguration` example
- [x] Update documentation references
- [ ] Update existing examples to use new pattern

### Phase 3: Expand to Other Protocols
- [ ] Create `SerialModbusDeviceConfiguration`
- [ ] Create `OpcUaDeviceConfiguration`
- [ ] Create protocol-agnostic patterns

## Example: Creating a New Device Type

```csharp
// 1. Define configuration class
public class OpcUaDeviceConfiguration
{
    public required string DeviceName { get; set; }
    public required string EndpointUrl { get; set; }
    public SecurityPolicy SecurityPolicy { get; set; } = SecurityPolicy.None;
    public List<OpcUaNodeConfiguration> Nodes { get; set; } = new();

    public DeviceConfiguration ToDeviceConfiguration()
    {
        return new DeviceConfiguration
        {
            DeviceName = DeviceName,
            Communication = new Dictionary<string, object>
            {
                ["EndpointUrl"] = EndpointUrl,
                ["SecurityPolicy"] = SecurityPolicy.ToString()
            },
            Sensors = Nodes.Select(n => n.ToSensor()).ToList()
        };
    }
}

// 2. Define sensor configuration
public class OpcUaNodeConfiguration
{
    public required string Name { get; set; }
    public required string Dtmi { get; set; }
    public required string NodeId { get; set; }
    public OpcUaDataType DataType { get; set; } = OpcUaDataType.Double;

    public Sensor ToSensor() { /* ... */ }
}

// 3. Add extension methods
public static class OpcUaDeviceConfigurationExtensions
{
    public static string GetEndpointUrl(this DeviceConfiguration config)
    {
        return config.Communication["EndpointUrl"]?.ToString() ?? "";
    }
}
```

## See Also

- [Strongly-Typed Configuration Guide](docs/wiki/en/guides/strongly-typed-configuration.md)
- [ModbusDeviceConfiguration Example](examples/ModbusDeviceConfiguration/)
- [Creating Custom Devices](docs/wiki/en/guides/custom-devices.md)

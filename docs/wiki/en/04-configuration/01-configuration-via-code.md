---
sidebar_position: 1
sidebar_label: 'Configuration via Code'
hide_title: true
title: 'Configuration via Code | SubNode SDK'
keywords: ['SubNode', 'Sensor', 'Configuration', 'Code', 'Programmatic']
description: 'Configure devices and sensors programmatically using the SubNode SDK API.'
---

# Configuration via Code

> Configure devices and sensors programmatically using the SubNode SDK API.

## Overview

In addition to JSON configuration files, SubNode supports defining devices and sensors dynamically through code. Programmatic configuration is suited for scenarios where settings need to be adjusted at runtime, or sensor definitions come from an external system. This article demonstrates how to build configuration using the `TcpModbusDeviceConfiguration` API with the SubNode template.

## What You'll Learn

After reading this article, you will be able to:

- Build device configuration using `TcpModbusDeviceConfiguration`
- Add sensors, Transforms, and DSP Filters via code
- Understand the use cases for programmatic vs JSON configuration

## Prerequisites

- Completed [Start with Template](../02-getting-started/03-start-with-template.md)
- Understand [SubNode Hierarchy](../03-hierarchy/02-aggregation-overview.md)

---

## Building Device Configuration

### Using TcpModbusDeviceConfiguration

```csharp
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Abstractions.Telemetry;

var modbusConfig = new TcpModbusDeviceConfiguration
{
    DeviceName = "MyDevice",
    Manufacturer = "YourCompany",
    Model = "Device-v1",
    Host = "127.0.0.1",
    Port = 5020,
    SlaveId = 1
};
```

### Adding Sensors

```csharp
var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature_sensor",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// Set reporting interval (milliseconds)
tempSensor.Config.Interval = 5000;

// Add to device configuration
modbusConfig.AddSensor(tempSensor);
```

### Adding Multiple Sensors

```csharp
modbusConfig.AddSensor(new ModbusSensorReporturation
{
    Name = "temp_zone1",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
});

modbusConfig.AddSensor(new ModbusSensorReporturation
{
    Name = "temp_zone2",
    RegisterAddress = 2,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
});

modbusConfig.AddSensor(new ModbusSensorReporturation
{
    Name = "humidity",
    RegisterAddress = 4,
    RegisterCount = 1,
    DataType = ModbusDataType.UInt16,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.AI
});
```

---

## Complete Example

Used with the SubNode template:

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Cloud;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Abstractions.Telemetry;

using var context = new WedaApplicationContext(
    options => options.CloudService = Cloud.Mock());

await using var subNode = new SubNode(context);
subNode.AddDevice(new MyDevice(context, BuildDeviceConfiguration()));

await subNode.InitializeAsync();
await subNode.StartAsync();

TcpModbusDeviceConfiguration BuildDeviceConfiguration()
{
    var config = new TcpModbusDeviceConfiguration
    {
        DeviceName = "TemperatureMonitor",
        Manufacturer = "YourCompany",
        Model = "TempMon-v1",
        Host = "127.0.0.1",
        Port = 5020,
        SlaveId = 1
    };

    var tempSensor = new ModbusSensorReporturation
    {
        Name = "temperature_sensor",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };
    tempSensor.Config.Interval = 1000;

    config.AddSensor(tempSensor);

    return config;
}
```

> The SubNode template's `TcpModbusDeviceConfiguration` is automatically converted to `DeviceConfiguration` -- no need to manually call `ToDeviceConfiguration()`.

---

## Adding Transforms and DSP Filters

### Calibration Transform

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform
        {
            Scale = 0.1,
            Offset = -10.0
        });
    });
```

### Multiple Transforms (executed in order)

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        // Step 1: Calibration
        pipeline.Add(new CalibrationTransform
        {
            Scale = 0.1,
            Offset = -273.15
        });

        // Step 2: Unit conversion
        pipeline.Add(new UnitConversionTransform
        {
            FromUnit = "celsius",
            ToUnit = "fahrenheit"
        });
    });
```

### DSP Filter

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform { Scale = 0.1 });
    })
    .ConfigureDspFilters(pipeline =>
    {
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });
    });
```

### Threshold

```csharp
tempSensor.Config.Report.Thresholds = new ThresholdConfig
{
    LowerWarning = 10,
    LowerCritical = 5,
    UpperWarning = 35,
    UpperCritical = 40
};
```

---

## Modbus Data Type Reference

| DataType | Registers | Description |
|----------|-----------|-------------|
| `UInt16` | 1 | 16-bit unsigned integer |
| `Int16` | 1 | 16-bit signed integer |
| `UInt32` | 2 | 32-bit unsigned integer |
| `Int32` | 2 | 32-bit signed integer |
| `Float32` | 2 | 32-bit floating point |
| `Float64` | 4 | 64-bit floating point |

| RegisterType | Modbus Function Code | Description |
|--------------|---------------------|-------------|
| `HoldingRegister` | FC03 | Read/Write registers |
| `InputRegister` | FC04 | Read-only registers |
| `Coil` | FC01 | Read/Write bits |
| `DiscreteInput` | FC02 | Read-only bits |

---

## Custom DeviceConfiguration

If you [create a custom device](../09-customization/01-custom-device.md) (e.g., with a custom protocol), you can implement the `IDeviceConfiguration` interface to provide a strongly-typed programmatic configuration API for your device.

### IDeviceConfiguration Interface

```csharp
public interface IDeviceConfiguration
{
    /// <summary>
    /// Converts this strongly-typed configuration to the generic DeviceConfiguration
    /// used by the DeviceBase framework.
    /// </summary>
    DeviceConfiguration ToDeviceConfiguration();
}
```

Only one method to implement: convert your strongly-typed configuration to the framework's generic `DeviceConfiguration`.

### Example: Custom Serial Device Configuration

```csharp
public class SerialDeviceConfiguration : IDeviceConfiguration
{
    public required string DeviceName { get; set; }
    public required string PortName { get; set; }  // e.g. "/dev/ttyUSB0"
    public int BaudRate { get; set; } = 9600;
    public byte SlaveId { get; set; } = 1;
    public List<ModbusSensorReporturation> Sensors { get; set; } = new();

    public SerialDeviceConfiguration AddSensor(ModbusSensorReporturation sensor)
    {
        Sensors.Add(sensor);
        return this;
    }

    public DeviceConfiguration ToDeviceConfiguration()
    {
        return new DeviceConfiguration
        {
            DeviceName = DeviceName,
            DeviceCommunication = new Dictionary<string, object>
            {
                ["PortName"] = PortName,
                ["BaudRate"] = BaudRate,
                ["SlaveId"] = SlaveId
            },
            Sensors = Sensors.Select(s => /* convert to Sensor */ ...).ToList()
        };
    }
}
```

Usage follows the same pattern as `TcpModbusDeviceConfiguration` -- pass the configuration instance directly, and the Device calls `ToDeviceConfiguration()` internally:

```csharp
// Device constructor receives SerialDeviceConfiguration, calls ToDeviceConfiguration() internally
public class MySerialDevice : RtuModbusDevice
{
    public MySerialDevice(IWedaApplicationContext context, SerialDeviceConfiguration config)
        : base(context, config.ToDeviceConfiguration()) { }
}

// Usage - pass the strongly-typed configuration directly
var config = new SerialDeviceConfiguration
{
    DeviceName = "MySerialDevice",
    PortName = "/dev/ttyUSB0",
    BaudRate = 9600
};
config.AddSensor(new ModbusSensorReporturation { ... });

subNode.AddDevice(new MySerialDevice(context, config));
```

> The SDK's built-in `TcpModbusDeviceConfiguration` serves as the reference implementation of `IDeviceConfiguration` and can be used as a template for custom configuration classes. Callers should not manually call `ToDeviceConfiguration()` -- that is the Device's responsibility.

---

## Programmatic vs JSON Configuration

| Aspect | Programmatic | JSON |
|--------|-------------|------|
| Use case | Dynamic settings, external sources, SubNode template | Static settings, SA operations, WedaBuilder template |
| Compile-time checking | Yes (strongly-typed) | No |
| Cloud sync | Supported (persisted via `.device-config-cache.json`) | Supported (via `devicecfg.json`) |
| Initial config source | Code (can be overridden by cache) | JSON file (can be overridden by cache) |
| Change method | Requires recompilation | Edit JSON file |

> Both approaches support cloud sync. Cloud-pushed configuration changes are persisted to `.device-config-cache.json`, ensuring the last synced configuration is used when restarting offline. Use the `--no-cache` argument to force ignoring the cache.

---

## Summary

- `TcpModbusDeviceConfiguration` provides a strongly-typed API for building Modbus device configuration
- `ModbusSensorReporturation` defines sensor register addresses, data types, and reporting intervals
- Transforms and DSP Filters are added via `ConfigureTransforms` / `ConfigureDspFilters` fluent API
- Programmatic configuration suits dynamic scenarios; JSON configuration suits static deployments

## See Also

- [Configuration via JSON](./02-configuration-via-json.md) - JSON configuration approach
- [Configuration Examples](./03-configuration-examples.md) - Complete examples for each protocol
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Transform and DSP Filter details

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

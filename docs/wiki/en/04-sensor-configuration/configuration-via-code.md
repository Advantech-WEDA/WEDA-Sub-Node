---
sidebar_position: 1
sidebar_label: 'Configuration via Code'
hide_title: true
title: 'Sensor Configuration via Code'
keywords: ['SubNode', 'Sensor', 'Configuration', 'Code', 'Programmatic']
description: 'Configure sensors programmatically using the SubNode SDK API'
---

# Configuration via Code

> Configure sensors programmatically using the SubNode SDK API.

## Overview

Programmatic configuration is useful when:
- You need dynamic sensor setup based on runtime conditions
- Sensor definitions come from an external source
- You prefer compile-time validation of configurations

## Basic Sensor Configuration

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
    Port = 502,
    SlaveId = 1
};
```

### Adding Sensors

```csharp
// Create a temperature sensor
var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature.sensor",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// Configure reporting interval (milliseconds)
tempSensor.Config.Interval = 5000;

// Add to device configuration
modbusConfig.AddSensor(tempSensor);
```

### Complete Example

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Abstractions.Telemetry;

// Create application context
using var context = new WedaApplicationContext(
    options => options.CloudService = WedaFactory.Cloud.Mock);

// Build device configuration
var deviceConfig = BuildDeviceConfiguration();

// Create SubNode with device
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyDevice(context, deviceConfig));

await subNode.InitializeAsync();
await subNode.StartAsync();

DeviceConfiguration BuildDeviceConfiguration()
{
    var modbusConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "TemperatureMonitor",
        Manufacturer = "YourCompany",
        Model = "TempMon-v1",
        Host = "192.168.1.100",
        Port = 502,
        SlaveId = 1
    };

    // Add multiple sensors
    modbusConfig.AddSensor(new ModbusSensorReporturation
    {
        Name = "temp.zone1",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    });

    modbusConfig.AddSensor(new ModbusSensorReporturation
    {
        Name = "temp.zone2",
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

    // Convert to DeviceConfiguration
    var config = modbusConfig.ToDeviceConfiguration();

    // Initialize DTDL metadata (required for cloud registration)
    config.InitializeDtdl();

    return config;
}
```

## Adding Transforms via Code

### Calibration Transform

```csharp
using Weda.SubNode.Core.Transforms;

var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// Add calibration transform
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

### Multiple Transforms

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        // First: Apply calibration
        pipeline.Add(new CalibrationTransform
        {
            Scale = 0.1,
            Offset = -273.15  // Convert to Celsius
        });

        // Second: Unit conversion
        pipeline.Add(new UnitConversionTransform
        {
            FromUnit = "celsius",
            ToUnit = "fahrenheit"
        });
    });
```

### Adding DSP Filters

```csharp
using Weda.SubNode.Core.Dsp;

tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform { Scale = 0.1 });
    })
    .ConfigureDspFilters(pipeline =>
    {
        // Add moving average filter
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });

        // Add Kalman filter for noise reduction
        pipeline.Add(new KalmanFilter
        {
            ProcessNoise = 0.01,
            MeasurementNoise = 0.1
        });
    });
```

## Direct Sensor Manipulation

### Creating Sensors Directly

```csharp
using Weda.SubNode.Abstractions.Telemetry;

var sensor = new Sensor
{
    Name = "pressure.sensor",
    SensorGroup = SensorGroup.AI,
    Parameters = new Dictionary<string, object>
    {
        ["RegisterType"] = "HoldingRegister",
        ["RegisterAddress"] = 10,
        ["RegisterCount"] = 2,
        ["DataType"] = "Float32"
    },
    Report = new SensorReport
    {
        Enabled = true,
        Interval = 3000,
        Unit = "pascal"
    },
    SensorInfo = new SensorInfo
    {
        Schema = "double",
        DisplayName = "Pressure Sensor",
        Description = "Measures atmospheric pressure"
    }
};
```

### Adding Transforms to Existing Sensor

```csharp
// Using AddTransform method
sensor.Report.AddTransform(new CalibrationTransform
{
    Scale = 0.001,
    Offset = 101325  // Standard atmospheric pressure
});

// Using ConfigureTransforms builder
sensor.Report.ConfigureTransforms(pipeline =>
{
    pipeline
        .Clear()  // Remove existing transforms
        .Add(new CalibrationTransform { Scale = 0.001 });
});
```

## Setting Thresholds

```csharp
sensor.Report.Thresholds = new ThresholdConfig
{
    LowerWarning = 95000,     // Low pressure warning
    LowerCritical = 90000,    // Low pressure critical
    UpperWarning = 107000,    // High pressure warning
    UpperCritical = 110000    // High pressure critical
};
```

## Modbus Data Types

Available data types for Modbus sensors:

| DataType | Registers | Description |
|----------|-----------|-------------|
| `UInt16` | 1 | 16-bit unsigned integer |
| `Int16` | 1 | 16-bit signed integer |
| `UInt32` | 2 | 32-bit unsigned integer |
| `Int32` | 2 | 32-bit signed integer |
| `Float32` | 2 | 32-bit floating point |
| `Float64` | 4 | 64-bit floating point |
| `String16` | N | 16-bit character string |

## Modbus Register Types

| RegisterType | Modbus Function | Description |
|--------------|-----------------|-------------|
| `HoldingRegister` | FC03 | Read/Write registers |
| `InputRegister` | FC04 | Read-only registers |
| `Coil` | FC01 | Read/Write bits |
| `DiscreteInput` | FC02 | Read-only bits |

## Best Practices

1. **Use strongly-typed configurations** - Prefer `TcpModbusDeviceConfiguration` over raw dictionaries
2. **Initialize DTDL** - Always call `InitializeDtdl()` for cloud registration
3. **Set meaningful names** - Use descriptive sensor names like `temp.zone1` instead of `sensor1`
4. **Configure intervals appropriately** - Balance data freshness with system load
5. **Apply transforms in order** - Calibration before unit conversion

## See Also

- [Configuration via JSON](./configuration-via-json.md) - JSON-based configuration
- [Configuration Reference](./configuration-reference.md) - Complete reference
- [Data Pipeline](../05-data-pipeline/pipeline-overview.md) - Transform details

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

---
sidebar_position: 1
sidebar_label: 'Configuration via Code'
hide_title: true
title: 'Sensor Configuration via Code'
keywords: ['SubNode', 'Sensor', 'Configuration', 'Code', 'Programmatic']
description: 'Configure sensors programmatically using the SubNode SDK API'
---

# Configuration via Code

> Configure sensors programmatically using the SubNode SDK API.

## Overview

Programmatic configuration is useful when:
- You need dynamic sensor setup based on runtime conditions
- Sensor definitions come from an external source
- You prefer compile-time validation of configurations

## Basic Sensor Configuration

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
    Port = 502,
    SlaveId = 1
};
```

### Adding Sensors

```csharp
// Create a temperature sensor
var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature_sensor",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// Configure reporting interval (milliseconds)
tempSensor.Config.Interval = 5000;

// Add to device configuration
modbusConfig.AddSensor(tempSensor);
```

### Complete Example

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Abstractions.Telemetry;

// Create application context
using var context = new WedaApplicationContext(
    options => options.CloudService = WedaFactory.Cloud.Mock);

// Build device configuration
var deviceConfig = BuildDeviceConfiguration();

// Create SubNode with device
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyDevice(context, deviceConfig));

await subNode.InitializeAsync();
await subNode.StartAsync();

DeviceConfiguration BuildDeviceConfiguration()
{
    var modbusConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "TemperatureMonitor",
        Manufacturer = "YourCompany",
        Model = "TempMon-v1",
        Host = "192.168.1.100",
        Port = 502,
        SlaveId = 1
    };

    // Add multiple sensors
    modbusConfig.AddSensor(new ModbusSensorReporturation
    {
        Name = "temp.zone1",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    });

    modbusConfig.AddSensor(new ModbusSensorReporturation
    {
        Name = "temp.zone2",
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

    // Convert to DeviceConfiguration
    var config = modbusConfig.ToDeviceConfiguration();

    // Initialize DTDL metadata (required for cloud registration)
    config.InitializeDtdl();

    return config;
}
```

## Adding Transforms via Code

### Calibration Transform

```csharp
using Weda.SubNode.Core.Transforms;

var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// Add calibration transform
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

### Multiple Transforms

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        // First: Apply calibration
        pipeline.Add(new CalibrationTransform
        {
            Scale = 0.1,
            Offset = -273.15  // Convert to Celsius
        });

        // Second: Unit conversion
        pipeline.Add(new UnitConversionTransform
        {
            FromUnit = "celsius",
            ToUnit = "fahrenheit"
        });
    });
```

### Adding DSP Filters

```csharp
using Weda.SubNode.Core.Dsp;

tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform { Scale = 0.1 });
    })
    .ConfigureDspFilters(pipeline =>
    {
        // Add moving average filter
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });

        // Add Kalman filter for noise reduction
        pipeline.Add(new KalmanFilter
        {
            ProcessNoise = 0.01,
            MeasurementNoise = 0.1
        });
    });
```

## Direct Sensor Manipulation

### Creating Sensors Directly

```csharp
using Weda.SubNode.Abstractions.Telemetry;

var sensor = new Sensor
{
    Name = "pressure.sensor",
    SensorGroup = SensorGroup.AI,
    Parameters = new Dictionary<string, object>
    {
        ["RegisterType"] = "HoldingRegister",
        ["RegisterAddress"] = 10,
        ["RegisterCount"] = 2,
        ["DataType"] = "Float32"
    },
    Report = new SensorReport
    {
        Enabled = true,
        Interval = 3000,
        Unit = "pascal"
    },
    SensorInfo = new SensorInfo
    {
        Schema = "double",
        DisplayName = "Pressure Sensor",
        Description = "Measures atmospheric pressure"
    }
};
```

### Adding Transforms to Existing Sensor

```csharp
// Using AddTransform method
sensor.Report.AddTransform(new CalibrationTransform
{
    Scale = 0.001,
    Offset = 101325  // Standard atmospheric pressure
});

// Using ConfigureTransforms builder
sensor.Report.ConfigureTransforms(pipeline =>
{
    pipeline
        .Clear()  // Remove existing transforms
        .Add(new CalibrationTransform { Scale = 0.001 });
});
```

## Setting Thresholds

```csharp
sensor.Report.Thresholds = new ThresholdConfig
{
    LowerWarning = 95000,     // Low pressure warning
    LowerCritical = 90000,    // Low pressure critical
    UpperWarning = 107000,    // High pressure warning
    UpperCritical = 110000    // High pressure critical
};
```

## Modbus Data Types

Available data types for Modbus sensors:

| DataType | Registers | Description |
|----------|-----------|-------------|
| `UInt16` | 1 | 16-bit unsigned integer |
| `Int16` | 1 | 16-bit signed integer |
| `UInt32` | 2 | 32-bit unsigned integer |
| `Int32` | 2 | 32-bit signed integer |
| `Float32` | 2 | 32-bit floating point |
| `Float64` | 4 | 64-bit floating point |
| `String16` | N | 16-bit character string |

## Modbus Register Types

| RegisterType | Modbus Function | Description |
|--------------|-----------------|-------------|
| `HoldingRegister` | FC03 | Read/Write registers |
| `InputRegister` | FC04 | Read-only registers |
| `Coil` | FC01 | Read/Write bits |
| `DiscreteInput` | FC02 | Read-only bits |

## Best Practices

1. **Use strongly-typed configurations** - Prefer `TcpModbusDeviceConfiguration` over raw dictionaries
2. **Initialize DTDL** - Always call `InitializeDtdl()` for cloud registration
3. **Set meaningful names** - Use descriptive sensor names like `temp.zone1` instead of `sensor1`
4. **Configure intervals appropriately** - Balance data freshness with system load
5. **Apply transforms in order** - Calibration before unit conversion

## See Also

- [Configuration via JSON](./configuration-via-json.md) - JSON-based configuration
- [Configuration Reference](./configuration-reference.md) - Complete reference
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Transform details

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

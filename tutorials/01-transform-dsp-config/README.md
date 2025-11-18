---
title: "Transform & DSP Filter - Configuration Approach"
description: "Learn how to apply Transform and DSP Filter using appsettings.json configuration"
version: "0.0.1"
author: "Rain Hu"
date: "2025-11-18"
lang: "en"
---
# Transform & DSP Filter Example - Configuration Approach

This example demonstrates how to apply **Transform** (UnitConversion) and **DSP Filter** (MovingAverage) using `appsettings.json` configuration with the `wedabuilder` template.

## What This Example Shows

-  **Configuration-based setup**: All transforms and filters defined in `appsettings.json`
-  **Zero code changes**: No programmatic configuration needed
-  **CalibrationTransform**: Scale and offset calibration
-  **UnitConversionTransform**: Convert Celsius to Fahrenheit
-  **MovingAverageFilter**: Smooth sensor data with window size 5
-  **Order parameter**: Control execution sequence via config

## Pipeline Configuration

The example applies the following pipeline to the temperature sensor:

```
Raw Data → Calibration → UnitConversion (°C→°F) → MovingAverage(5) → Output
```

### Transform Pipeline (executed first)
1. **CalibrationTransform** (Order=0): `scale=1.0, offset=0.0`
2. **UnitConversionTransform** (Order=1): `celsius` → `fahrenheit`

### DSP Pipeline (executed after transforms)
1. **MovingAverageFilter** (Order=0): Window size = 5

## How It Works

All configuration is in `appsettings.json`:

```json
{
  "Sensors": [
    {
      "Name": "temperature.sensor",
      "Config": {
        "TransformPipeline": [
          {
            "Type": "Calibration",
            "Enabled": true,
            "Order": 0,
            "Parameters": {
              "Scale": 1.0,
              "Offset": 0.0
            }
          },
          {
            "Type": "UnitConversion",
            "Enabled": true,
            "Order": 1,
            "Parameters": {
              "TargetResourceId": "*",
              "FromUnit": "celsius",
              "ToUnit": "fahrenheit"
            }
          }
        ],
        "DspPipeline": [
          {
            "Type": "MovingAverage",
            "Enabled": true,
            "Order": 0,
            "Parameters": {
              "WindowSize": 5
            }
          }
        ]
      }
    }
  ]
}
```

The device code (`MyFirstDevice.cs`) requires **no pipeline configuration** - everything is loaded automatically from the config file!

## Prerequisites

- .NET 9.0 SDK
- Weda SubNode SDK templates installed

## Quick Start

### 1. Run with Mock Simulator

The example includes a built-in Modbus simulator that generates temperature data:

```bash
cd TransformDspConfig
dotnet run
```

**Expected Output:**
```
[12:34:56 INF] Transform and DSP Filter loaded from appsettings.json for temperature.sensor
[12:34:56 INF] TransformPipeline: 2 transforms configured
[12:34:56 INF]   - [Order=0] Calibration (Enabled=True)
[12:34:56 INF]   - [Order=1] UnitConversion (Enabled=True)
[12:34:56 INF] DspPipeline: 1 filters configured
[12:34:56 INF]   - [Order=0] MovingAverage (Enabled=True)
[12:34:56 INF] Pipeline: Calibration → UnitConversion (°C→°F) → MovingAverage(5)
[12:34:57 INF] temperature.sensor: 77.23°F (after config-based pipeline)
[12:34:58 INF] temperature.sensor: 77.45°F (after config-based pipeline)
```

### 2. Connect to Real Modbus Device

Edit `appsettings.json` to point to your real device:

```json
{
  "DeviceConfigs": {
    "MyFirstDeviceConfig": {
      "Communication": {
        "Host": "192.168.1.100",  // Your device IP
        "Port": 502,
        "SlaveId": 1
      }
    }
  }
}
```

Then remove the simulator from `Program.cs`:

```csharp
// Comment out or remove:
// builder.Services.AddHostedService(sp => ...);
```

## Key Files

| File | Description |
|------|-------------|
| `MyFirstDevice.cs` | Device implementation (no pipeline code needed!) |
| `Program.cs` | Application entry point with Modbus simulator setup |
| `appsettings.json` | **Main file** - Contains all Transform/DSP configuration |
| `appsettings.template.json` | Template for deployment |

## Customization

All customization is done in `appsettings.json` - no code changes needed!

### Change Unit Conversion

```json
{
  "Type": "UnitConversion",
  "Parameters": {
    "FromUnit": "celsius",
    "ToUnit": "kelvin"  // Changed to kelvin
  }
}
```

### Adjust Moving Average Window

```json
{
  "Type": "MovingAverage",
  "Parameters": {
    "WindowSize": 10  // Changed from 5 to 10
  }
}
```

### Disable a Transform

```json
{
  "Type": "Calibration",
  "Enabled": false,  // Disabled
  "Parameters": { ... }
}
```

### Add Kalman Filter

```json
{
  "DspPipeline": [
    {
      "Type": "MovingAverage",
      "Enabled": true,
      "Order": 0,
      "Parameters": { "WindowSize": 5 }
    },
    {
      "Type": "Kalman",
      "Enabled": true,
      "Order": 1,
      "Parameters": {
        "ProcessNoise": 0.1,
        "MeasurementNoise": 0.1,
        "EstimationError": 0.1
      }
    }
  ]
}
```

### Add Multiple Sensors with Different Pipelines

```json
{
  "Sensors": [
    {
      "Name": "temp1",
      "Config": {
        "TransformPipeline": [
          { "Type": "UnitConversion", "Order": 0, "Parameters": { "FromUnit": "celsius", "ToUnit": "fahrenheit" } }
        ],
        "DspPipeline": [
          { "Type": "MovingAverage", "Order": 0, "Parameters": { "WindowSize": 5 } }
        ]
      }
    },
    {
      "Name": "temp2",
      "Config": {
        "TransformPipeline": [
          { "Type": "UnitConversion", "Order": 0, "Parameters": { "FromUnit": "kelvin", "ToUnit": "celsius" } }
        ],
        "DspPipeline": [
          { "Type": "Kalman", "Order": 0, "Parameters": { "ProcessNoise": 0.1, "MeasurementNoise": 0.1, "EstimationError": 0.1 } }
        ]
      }
    }
  ]
}
```

## Advantages of Configuration Approach

 **No code changes**: Update pipelines without recompiling  
 **Easy deployment**: Change config per environment  
 **Cloud synchronization**: Can be updated from Weda.Core  
 **Version control friendly**: Track config changes separately  
 **Non-developers can modify**: No programming knowledge needed  

## Configuration Reference

### Available Transform Types

| Type | Parameters | Description |
|------|------------|-------------|
| `Calibration` | `Scale`, `Offset` | Linear calibration: `output = input × Scale + Offset` |
| `Calibration` | `CalibrationCurve` | Curve-based calibration with interpolation |
| `UnitConversion` | `FromUnit`, `ToUnit`, `TargetResourceId` | Convert between units (temp, pressure, etc.) |

### Available DSP Filter Types

| Type | Parameters | Description |
|------|------------|-------------|
| `MovingAverage` | `WindowSize` | Simple moving average filter |
| `Kalman` | `ProcessNoise`, `MeasurementNoise`, `EstimationError` | Kalman filter for optimal estimation |
| `ReLU` | `Threshold` | ReLU activation function |

### Common Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Type` | string |  | Transform/Filter type name |
| `Enabled` | boolean |  | Enable/disable (default: true) |
| `Order` | number |  | Execution order (default: 0) |
| `Parameters` | object |  | Type-specific parameters |

## Related Examples

- [Transform DSP Programmatic Example](../transform-dsp-programmatic/) - Same functionality using code
- [Transformation Documentation](../../docs/wiki/zh/02_use_cases/02_transformation.md) - Complete guide

## Learn More

- [Transformation Pipeline Guide](../../docs/wiki/zh/02_use_cases/02_transformation.md)
- [DSP Filters Guide](../../docs/wiki/zh/02_use_cases/03_dsp_filters.md)
- [appsettings.json Configuration](../../docs/wiki/zh/03_advanced/appsettings_configuration.md)

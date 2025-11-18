---
title: "Transform & DSP Filter - Programmatic Approach"
description: "Learn how to apply Transform and DSP Filter using code configuration"
version: "0.0.1"
author: "Rain Hu"
date: "2025-11-18"
lang: "en"
---
# Transform & DSP Filter Example - Programmatic Approach

This example demonstrates how to apply **Transform** (UnitConversion) and **DSP Filter** (MovingAverage) programmatically using the `wedabuilder` template.

## What This Example Shows

-  **Programmatic configuration**: Add transforms and filters in code
-  **CalibrationTransform**: Scale and offset calibration
-  **UnitConversionTransform**: Convert Celsius to Fahrenheit
-  **MovingAverageFilter**: Smooth sensor data with window size 5
-  **Execution order**: Transform → DSP Filter

## Pipeline Configuration

The example applies the following pipeline to the temperature sensor:

```
Raw Data → Calibration → UnitConversion (°C→°F) → MovingAverage(5) → Output
```

### Transform Pipeline (executed first)
1. **CalibrationTransform**: `scale=1.0, offset=0.0` (identity, for demonstration)
2. **UnitConversionTransform**: Convert `celsius` → `fahrenheit`

### DSP Pipeline (executed after transforms)
1. **MovingAverageFilter**: Window size = 5

## How It Works

The configuration is done in `MyFirstDevice.cs`:

```csharp
private void ConfigureTransformsAndFilters()
{
    var tempSensor = Configuration.Sensors.FirstOrDefault(s => s.Name == "temperature.sensor");
    if (tempSensor != null)
    {
        // Transform Pipeline
        tempSensor.Config
            .AddTransform(new CalibrationTransform(scale: 1.0, offset: 0.0))
            .AddTransform(new UnitConversionTransform("*", "celsius", "fahrenheit"));

        // DSP Pipeline
        tempSensor.Config
            .AddDspFilter(new MovingAverageFilter(windowSize: 5));
    }
}
```

## Prerequisites

- .NET 9.0 SDK
- Weda SubNode SDK templates installed

## Quick Start

### 1. Run with Mock Simulator

The example includes a built-in Modbus simulator that generates temperature data:

```bash
cd TransformDspProgrammatic
dotnet run
```

**Expected Output:**
```
[12:34:56 INF] Configuring Transform and DSP Filter for temperature.sensor
[12:34:56 INF] Applied: Calibration → UnitConversion (°C→°F) → MovingAverage(5)
[12:34:57 INF] temperature.sensor: 77.23°F (after calibration, unit conversion, and moving average)
[12:34:58 INF] temperature.sensor: 77.45°F (after calibration, unit conversion, and moving average)
[12:34:59 INF] temperature.sensor: 77.68°F (after calibration, unit conversion, and moving average)
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
| `MyFirstDevice.cs` | Device implementation with programmatic Transform/DSP configuration |
| `Program.cs` | Application entry point with Modbus simulator setup |
| `appsettings.json` | Device and sensor configuration |
| `appsettings.template.json` | Template for deployment |

## Customization

### Change Unit Conversion

```csharp
// Celsius to Kelvin
.AddTransform(new UnitConversionTransform("*", "celsius", "kelvin"));

// Fahrenheit to Celsius
.AddTransform(new UnitConversionTransform("*", "fahrenheit", "celsius"));
```

### Adjust Moving Average Window

```csharp
// Smaller window = more responsive but noisier
.AddDspFilter(new MovingAverageFilter(windowSize: 3));

// Larger window = smoother but slower response
.AddDspFilter(new MovingAverageFilter(windowSize: 10));
```

### Add Calibration

```csharp
// Example: Raw sensor reads 10x higher and needs -5 offset
.AddTransform(new CalibrationTransform(scale: 0.1, offset: -5.0));
```

### Add Multiple Sensors with Different Pipelines

```csharp
// Sensor 1: Celsius → Fahrenheit + MovingAverage
var tempSensor1 = Configuration.Sensors.First(s => s.Name == "temp1");
tempSensor1.Config
    .AddTransform(new UnitConversionTransform("*", "celsius", "fahrenheit"))
    .AddDspFilter(new MovingAverageFilter(5));

// Sensor 2: Kelvin → Celsius + Kalman Filter
var tempSensor2 = Configuration.Sensors.First(s => s.Name == "temp2");
tempSensor2.Config
    .AddTransform(new UnitConversionTransform("*", "kelvin", "celsius"))
    .AddDspFilter(new KalmanFilter(0.1, 0.1, 0.1));
```

## Advantages of Programmatic Approach

 **Full control**: Different sensors can have different pipelines  
 **Dynamic configuration**: Can change based on runtime conditions  
 **Type safety**: Compile-time checking  
 **Complex logic**: Conditional transforms based on sensor properties  

## Related Examples

- [Transform DSP Config Example](../transform-dsp-config/) - Same functionality using `appsettings.json`
- [Transformation Documentation](../../docs/wiki/zh/02_use_cases/02_transformation.md) - Complete guide

## Learn More

- [Transformation Pipeline Guide](../../docs/wiki/zh/02_use_cases/02_transformation.md)
- [DSP Filters Guide](../../docs/wiki/zh/02_use_cases/03_dsp_filters.md)
- [appsettings.json Configuration](../../docs/wiki/zh/03_advanced/appsettings_configuration.md)

---
sidebar_position: 1
sidebar_label: 'Pipeline Overview'
hide_title: true
title: 'Data Pipeline Overview | SubNode SDK'
keywords: ['SubNode', 'Pipeline', 'Transform', 'DSP', 'Data Processing', 'Threshold']
description: 'Learn about the SubNode Data Pipeline architecture, execution order, and configuration methods.'
---

# Data Pipeline Overview

> Learn about the SubNode Data Pipeline architecture, execution order, and configuration methods.

## Overview

Each Sensor in SubNode can be configured with a Data Pipeline that sequentially processes raw values read from the device through Transform, DSP Filter, and Threshold stages before sending them to the cloud or local storage. The Pipeline is designed to be composable -- each stage is optional, and multiple processors of the same type can be chained together.

## What You'll Learn

After reading this article, you will be able to:

- Understand the three processing stages of the Data Pipeline and their execution order
- Distinguish between Transform and DSP Filter
- Configure the Pipeline using both JSON and code

## Prerequisites

- Completed [Configuration via JSON](../04-configuration/02-configuration-via-json.md)

---

## Pipeline Architecture

```text
┌───────────────────────────────────────────────────────────────────┐
│                      Sensor Data Pipeline                         │
├───────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────┐    ┌──────────────┐    ┌───────────┐                │
│  │   Raw    │    │  Transform   │    │    DSP    │                │
│  │  Value   │───>│  Pipeline    │───>│  Pipeline │───> Final      │
│  │          │    │              │    │           │     Value      │
│  │ (device) │    │ calibration  │    │ moving    │    (Cloud/     │
│  │          │    │ unitconv     │    │  average  │     Storage)   │
│  │          │    │ chunking     │    │ kalman    │                │
│  │          │    │              │    │ relu      │                │
│  └──────────┘    └──────────────┘    └───────────┘                │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

## Execution Order

The three Pipeline stages are always executed in the following fixed order:

| Order | Stage | Interface | Purpose |
|-------|-------|-----------|---------|
| 1 | **TransformPipeline** | `ITelemetryTransform` | Value transformation (calibration, unit conversion, chunking) |
| 2 | **DspPipeline** | `IDspFilter` | Signal processing (smoothing, filtering, noise reduction) |

> **Note**: `ThresholdConfig` is already defined in the SDK (supporting `LowerWarning`, `UpperCritical`, etc.), but it is not yet integrated into the automatic Pipeline execution. Future versions will support Threshold-triggered alerts.

Within each stage, processors are executed in **array order** (JSON) or **insertion order** (code).

---

## Transform vs DSP Filter

| Aspect | Transform | DSP Filter |
|--------|-----------|------------|
| Interface | `ITelemetryTransform` | `IDspFilter` |
| Purpose | Value transformation, format conversion | Signal quality processing |
| Stateful | Typically stateless | Typically stateful (requires historical data) |
| Typical use cases | Calibration, unit conversion, chunking | Moving average, Kalman filtering |
| Execution order | Before DSP Filter | After Transform |

---

## Built-in Transforms

| Type | Class | Description |
|------|-------|-------------|
| `calibration` | `CalibrationTransform` | Linear calibration or curve calibration |
| `unitconversion` | `UnitConversionTransform` | Unit conversion |
| `chunking` | `ChunkingTransform` | Large data chunked transmission |

## Built-in DSP Filters

| Type | Class | Description |
|------|-------|-------------|
| `movingAverage` | `MovingAverageFilter` | Moving average (noise smoothing) |
| `kalman` | `KalmanFilter` | Kalman filter (prediction + noise reduction) |
| `relu` | `ReluFilter` | ReLU filter (clamp negative values to zero) |

---

## JSON Configuration

Configure in the Sensor `Report` section of `devicecfg.json`:

```json
{
  "Name": "temperature_sensor",
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "TransformPipeline": [
      {
        "Type": "calibration",
        "Enabled": true,
        "Parameters": {
          "Scale": 0.1,
          "Offset": -40.0
        }
      },
      {
        "Type": "unitconversion",
        "Enabled": true,
        "Parameters": {
          "FromUnit": "celsius",
          "ToUnit": "fahrenheit"
        }
      }
    ],
    "DspPipeline": [
      {
        "Type": "movingAverage",
        "Enabled": true,
        "Parameters": {
          "WindowSize": 5
        }
      }
    ],
    "Thresholds": {
      "LowerWarning": 0,
      "LowerCritical": -10,
      "UpperWarning": 50,
      "UpperCritical": 60
    }
  }
}
```

Each processor has an `Enabled` field. Set it to `false` to skip the processor without removing the configuration.

## Programmatic Configuration

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform(scale: 0.1, offset: -40.0));
        pipeline.Add(new UnitConversionTransform
        {
            FromUnit = "celsius",
            ToUnit = "fahrenheit"
        });
    })
    .ConfigureDspFilters(pipeline =>
    {
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });
    });

tempSensor.Config.Report.Thresholds = new ThresholdConfig
{
    LowerWarning = 0,
    LowerCritical = -10,
    UpperWarning = 50,
    UpperCritical = 60
};
```

---

## Threshold (Planned)

The SDK already defines the `ThresholdConfig` data model, allowing threshold values to be pre-configured in settings. It is not yet automatically integrated into the Pipeline execution. Future versions will support automatic alert triggering.

| Level | Condition |
|-------|-----------|
| `Normal` | Within all threshold ranges |
| `LowerWarning` | value <= LowerWarning |
| `LowerCritical` | value <= LowerCritical |
| `UpperWarning` | value >= UpperWarning |
| `UpperCritical` | value >= UpperCritical |

---

## Monitoring the Pipeline

Monitor Pipeline input and output through events:

```csharp
// Raw data from device (before pipeline)
device.EnableDataReceivedTracking = true;
device.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Raw: {e.Data[0].Value}");
};

// Processed data (after pipeline)
device.EnableDataProcessedTracking = true;
device.DataProcessed += (sender, e) =>
{
    Console.WriteLine($"Processed: {e.Data[0].Value}");
};
```

> Events are disabled by default to improve performance. Enable them only during development and debugging.

---

## Summary

- The Data Pipeline has two fixed-order processing stages: Transform -> DSP Filter (Threshold is planned)
- Transform handles value transformation (stateless), DSP Filter handles signal processing (stateful)
- 3 built-in Transforms (calibration, unitconversion, chunking) and 3 built-in DSP Filters (movingAverage, kalman, relu)
- Configure via JSON or code; each processor can be individually enabled/disabled
- Use `DataReceived` and `DataProcessed` events to monitor Pipeline input and output

## See Also

- [Transformations](./02-transformations.md) - Detailed Transform documentation
- [DSP Filters](./03-dsp-filters.md) - Detailed DSP Filter documentation
- [Configuration via JSON](../04-configuration/02-configuration-via-json.md) - Report configuration

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

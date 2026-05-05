---
sidebar_position: 1
sidebar_label: 'Pipeline Overview'
hide_title: true
title: 'Data Pipeline Overview'
keywords: ['SubNode', 'Pipeline', 'Transform', 'DSP', 'Data Processing']
description: 'Understanding the SubNode data processing pipeline architecture'
---

# Pipeline Overview

> Understanding the SubNode data processing pipeline architecture.

## What is the Data Pipeline?

The Data Pipeline is a sequence of processing stages that telemetry data flows through before being sent to the cloud or stored locally. It enables:

- **Data Transformation** - Calibration, unit conversion
- **Signal Processing** - Filtering, smoothing, noise reduction
- **Alert Generation** - Threshold monitoring

## Pipeline Stages

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                              Data Pipeline                                   │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐   │
│   │   Raw       │    │  Transform  │    │    DSP      │    │  Threshold  │   │
│   │   Value     │───>│  Pipeline   │───>│  Pipeline   │───>│   Check     │   │
│   │             │    │             │    │             │    │             │   │
│   │ From device │    │ Calibration │    │ Moving Avg  │    │ Alert if    │   │
│   │             │    │ Unit Conv   │    │ Kalman      │    │ out of range│   │
│   └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘   │
│                                                                              │
│                                         │                                    │
│                                         v                                    │
│                              ┌─────────────────────┐                         │
│                              │   Final Value       │                         │
│                              │   (Cloud / Storage) │                         │
│                              └─────────────────────┘                         │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Stage 1: Raw Value

The raw value read from the device via the protocol parser. This is the unprocessed sensor reading.

### Stage 2: Transform Pipeline

Transforms modify the raw value sequentially. Common transforms:

| Transform | Purpose | Example |
|-----------|---------|---------|
| Calibration | Apply scale and offset | `value * 0.1 + 10` |
| Unit Conversion | Convert units | Celsius to Fahrenheit |
| Chunking | Batch data points | Group 10 samples |

### Stage 3: DSP Pipeline

Digital Signal Processing filters for signal quality:

| Filter | Purpose | Example |
|--------|---------|---------|
| Moving Average | Smooth noise | 5-sample average |
| Kalman | Predict and filter | Noise reduction |
| Low-pass | Remove high frequency | < 10Hz passthrough |
| High-pass | Remove low frequency | > 1Hz passthrough |

### Stage 4: Threshold Check

Evaluate the processed value against defined thresholds:

| Level | Condition | Action |
|-------|-----------|--------|
| Normal | Within bounds | No action |
| Warning | Approaching limit | Log warning |
| Critical | Exceeded limit | Alert generation |

## Processing Order

The order of execution is fixed:

1. **Transforms first** - Always applied before DSP filters
2. **DSP filters second** - Applied after all transforms
3. **Thresholds last** - Evaluated on final processed value

Within each pipeline, operations execute in **array order** (index 0, 1, 2, ...).

## Configuration Methods

### Via JSON

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "TransformPipeline": [
      {
        "Type": "calibration",
        "Enabled": true,
        "Parameters": { "Scale": 0.1, "Offset": -10 }
      }
    ],
    "DspPipeline": [
      {
        "Type": "movingAverage",
        "Enabled": true,
        "Parameters": { "WindowSize": 5 }
      }
    ],
    "Thresholds": {
      "UpperWarning": 35,
      "UpperCritical": 40
    }
  }
}
```

### Via Code

```csharp
sensor.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform { Scale = 0.1, Offset = -10 });
    })
    .ConfigureDspFilters(pipeline =>
    {
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });
    });

sensor.Report.Thresholds = new ThresholdConfig
{
    UpperWarning = 35,
    UpperCritical = 40
};
```

## Enabling/Disabling Pipeline Steps

Each step can be individually enabled or disabled:

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": false,      // <-- Disabled, will be skipped
      "Parameters": { "Scale": 0.1 }
    },
    {
      "Type": "unitconversion",
      "Enabled": true,       // <-- Enabled
      "Parameters": { "FromUnit": "celsius", "ToUnit": "fahrenheit" }
    }
  ]
}
```

## Pipeline Events

Subscribe to pipeline events for monitoring:

```csharp
// Raw data from device
device.EnableDataReceivedTracking = true;
device.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Raw: {e.Data[0].Value}");
};

// After pipeline processing
device.EnableDataProcessedTracking = true;
device.DataProcessed += (sender, e) =>
{
    Console.WriteLine($"Processed: {e.Data[0].Value}");
};
```

## Best Practices

1. **Order matters** - Place calibration before unit conversion
2. **Enable selectively** - Only enable pipeline steps you need
3. **Test transforms** - Verify transform parameters with known values
4. **Monitor performance** - Complex DSP filters add latency
5. **Use thresholds wisely** - Too many alerts reduce their effectiveness

## See Also

- [Transformation](./transformation.md) - Transform details
- [DSP Filters](./dsp-filters.md) - DSP filter details
- [Sensor Configuration](../04-sensor-configuration/configuration-reference.md) - Configuration reference

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />

---
sidebar_position: 3
sidebar_label: 'DSP Filters'
hide_title: true
title: 'DSP Filters | SubNode SDK'
keywords: ['SubNode', 'DSP', 'Filter', 'MovingAverage', 'Kalman', 'ReLU']
description: 'Learn about the built-in DSP Filter types and usage in SubNode.'
---

# DSP Filters

> Learn about the built-in DSP Filter types and usage in SubNode.

## Overview

DSP (Digital Signal Processing) Filters are the second processing stage of the Data Pipeline, executed after Transforms. Unlike Transforms, DSP Filters are typically **stateful** -- they need to remember historical data to compute results (e.g., a moving average requires previous values). SubNode includes three built-in DSP Filters, each maintaining independent state per Sensor.

## What You'll Learn

After reading this article, you will be able to:

- Use the Moving Average Filter to smooth out noise
- Use the Kalman Filter for prediction and noise reduction
- Use the ReLU Filter to filter out negative values
- Understand the stateful nature and warm-up behavior of DSP Filters

## Prerequisites

- Complete [Pipeline Overview](./01-overview.md)

---

## Built-in DSP Filter Overview

| Type Name | Class | Purpose | Statefulness |
|-----------|-------|---------|--------------|
| `movingaverage` | `MovingAverageFilter` | Moving average (noise smoothing) | Stateful (circular buffer) |
| `kalman` | `KalmanFilter` | Kalman filtering (prediction + noise reduction) | Stateful (estimate + covariance) |
| `relu` | `ReluFilter` | ReLU filtering (clamp negatives to zero) | Stateless |

---

## Moving Average Filter

Uses a circular buffer to implement a moving average with O(1) time complexity. Maintains an independent buffer for each Sensor.

**JSON:**

```json
{
  "DspPipeline": [
    {
      "Type": "movingAverage",
      "Enabled": true,
      "Parameters": {
        "Window": 5
      }
    }
  ]
}
```

**Code:**

```csharp
sensor.Report.ConfigureDspFilters(pipeline =>
{
    pipeline.Add(new MovingAverageFilter(window: 5));
});
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Window` | int | 5 | Window size for the moving average (must be > 0) |

### Behavior

- **Warm-up**: When the buffer is not yet full, the average is computed using the available data (it does not wait until the buffer is full to produce output)
- **Window change**: When the `Window` parameter is updated via the cloud, the buffer is reset and requires a new warm-up period
- **Per-Sensor state**: Each Sensor (by `ResourceId`) maintains its own independent buffer

### Example

Window = 3, input sequence: `10, 20, 30, 40, 50`

| Input | Buffer State | Output (Average) |
|-------|--------------|------------------|
| 10 | [10] | 10.0 |
| 20 | [10, 20] | 15.0 |
| 30 | [10, 20, 30] | 20.0 |
| 40 | [20, 30, 40] | 30.0 |
| 50 | [30, 40, 50] | 40.0 |

---

## Kalman Filter

A simple 1D Kalman Filter that reduces noise through prediction and measurement updates. Suitable for scenarios where sensor readings are noisy but the underlying trend is stable.

**JSON:**

```json
{
  "DspPipeline": [
    {
      "Type": "kalman",
      "Enabled": true,
      "Parameters": {
        "ProcessNoise": 0.01,
        "MeasurementNoise": 0.1
      }
    }
  ]
}
```

**Code:**

```csharp
sensor.Report.ConfigureDspFilters(pipeline =>
{
    pipeline.Add(new KalmanFilter(processNoise: 0.01, measurementNoise: 0.1));
});
```

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `ProcessNoise` | double | 0.01 | >= 0 | Process noise covariance (higher values trust new measurements more) |
| `MeasurementNoise` | double | 0.1 | > 0 | Measurement noise covariance (higher values trust predictions more) |

### Parameter Tuning Guide

| Scenario | ProcessNoise | MeasurementNoise | Effect |
|----------|-------------|------------------|--------|
| High sensor noise | Low (0.001) | High (1.0) | Heavy smoothing, slow response |
| Precise sensor | High (0.1) | Low (0.01) | Minimal smoothing, fast response |
| General purpose | 0.01 | 0.1 | Moderate smoothing |

### Behavior

- **Initialization**: The first measurement is used directly as the initial estimate
- **Parameter update**: When `ProcessNoise` / `MeasurementNoise` are updated, the state is preserved (no warm-up needed)
- **Per-Sensor state**: Each Sensor independently maintains its own estimate and error covariance

---

## ReLU Filter

The ReLU (Rectified Linear Unit) filter clamps negative values to 0. Suitable for sensors where negative values are physically impossible (e.g., power, brightness).

**JSON:**

```json
{
  "DspPipeline": [
    {
      "Type": "relu",
      "Enabled": true,
      "Parameters": {}
    }
  ]
}
```

**Code:**

```csharp
sensor.Report.ConfigureDspFilters(pipeline =>
{
    pipeline.Add(new ReluFilter());
});
```

Formula: `output = max(0, input)`

| Input | Output |
|-------|--------|
| 25.3 | 25.3 |
| -1.5 | 0.0 |
| 0.0 | 0.0 |
| 100.0 | 100.0 |

> The ReLU Filter has no parameters and no state.

---

## Chaining Multiple DSP Filters

DSP Filters are executed sequentially in array order:

```json
{
  "DspPipeline": [
    { "Type": "kalman", "Parameters": { "ProcessNoise": 0.01, "MeasurementNoise": 0.1 } },
    { "Type": "relu" }
  ]
}
```

Execution flow: `Transformed Value` -> Kalman (noise reduction) -> ReLU (clamp negatives to zero) -> `Final Value`

---

## Chaining Transforms and DSP Filters

Complete Pipeline example (Transform + DSP):

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "TransformPipeline": [
      { "Type": "calibration", "Parameters": { "Scale": 0.1, "Offset": -40 } }
    ],
    "DspPipeline": [
      { "Type": "movingAverage", "Parameters": { "Window": 5 } },
      { "Type": "relu" }
    ]
  }
}
```

Execution flow: `Raw` -> Calibration -> Moving Average -> ReLU -> `Final`

---

## Summary

- **Moving Average**: O(1) circular buffer implementation, `Window` parameter controls smoothing level, requires warm-up after changes
- **Kalman**: 1D Kalman Filter, `ProcessNoise` and `MeasurementNoise` control trust levels, state is preserved on parameter updates
- **ReLU**: Clamps negatives to zero, no parameters, no state
- All DSP Filters maintain independent state per Sensor and support dynamic parameter updates from the cloud

## See Also

- [Pipeline Overview](./01-overview.md) - Pipeline architecture and execution order
- [Transformations](./02-transformations.md) - Detailed explanation of Transforms
- [Kalman Filter Deep Dive](./03-01-kalman-filter.md) - Mathematical principles of the Kalman Filter

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

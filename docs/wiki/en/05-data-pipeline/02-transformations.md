---
sidebar_position: 2
sidebar_label: 'Transformations'
hide_title: true
title: 'Transformations | SubNode SDK'
keywords: ['SubNode', 'Transform', 'Calibration', 'UnitConversion', 'Chunking']
description: 'Learn about the built-in Transform types and usage in SubNode.'
---

# Transformations

> Learn about the built-in Transform types and usage in SubNode.

## Overview

Transform is the first processing stage of the Data Pipeline, responsible for performing numerical conversions on raw Sensor values. SubNode includes three built-in Transforms: linear/curve calibration, unit conversion, and large data chunking. Each can be configured via JSON or code, and supports dynamic parameter updates from the cloud.

## What You'll Learn

After reading this article, you will be able to:

- Use Calibration Transform for linear and curve calibration
- Use Unit Conversion Transform for temperature unit conversion
- Use Chunking Transform to transmit large data in chunks
- Understand how the `IConfigurableTransform` interface supports cloud parameter updates

## Prerequisites

- Complete [Pipeline Overview](./01-overview.md)

---

## Built-in Transform Overview

| Type Name | Class | Purpose | Statefulness |
|-----------|-------|---------|--------------|
| `calibration` | `CalibrationTransform` | Linear calibration or curve calibration | Stateless |
| `unitconversion` | `UnitConversionTransform` | Temperature unit conversion | Stateless |
| `chunking` | `ChunkingTransform` | Large data chunked transmission | Stateless |

---

## Calibration Transform

### Linear Calibration

Formula: `calibrated_value = (raw_value * Scale) + Offset`

**JSON:**

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": {
        "Scale": 0.1,
        "Offset": -40.0
      }
    }
  ]
}
```

**Code:**

```csharp
tempSensor.Config.Report.ConfigureTransforms(pipeline =>
{
    pipeline.Add(new CalibrationTransform(scale: 0.1, offset: -40.0));
});
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Scale` | double | 1.0 | Multiplication factor (must not be 0) |
| `Offset` | double | 0.0 | Additive offset |

### Curve Calibration

For non-linear sensors, you can provide calibration curve points. The framework performs linear interpolation between adjacent points:

**JSON:**

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": {
        "CalibrationCurve": [
          { "RawValue": 0,    "CalibratedValue": -20.0 },
          { "RawValue": 1000, "CalibratedValue": 0.0 },
          { "RawValue": 2000, "CalibratedValue": 25.0 },
          { "RawValue": 3000, "CalibratedValue": 50.0 },
          { "RawValue": 4095, "CalibratedValue": 80.0 }
        ]
      }
    }
  ]
}
```

**Code:**

```csharp
pipeline.Add(new CalibrationTransform(new List<CalibrationPoint>
{
    new() { RawValue = 0,    CalibratedValue = -20.0 },
    new() { RawValue = 1000, CalibratedValue = 0.0 },
    new() { RawValue = 2000, CalibratedValue = 25.0 },
    new() { RawValue = 3000, CalibratedValue = 50.0 },
    new() { RawValue = 4095, CalibratedValue = 80.0 }
}));
```

> When `CalibrationCurve` is present, `Scale` and `Offset` are ignored. Values outside the curve range use the boundary point values.

---

## Unit Conversion Transform

Currently supports conversion between temperature units.

**JSON:**

```json
{
  "TransformPipeline": [
    {
      "Type": "unitconversion",
      "Enabled": true,
      "Parameters": {
        "FromUnit": "celsius",
        "ToUnit": "fahrenheit"
      }
    }
  ]
}
```

**Code:**

```csharp
pipeline.Add(new UnitConversionTransform("celsius", "fahrenheit"));
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `FromUnit` | string | Source unit (required) |
| `ToUnit` | string | Target unit (required) |

### Supported Unit Conversions

| From | To | Formula |
|------|----|---------|
| Celsius (C) | Fahrenheit (F) | `F = C * 9/5 + 32` |
| Fahrenheit (F) | Celsius (C) | `C = (F - 32) * 5/9` |
| Celsius (C) | Kelvin (K) | `K = C + 273.15` |
| Kelvin (K) | Celsius (C) | `C = K - 273.15` |
| Fahrenheit (F) | Kelvin (K) | `K = (F - 32) * 5/9 + 273.15` |
| Kelvin (K) | Fahrenheit (F) | `F = (K - 273.15) * 9/5 + 32` |

> Unit names are case-insensitive. Both abbreviations (`C`, `F`, `K`) and full names (`celsius`, `fahrenheit`, `kelvin`) are supported.

---

## Chunking Transform

Splits large data (such as Base64-encoded images) into chunks for transmission. Each chunk includes transfer metadata that the receiver can use for reassembly.

**JSON:**

```json
{
  "TransformPipeline": [
    {
      "Type": "chunking",
      "Enabled": true,
      "Parameters": {
        "chunkSize": 131072
      }
    }
  ]
}
```

**Code:**

```csharp
pipeline.Add(new ChunkingTransform { ChunkSize = 128 * 1024 });
```

| Parameter | Type | Default | Range | Description |
|-----------|------|---------|-------|-------------|
| `chunkSize` | int | 256KB | 1KB - 750KB | Size of each chunk (bytes) |

### Chunk Metadata

Each chunk's `TelemetryMeasure.Metadata` will contain:

| Key | Type | Description |
|-----|------|-------------|
| `transferId` | string | Unique ID for this transfer (GUID) |
| `chunkIndex` | int | Chunk index (starting from 0) |
| `totalChunks` | int | Total number of chunks |
| `crc32Checksum` | uint | CRC32 checksum of the original data |

> Chunking only processes Base64 strings that exceed the `chunkSize`. JSON strings and small data pass through directly.

---

## Chaining Multiple Transforms

Transforms are executed sequentially in array order. A common chaining pattern:

```json
{
  "TransformPipeline": [
    { "Type": "calibration", "Parameters": { "Scale": 0.1, "Offset": -40 } },
    { "Type": "unitconversion", "Parameters": { "FromUnit": "celsius", "ToUnit": "fahrenheit" } }
  ]
}
```

Execution flow: `Raw(4000)` -> Calibration(`4000 * 0.1 - 40 = 360`) -> UnitConversion(`360°C -> 680°F`)

> Order matters. Calibration should come before unit conversion.

---

## Cloud Parameter Updates

All built-in Transforms implement the `IConfigurableTransform` interface, supporting dynamic parameter updates from the cloud without requiring a restart:

```csharp
public interface IConfigurableTransform<TSelf>
{
    static abstract string TypeName { get; }
    static abstract TSelf Create(Dictionary<string, object> parameters);
    ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters);
    void UpdateParameters(Dictionary<string, object> parameters);
}
```

When the cloud sends a Config Update, the framework will:

1. Call `ValidateParameters` to validate the new parameters
2. If validation passes, call `UpdateParameters` to apply the new parameters
3. If validation fails, reject the update and keep the original parameters

---

## Summary

- **Calibration**: Linear calibration (`Scale` + `Offset`) or curve calibration (`CalibrationCurve` + linear interpolation)
- **Unit Conversion**: Temperature unit conversion (Celsius / Fahrenheit / Kelvin)
- **Chunking**: Large data chunking, each chunk includes `transferId`, `chunkIndex`, `totalChunks`, `crc32Checksum`
- All Transforms support dynamic cloud parameter updates (`IConfigurableTransform`)

## See Also

- [Pipeline Overview](./01-overview.md) - Pipeline architecture and execution order
- [DSP Filters](./03-dsp-filters.md) - DSP Filter detailed explanation
- [Calibration Transform Details](./02-01-linear-transformation.md) - Advanced calibration usage

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |

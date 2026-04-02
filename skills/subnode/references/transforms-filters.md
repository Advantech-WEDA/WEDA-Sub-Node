# Transforms and DSP Filters Reference

Complete reference for data transformation and signal processing options.

Both transforms and DSP filters use a pluggable factory pattern with auto-discovery.
Type names are **case-insensitive**. Pipelines are applied in **array order**.

## Transforms (IConfigurableTransform)

Transforms modify individual telemetry values before transmission.

### Calibration Transform

Linear scaling/offset or curve-based non-linear calibration.

**Linear mode:**

```json
{
  "Type": "calibration",
  "Parameters": {
    "Scale": 0.1,
    "Offset": -40
  }
}
```

**Formula:** `output = (input * Scale) + Offset`

**Defaults:** Scale=1.0, Offset=0.0. Scale cannot be 0.

**Curve mode (non-linear interpolation):**

```json
{
  "Type": "calibration",
  "Parameters": {
    "CalibrationCurve": [
      { "RawValue": 0, "CalibratedValue": -40 },
      { "RawValue": 1000, "CalibratedValue": 0 },
      { "RawValue": 2000, "CalibratedValue": 50 },
      { "RawValue": 3000, "CalibratedValue": 100 }
    ]
  }
}
```

Uses linear interpolation between points. At least 1 point required.

**Use Cases:**
- Raw ADC to engineering units: Scale=0.00122, Offset=0 (for 12-bit ADC)
- Temperature sensor: Scale=0.1, Offset=-40 (for sensors returning value*10+40)
- Non-linear sensor calibration via CalibrationCurve

### Unit Conversion Transform

Temperature unit conversion.

```json
{
  "Type": "unitconversion",
  "Parameters": {
    "FromUnit": "celsius",
    "ToUnit": "fahrenheit"
  }
}
```

**Supported Units (case-insensitive):** `C`/`celsius`, `F`/`fahrenheit`, `K`/`kelvin`

All bidirectional conversions supported: C-F, C-K, F-K.

### Chunking Transform

Splits large payloads into chunks for bandwidth-limited scenarios.

```json
{
  "Type": "chunking",
  "Parameters": {
    "chunkSize": 262144
  }
}
```

**Default:** 262144 bytes (256KB). Range: 1024 (1KB) to 768000 (750KB).

Each chunk includes metadata: transferId, chunkIndex, totalChunks, crc32Checksum.

## DSP Filters (IConfigurableDspFilter)

Filters process streams of data for noise reduction and signal conditioning.

### Moving Average Filter

Simple averaging over a sliding window using circular buffer (O(1) complexity).

```json
{
  "Type": "movingaverage",
  "Parameters": {
    "Window": 5
  }
}
```

**Parameters:** `Window` (int, default: 5, must be > 0)

**Properties:**
- Reduces random noise
- Introduces lag proportional to window size
- Good for stable signals with white noise

**Use Cases:**
- Temperature smoothing
- Pressure readings
- General noise reduction

### Kalman Filter

Optimal estimation for noisy measurements with per-sensor state tracking.

```json
{
  "Type": "kalman",
  "Parameters": {
    "ProcessNoise": 0.01,
    "MeasurementNoise": 0.1
  }
}
```

**Parameters:**
- `ProcessNoise` (double, default: 0.01, must be >= 0)
- `MeasurementNoise` (double, default: 0.1, must be > 0)

**Properties:**
- Adapts to signal dynamics
- Maintains separate state per ResourceId
- States are preserved on parameter updates

**Use Cases:**
- Position/velocity tracking
- Fluctuating sensor readings
- Real-time estimation

### ReLU Filter

Rectified Linear Unit: clamps negative values to 0.

```json
{
  "Type": "relu"
}
```

No configurable parameters. Formula: `output = max(0, input)`

**Use Cases:**
- Ensuring non-negative sensor values
- Removing spurious negative readings from unsigned sensors

## Combining Transforms and Filters

Transforms and filters are applied in array order:

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 5000,
    "Transforms": [
      {
        "Type": "calibration",
        "Parameters": { "Scale": 0.1, "Offset": 0 }
      }
    ],
    "DspFilters": [
      {
        "Type": "movingaverage",
        "Parameters": { "Window": 3 }
      }
    ]
  }
}
```

**Processing Order:**
1. Raw sensor value received
2. Transforms applied (in order): Calibration scaling
3. DSP Filters applied (in order): Moving Average
4. Final value sent to cloud

## Common Sensor Pipeline Patterns

### Temperature Sensor (with calibration curve)

```json
{
  "Transforms": [
    {
      "Type": "calibration",
      "Parameters": {
        "CalibrationCurve": [
          { "RawValue": 0, "CalibratedValue": -20 },
          { "RawValue": 2048, "CalibratedValue": 25 },
          { "RawValue": 4095, "CalibratedValue": 80 }
        ]
      }
    }
  ],
  "DspFilters": [
    {
      "Type": "movingaverage",
      "Parameters": { "Window": 5 }
    }
  ]
}
```

### Power Meter

```json
{
  "Transforms": [
    {
      "Type": "calibration",
      "Parameters": { "Scale": 0.001, "Offset": 0 }
    }
  ],
  "DspFilters": [
    {
      "Type": "kalman",
      "Parameters": { "ProcessNoise": 0.01, "MeasurementNoise": 0.1 }
    }
  ]
}
```

### Position Tracking

```json
{
  "DspFilters": [
    {
      "Type": "kalman",
      "Parameters": {
        "ProcessNoise": 0.001,
        "MeasurementNoise": 0.1
      }
    }
  ]
}
```

### Non-negative Sensor with Smoothing

```json
{
  "DspFilters": [
    {
      "Type": "relu"
    },
    {
      "Type": "movingaverage",
      "Parameters": { "Window": 5 }
    }
  ]
}
```

# Transforms and DSP Filters Reference

Complete reference for data transformation and signal processing options.

## Transforms (ITelemetryTransform)

Transforms modify individual telemetry values before transmission.

### Linear Transform

Most common transform for scaling and offset adjustment.

```json
{
  "Type": "linear",
  "Parameters": {
    "Scale": 0.1,
    "Offset": -40
  }
}
```

**Formula:** `output = (input * Scale) + Offset`

**Use Cases:**
- Raw ADC to engineering units: Scale=0.00122, Offset=0 (for 12-bit ADC)
- Temperature sensor: Scale=0.1, Offset=-40 (for sensors returning value*10+40)
- Percentage conversion: Scale=100, Offset=0

### Polynomial Transform

For non-linear sensor calibration curves.

```json
{
  "Type": "polynomial",
  "Parameters": {
    "Coefficients": [0.5, 0.02, 0.0001]
  }
}
```

**Formula:** `output = C[0] + C[1]*x + C[2]*x² + ...`

**Use Cases:**
- Thermistor temperature compensation
- Non-linear sensor calibration
- Curve fitting corrections

### Lookup Table Transform

For discrete value mapping or interpolation.

```json
{
  "Type": "lookup",
  "Parameters": {
    "Table": {
      "0": 0.0,
      "1000": 25.0,
      "2000": 50.0,
      "3000": 75.0,
      "4000": 100.0
    },
    "Interpolate": true
  }
}
```

**Use Cases:**
- RTD temperature lookup
- Discrete state mapping
- Custom calibration tables

### Expression Transform

For complex mathematical expressions.

```json
{
  "Type": "expression",
  "Parameters": {
    "Formula": "sqrt(x * x + y * y)",
    "Variables": {
      "y": "OtherSensorResourceId"
    }
  }
}
```

**Use Cases:**
- Multi-sensor calculations
- Complex engineering formulas
- Derived measurements

## DSP Filters (IDspFilter)

Filters process streams of data for noise reduction and signal conditioning.

### Moving Average Filter

Simple averaging over a sliding window.

```json
{
  "Type": "movingaverage",
  "Parameters": {
    "WindowSize": 5
  }
}
```

**Properties:**
- Reduces random noise
- Introduces lag proportional to window size
- Good for stable signals with white noise

**Use Cases:**
- Temperature smoothing
- Pressure readings
- General noise reduction

### Kalman Filter

Optimal estimation for noisy measurements.

```json
{
  "Type": "kalman",
  "Parameters": {
    "ProcessNoise": 0.01,
    "MeasurementNoise": 0.1,
    "InitialEstimate": 0,
    "InitialErrorCovariance": 1
  }
}
```

**Properties:**
- Adapts to signal dynamics
- Provides optimal noise reduction
- Handles varying noise levels

**Use Cases:**
- Position/velocity tracking
- Fluctuating sensor readings
- Real-time estimation

### Low Pass Filter

Attenuates high-frequency components.

```json
{
  "Type": "lowpass",
  "Parameters": {
    "CutoffFrequency": 1.0,
    "SampleRate": 10.0,
    "Order": 2
  }
}
```

**Properties:**
- Removes high-frequency noise
- Preserves slowly changing signals
- Configurable cutoff frequency

**Use Cases:**
- Vibration filtering
- Power signal smoothing
- Audio/sensor denoising

### High Pass Filter

Attenuates low-frequency components (drift removal).

```json
{
  "Type": "highpass",
  "Parameters": {
    "CutoffFrequency": 0.1,
    "SampleRate": 10.0
  }
}
```

**Use Cases:**
- DC offset removal
- Drift compensation
- AC signal extraction

### Deadband Filter

Only reports changes exceeding a threshold.

```json
{
  "Type": "deadband",
  "Parameters": {
    "Threshold": 0.5,
    "Mode": "absolute"
  }
}
```

**Modes:**
- `absolute`: Change must exceed Threshold value
- `percentage`: Change must exceed Threshold % of current value

**Use Cases:**
- Reduce telemetry bandwidth
- Ignore sensor noise
- Report only significant changes

### Rate Limiter

Limits rate of change.

```json
{
  "Type": "ratelimiter",
  "Parameters": {
    "MaxRatePerSecond": 10.0
  }
}
```

**Use Cases:**
- Spike suppression
- Gradual transition enforcement
- Actuator protection

### Median Filter

Non-linear filter for impulse noise.

```json
{
  "Type": "median",
  "Parameters": {
    "WindowSize": 5
  }
}
```

**Properties:**
- Excellent for impulse/spike noise
- Preserves edges better than averaging
- Non-linear (good for outliers)

**Use Cases:**
- Sensor spike removal
- Image-like data processing
- Outlier rejection

## Combining Transforms and Filters

Transforms and filters are applied in order. Design your pipeline carefully:

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 5000,
    "Transforms": [
      {
        "Type": "linear",
        "Parameters": { "Scale": 0.1, "Offset": 0 }
      }
    ],
    "DspFilters": [
      {
        "Type": "deadband",
        "Parameters": { "Threshold": 0.5 }
      },
      {
        "Type": "movingaverage",
        "Parameters": { "WindowSize": 3 }
      }
    ]
  }
}
```

**Processing Order:**
1. Raw sensor value received
2. Transforms applied (in order): Linear scaling
3. DSP Filters applied (in order): Deadband → Moving Average
4. Final value sent to cloud

## Common Sensor Pipeline Patterns

### Temperature Sensor (Thermistor)

```json
{
  "Transforms": [
    {
      "Type": "polynomial",
      "Parameters": {
        "Coefficients": [25.0, -0.05, 0.0001]
      }
    }
  ],
  "DspFilters": [
    {
      "Type": "median",
      "Parameters": { "WindowSize": 3 }
    },
    {
      "Type": "movingaverage",
      "Parameters": { "WindowSize": 5 }
    }
  ]
}
```

### Power Meter

```json
{
  "Transforms": [
    {
      "Type": "linear",
      "Parameters": { "Scale": 0.001, "Offset": 0 }
    }
  ],
  "DspFilters": [
    {
      "Type": "lowpass",
      "Parameters": { "CutoffFrequency": 0.5, "SampleRate": 1.0 }
    },
    {
      "Type": "deadband",
      "Parameters": { "Threshold": 0.01, "Mode": "percentage" }
    }
  ]
}
```

### Vibration Sensor

```json
{
  "DspFilters": [
    {
      "Type": "highpass",
      "Parameters": { "CutoffFrequency": 1.0, "SampleRate": 100.0 }
    },
    {
      "Type": "lowpass",
      "Parameters": { "CutoffFrequency": 50.0, "SampleRate": 100.0 }
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
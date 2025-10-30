# How to Add DSP Filters

Digital Signal Processing (DSP) filters help you clean and enhance sensor data by removing noise, smoothing signals, and extracting meaningful patterns.

## Overview

The Weda SubNode SDK includes built-in DSP filters:
- **Moving Average** - Smooth noisy data
- **Kalman Filter** - Optimal estimation for noisy measurements
- **ReLU Filter** - Rectified Linear Unit for thresholding

## Built-in DSP Filters

### 1. Moving Average Filter

Smooths data by averaging recent values:

```csharp
using Weda.SubNode.Core.Dsp;

var filter = new MovingAverageFilter(windowSize: 5);
```

**Configuration:**
```json
{
  "Sensors": [
    {
      "ResourceId": "temp",
      "DspFilters": [
        {
          "Type": "MovingAverage",
          "WindowSize": 5
        }
      ]
    }
  ]
}
```

**Use Cases:**
- Remove high-frequency noise
- Smooth temperature readings
- Reduce sensor jitter

### 2. Kalman Filter

Optimal recursive filter for noisy measurements:

```csharp
var filter = new KalmanFilter(
    processNoise: 0.01,    // Q: how much we trust the model
    measurementNoise: 0.1, // R: how much we trust the measurements
    estimationError: 1.0,  // P: initial estimation error
    initialValue: 0.0      // Initial state estimate
);
```

**Configuration:**
```json
{
  "DspFilters": [
    {
      "Type": "Kalman",
      "ProcessNoise": 0.01,
      "MeasurementNoise": 0.1,
      "EstimationError": 1.0
    }
  ]
}
```

**Use Cases:**
- GPS position tracking
- Battery state estimation
- Sensor fusion
- Predictive maintenance

### 3. ReLU Filter

Applies threshold with optional dead zone:

```csharp
var filter = new ReluFilter(threshold: 10.0);

// With dead zone (output 0 if input < threshold)
var filterWithDeadZone = new ReluFilter(
    threshold: 10.0,
    useDeadZone: true
);
```

**Use Cases:**
- Remove negative values
- Apply minimum thresholds
- Detect activation levels

## Adding Filters to Your Device

### Method 1: Configuration (Recommended)

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "Sensors": [
        {
          "ResourceId": "vibration",
          "Name": "Vibration Sensor",
          "DspFilters": [
            {
              "Type": "MovingAverage",
              "WindowSize": 10
            },
            {
              "Type": "Kalman",
              "ProcessNoise": 0.01,
              "MeasurementNoise": 0.1
            }
          ]
        }
      ]
    }
  }
}
```

### Method 2: Programmatic

```csharp
public class MyDevice : TcpModbusDevice
{
    protected override void ConfigureFilters()
    {
        var pipeline = _telemetryPipeline;

        // Add filter to specific sensor
        var vibrationPipeline = pipeline.GetSensorPipeline("vibration");
        vibrationPipeline.AddFilter(new MovingAverageFilter(10));
        vibrationPipeline.AddFilter(new KalmanFilter(0.01, 0.1, 1.0, 0.0));
    }
}
```

## Creating Custom DSP Filters

### Step 1: Implement IDspFilter

```csharp
using Weda.SubNode.Abstractions.Dsp;

public class ExponentialSmoothingFilter : IDspFilter
{
    private readonly double _alpha; // Smoothing factor (0-1)
    private double? _lastValue;

    public ExponentialSmoothingFilter(double alpha)
    {
        if (alpha < 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1");

        _alpha = alpha;
    }

    public double Filter(double value)
    {
        if (!_lastValue.HasValue)
        {
            _lastValue = value;
            return value;
        }

        // Exponential smoothing formula
        _lastValue = _alpha * value + (1 - _alpha) * _lastValue.Value;
        return _lastValue.Value;
    }

    public void Reset()
    {
        _lastValue = null;
    }
}
```

### Step 2: Use Your Custom Filter

```csharp
pipeline.AddFilter(new ExponentialSmoothingFilter(alpha: 0.3));
```

## Advanced Filter Examples

### Example 1: Median Filter

Remove outliers using median:

```csharp
public class MedianFilter : IDspFilter
{
    private readonly int _windowSize;
    private readonly Queue<double> _window = new();

    public MedianFilter(int windowSize)
    {
        _windowSize = windowSize;
    }

    public double Filter(double value)
    {
        _window.Enqueue(value);

        while (_window.Count > _windowSize)
            _window.Dequeue();

        // Calculate median
        var sorted = _window.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        else
            return sorted[mid];
    }

    public void Reset() => _window.Clear();
}
```

### Example 2: Band-Pass Filter

Allow only specific frequency range:

```csharp
public class BandPassFilter : IDspFilter
{
    private readonly double _lowCutoff;
    private readonly double _highCutoff;
    private readonly Queue<double> _history;

    public double Filter(double value)
    {
        // Simplified: implement proper DSP band-pass
        // using FFT or IIR filter coefficients
        // ...
    }
}
```

### Example 3: Adaptive Filter

Auto-adjust based on signal characteristics:

```csharp
public class AdaptiveNoiseFilter : IDspFilter
{
    private double _noiseEstimate;
    private const double LearningRate = 0.1;

    public double Filter(double value)
    {
        // Estimate noise level
        var error = value - _noiseEstimate;
        _noiseEstimate += LearningRate * error;

        // Remove estimated noise
        return value - _noiseEstimate;
    }
}
```

## Filter Combination Strategies

### Strategy 1: Serial Filters

Apply filters in sequence:

```csharp
pipeline
    .AddFilter(new MedianFilter(5))              // Remove outliers first
    .AddFilter(new MovingAverageFilter(10))      // Then smooth
    .AddFilter(new KalmanFilter(...));           // Finally optimize
```

### Strategy 2: Parallel Filters

Apply different filters to copies:

```csharp
var data = telemetryData.Value;

var smoothed = new MovingAverageFilter(5).Filter(data);
var predicted = new KalmanFilter(...).Filter(data);

// Use both results
telemetryData.Metadata["Smoothed"] = smoothed;
telemetryData.Metadata["Predicted"] = predicted;
```

## Best Practices

### 1. Choose the Right Filter

- **Moving Average**: General smoothing, simple
- **Kalman**: Optimal for linear systems with Gaussian noise
- **Median**: Good for removing spikes/outliers
- **Exponential Smoothing**: Responsive to trends

### 2. Tune Parameters

```csharp
// Test different window sizes
for (int windowSize = 3; windowSize <= 20; windowSize++)
{
    var filter = new MovingAverageFilter(windowSize);
    var mse = EvaluateFilter(filter, testData);
    Console.WriteLine($"Window {windowSize}: MSE = {mse}");
}
```

### 3. Monitor Filter Performance

```csharp
pipeline.FilterApplied += (sender, e) =>
{
    _logger.LogDebug("Filter {FilterType}: {Before} -> {After}",
        e.FilterType,
        e.BeforeValue,
        e.AfterValue);
};
```

### 4. Handle Edge Cases

```csharp
public double Filter(double value)
{
    // Handle NaN and infinity
    if (double.IsNaN(value) || double.IsInfinity(value))
    {
        _logger.LogWarning("Invalid value detected: {Value}", value);
        return _lastValidValue ?? 0.0;
    }

    // Apply filter
    // ...
}
```

## Testing DSP Filters

```csharp
[Fact]
public void MovingAverage_Should_Smooth_Noisy_Signal()
{
    // Arrange
    var filter = new MovingAverageFilter(5);
    var noisyData = new[] { 10.0, 12.0, 11.0, 13.0, 10.0 };

    // Act
    var filtered = noisyData.Select(d => filter.Filter(d)).ToArray();

    // Assert
    var variance = CalculateVariance(filtered);
    var originalVariance = CalculateVariance(noisyData);
    Assert.True(variance < originalVariance, "Filtered data should have lower variance");
}
```

## Performance Optimization

### 1. Use Fixed-Size Buffers

```csharp
private readonly double[] _buffer = new double[MaxWindowSize];
private int _index = 0;

public double Filter(double value)
{
    _buffer[_index] = value;
    _index = (_index + 1) % MaxWindowSize;

    // Use _buffer for calculations
}
```

### 2. Avoid Unnecessary Allocations

```csharp
// X Bad: Creates new array each time
public double Filter(double value)
{
    var window = _history.ToArray();
    return window.Average();
}

// *> Good: Reuse existing data structure
public double Filter(double value)
{
    return _history.Average();
}
```

## Next Steps

- [Using Hooks](05_using_hooks.md) - Intercept filter pipeline
- [Architecture Overview](../architecture/overview.md) - Understand data flow

## Summary

DSP filters in Weda SubNode SDK:
- **Built-in filters** for common use cases
- **Custom filters** via `IDspFilter` interface
- **Configuration** or programmatic setup
- **Chainable** for complex processing
- **Testable** with sample data

Remember:
- Choose appropriate filter for your signal characteristics
- Tune parameters using real data
- Monitor filter performance in production
- Test edge cases thoroughly
